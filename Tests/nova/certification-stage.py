"""Configured local certification stages; no natural-language routing or repairs.

Runs existing test owners. Every test result remains independently inspectable.
Only the user-authorized local-v1 copy criteria are scoped; raw legacy evidence
is preserved. No private database or pre-existing clipboard content is read.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import xml.etree.ElementTree as ET


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def case_id(owner, name):
    return owner+'/'+hashlib.sha256(name.encode('utf-8')).hexdigest()[:24]


def backend_build_command(project):
    return ['dotnet','build',str(project),'--no-restore','--no-incremental']


def relay_development_events(reader, feed, target, campaign, seen):
    """Forward actual isolated QA observations; preserve origin IDs and times.

    Uses the Lab's existing bounded, redacting schema adapter. This never reads
    an application DB or creates model reasoning, assertions or authorizations.
    """
    forwarded=[]
    for name in ('app.jsonl','core.jsonl'):
        origin=feed/name
        rows,error=reader.read_events(origin)
        if error:raise OSError(error)
        for event in rows:
            identity=event.get('id')
            if not isinstance(identity,str) or identity in seen:continue
            forwarded.append({**event,'campaignId':campaign,'originCampaignId':event.get('campaignId'),
                              'originJournal':str(origin)})
            seen.add(identity)
    if forwarded:
        target.parent.mkdir(parents=True,exist_ok=True)
        raw=''.join(json.dumps(e,ensure_ascii=False)+'\n' for e in forwarded).encode('utf-8')
        fd=os.open(target,os.O_WRONLY|os.O_CREAT|os.O_APPEND,0o600)
        try:
            if os.write(fd,raw)!=len(raw):raise OSError('Incomplete observation append')
        finally:os.close(fd)
    return len(forwarded)


def browser_rows(report, events):
    rows=[]
    for check in report['checks']:
        ids={run['runId'] for run in report['runs'] if
             'real-qwen-'+str((run.get('taskEvidence') or {}).get('scenario'))==check['name']}
        rows.append({'id':check['name'],'title':check['name'],'status':check['status'],
                     'expected':'PASS','observed':check,
                     'events':[event for event in events if event.get('runId') in ids]})
    return rows


def publish_live_ui(report_path, reader, feed, campaign):
    """Read-only view of the real harness checkpoints, followed by final QA."""
    live=report_path.with_name('live.json')
    if report_path.exists():
        raw=report_path.read_bytes()
    else:
        paths=list((report_path.parent/'ui').glob('*/results.json'))
        if len(paths)!=1:return
        receipt=read(paths[0])
        events=[]
        for name in ('app.jsonl','core.jsonl'):
            events.extend(reader.read_events(feed/name)[0])
        rows=browser_rows(receipt,events)
        raw=json.dumps({'schema':'ai-delivery-tests/1','campaignId':campaign,
            'status':receipt['status'],'count':len(rows),'skipped':0,'rows':rows,
            'acceptanceComplete':False,'sourceReceipt':{'path':str(paths[0]),'sha256':sha(paths[0])},
            'limits':'Live checkpoints from the existing independent browser assertions; partial results do not certify release.'},
            ensure_ascii=False,indent=2).encode('utf-8')
    if live.exists() and live.read_bytes()==raw:return
    temp=live.with_suffix('.tmp');temp.write_bytes(raw);temp.replace(live)


def main(args):
    repo=Path(__file__).resolve().parents[2]
    lab=Path(args.lab_root).resolve()
    sys.path.insert(0,str(lab))
    from ai_delivery_lab.telemetry import Journal, location
    candidate=args.candidate
    if not candidate or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in candidate):
        raise ValueError('Explicit frozen candidate required')
    runtime=repo/'.playwright/closure-lab-20261021'/candidate
    manifest=read(repo/'docs/evidence/closure-lab-20261021'/candidate/'manifest.json')
    for item in manifest['files']:
        file=(runtime/item['path']).resolve()
        if not file.is_relative_to(runtime) or sha(file)!=item['sha256']:
            raise ValueError('Frozen candidate integrity mismatch')
    report_path=Path(args.report).resolve()
    if report_path.exists():raise ValueError('Preserve prior stage evidence')
    report_path.parent.mkdir(parents=True,exist_ok=True)
    output=report_path.parent
    env={**os.environ,'PYTHONPATH':os.pathsep.join([str(runtime/'sdk'),str(lab)]),'PYDANTIC_AI_NO_BANNER':'1'}
    journal=Journal(args.events,args.campaign)
    rows=[]
    observation={'forwarded':0,'errors':[],'scope':'OWNED_SYNTHETIC_QA_FEED_ONLY'}
    feed=None

    def record(identity,title,status,expected,observed,where=None,metadata=None):
        row={'id':identity,'title':title,'status':status,'observedAt':datetime.now(timezone.utc).isoformat(),
             'expected':expected,'observed':observed,'location':where or location(main)}
        if metadata:row.update(metadata)
        rows.append(row)
        journal.case_id=identity
        journal.emit('certification_case','testing',status,owner='Existing local test owner',where=row['location'],expected=expected,observed=observed)

    def execute(name,command,cwd=repo,environment=None,timeout=900):
        journal.case_id=name;journal.emit('suite_started','testing','RUNNING',owner=name,where=location(main))
        stop=threading.Event();seen=set()
        def pump():
            if feed is None:return
            try:
                observation['forwarded']+=relay_development_events(reader,feed,Path(args.events),args.campaign,seen)
                if args.mode=='ui':publish_live_ui(report_path,reader,feed,args.campaign)
            except (OSError,ValueError) as error:
                message=type(error).__name__+': observation relay unavailable'
                if message not in observation['errors']:observation['errors'].append(message)
        def observe():
            while not stop.wait(0.5):pump()
        thread=threading.Thread(target=observe,daemon=True)
        if feed is not None:thread.start()
        try:result=subprocess.run([str(x) for x in command],cwd=cwd,env=environment or env,capture_output=True,timeout=timeout)
        finally:
            stop.set()
            if feed is not None:thread.join();pump()
        (output/(name+'.log')).write_bytes(result.stdout+b'\n'+result.stderr)
        return result

    if args.mode=='units':
        # Unit binaries are rebuilt only when their source matches this exact
        # candidate. Semantic/UI campaigns use the frozen binaries directly.
        for item in manifest['files']:
            relative=item['path'].replace('\\','/')
            if relative.startswith(('source/backend/','source/src/')):
                current=repo/relative.removeprefix('source/')
                if not current.exists() or sha(current)!=item['sha256']:
                    raise ValueError('Current C# source differs from frozen candidate; do not mix test builds')
        trx=output/'backend.trx'
        # Restoring rejected source with its original timestamp can leave a
        # newer assembly from that experiment in bin/obj. Source hashes alone
        # cannot make an incremental compilation evidence of this candidate.
        compiled=execute('backend-build',backend_build_command('backend/QuickPrompt.Tests'),timeout=900)
        if compiled.returncode:record('backend/build','Compile current backend source','FAIL',0,compiled.returncode)
        before=len(rows)
        dotnet=compiled if compiled.returncode else execute('backend',['dotnet','test','backend/QuickPrompt.Tests','--no-build','--no-restore','--logger','trx;LogFileName=backend.trx','--results-directory',output],timeout=900)
        if trx.exists():
            spec=importlib.util.spec_from_file_location('trx_results',Path(__file__).with_name('trx-results.py'))
            importer=importlib.util.module_from_spec(spec);spec.loader.exec_module(importer)
            try:
                imported=importer.backend_results(trx,runtime/'source/backend')
            except (ValueError,ET.ParseError) as error:
                record('backend/import-contract','Backend result identity and completeness','FAIL','complete unique executions',str(error))
            else:
                for test in imported:
                    metadata={k:v for k,v in test.items() if k not in {'id','title','status','expected','observed','location'}}
                    record(test['id'],test['title'],test['status'],test['expected'],test['observed'],test.get('location'),metadata)
        if not trx.exists() or dotnet.returncode or len(rows)==before:
            record('backend/command','Backend test command','FAIL',0,dotnet.returncode)
        elif int(ET.parse(trx).find('.//{*}Counters').get('total','-1'))!=len(rows)-before:
            record('backend/completeness','Backend declared tests represented','FAIL','all',len(rows)-before)
        web=repo/'apps/web'
        vitest_file=output/'react.json'
        before=len(rows)
        react=execute('react',[args.node,web/'node_modules/vitest/vitest.mjs','run','--maxWorkers=2','--reporter=json','--outputFile='+str(vitest_file)],cwd=web)
        if vitest_file.exists():
            for suite in read(vitest_file).get('testResults',[]):
                for test in suite.get('assertionResults',[]):
                    status={'passed':'PASS','failed':'FAIL'}.get(test['status'],'SKIP')
                    name=test.get('fullName') or test.get('title')
                    record(case_id('react',str(Path(suite['name']).relative_to(web))+':'+name),name,status,'passed',test['status'])
        if not vitest_file.exists() or react.returncode or len(rows)==before:
            record('react/command','React test command','FAIL',0,react.returncode)
        elif read(vitest_file).get('numTotalTests')!=len(rows)-before:
            record('react/completeness','React declared tests represented','FAIL','all',len(rows)-before)
        for name,test_args,cwd in [('core',['--tests',str(Path(args.core_root)/'tests')],Path(args.core_root)),
                                   ('auditor',['--test-file',str(repo/'Tests/nova/audit-semantic-boundary.test.py')],repo),
                                   ('trx-import',['--test-file',str(repo/'Tests/nova/trx-results.test.py')],repo),
                                   ('composition',['--test-file',str(repo/'Tests/nova/configure-certification.test.py')],repo),
                                   ('compilation',['--test-file',str(repo/'Tests/nova/compilation-provenance.test.py')],repo)]:
            result_file=output/(name+'.json')
            result=execute(name,[sys.executable,'-X','utf8','-B','-m','ai_delivery_lab.test_runner',*test_args,
                '--events',args.events,'--out',result_file,'--campaign',args.campaign+'-'+name,'--public-fixtures'],cwd=cwd)
            if result_file.exists():
                for row in read(result_file)['rows']:
                    rows.append({**row,'id':name+'/'+row['id']})
            if not result_file.exists() or result.returncode or not read(result_file).get('rows'):
                record(name+'/command',name+' test command','FAIL',0,result.returncode)
        junit=output/'node.xml'
        tests=['certify-lab.test.cjs','run-lab-goal.test.cjs','acceptance.test.cjs']
        node=execute('node',[args.node,'--test','--test-reporter=junit','--test-reporter-destination='+str(junit),
                            *[repo/'Tests/nova'/name for name in tests]])
        if junit.exists():
            for i,test in enumerate(ET.parse(junit).findall('.//testcase')):
                status='FAIL' if test.find('failure') is not None or test.find('error') is not None else 'SKIP' if test.find('skipped') is not None else 'PASS'
                record(case_id('node',test.get('classname','')+':'+test.get('name')),test.get('name'),status,'PASS',status)
        if not junit.exists() or node.returncode or not ET.parse(junit).findall('.//testcase'):record('node/command','Node contract tests','FAIL',0,node.returncode)
        if not rows:raise ValueError('Empty component suite')
        if len({r['id'] for r in rows})!=len(rows):
            record('components/duplicate-identity','Ambiguous test identity','FAIL','unique identities','duplicate test names')
        report={'schema':'ai-delivery-tests/1','campaignId':args.campaign,'profile':{'semanticCore':'FROZEN_SOURCE_COMPONENT_TESTS'},
                'candidate':candidate,'rows':rows,'count':len(rows),'skipped':sum(r['status']=='SKIP' for r in rows),
                'status':'PASS' if all(r['status']=='PASS' for r in rows) else 'FAIL'}
    else:
        frontend=Path(args.frontend).resolve()
        if not frontend.is_relative_to(repo/'.playwright'):raise ValueError('Frozen frontend required')
        # The shared developer feed can reach its fixed capacity. Each stage
        # owns a fresh feed; the prior journal and its safety cap stay intact.
        feed=output/'lab-feed';feed.mkdir(exist_ok=False)
        from ai_delivery_lab.visual_store import VisualStore
        reader=VisualStore({'sourceRoots':[str(repo),str(lab)],'journal':str(feed/'app.jsonl'),
                            'artifacts':str(output/'observation')})
        env.update({'ASPNETCORE_ENVIRONMENT':'Development','DOTNET_ENVIRONMENT':'Development',
            'NOVA_TEST_BUILD':str(runtime/'eval/QuickPrompt.Api.dll'),'NOVA_TEST_WEBROOT':str(frontend),
            'PYTHON':sys.executable,'NOVA_CORE_SDK':str(runtime/'sdk'),
            'NOVA_AGENT_OPTIONS':json.dumps(['--QuickPrompt:Agent:Runtime=adaptive-core',
                '--QuickPrompt:AdaptiveCore:Python='+sys.executable,'--QuickPrompt:AdaptiveCore:Worker='+str(runtime/'nova_worker.py'),
                '--QuickPrompt:AdaptiveCore:SdkRoot='+str(runtime/'sdk'),'--QuickPrompt:Diagnostics:Lab:Enabled=true',
                '--QuickPrompt:Diagnostics:Lab:CaptureSyntheticContent=true',
                '--QuickPrompt:Diagnostics:Lab:Directory='+str(feed)])})
        if args.mode=='ui':
            ui_root=output/'ui'
            env['NOVA_COPY_EVIDENCE_ROOT']=str(ui_root.relative_to(repo))
            env['NOVA_COPY_SCENARIOS']=os.environ.get('NOVA_CERTIFICATION_SCENARIOS','prepare,actual,read,read_incidental,field_data,paraphrase,adapt_copy,provider_prepare')
            result=execute('browser',[args.node,repo/'Tests/nova/clipboard-agent.cjs','--real-qwen','--adaptive-core'],environment=env,timeout=2400)
            files=list(ui_root.glob('*/results.json'))
            if len(files)!=1:raise ValueError('Exactly one fresh UI receipt required')
            report=read(files[0]);report['sourceReceipt']={'path':str(files[0]),'sha256':sha(files[0])}
            observed_events=reader.read_events(Path(args.events))[0]
            report['rows']=browser_rows(report,[event for event in observed_events if event.get('campaignId')==args.campaign])
            report['count']=len(report['rows']);report['skipped']=0
            if result.returncode:report['status']='FAIL'
        else:
            env['NOVA_NATIVE_EVIDENCE_ROOT']=str((output/'copy').relative_to(repo))
            result=execute('copy',[args.node,repo/'Tests/nova/native-clipboard-probe.cjs'],environment=env,timeout=120)
            files=list((output/'copy').glob('*/results.json'))
            if len(files)!=1:raise ValueError('Exactly one fresh native copy receipt required')
            raw=read(files[0])
            ids=['windows-standard-unicode-text-and-lines','downloaded-utf8-lf-exact-no-side-effects','downloaded-unsaved-draft-utf8-lf-no-side-effects']
            if any(len([r for r in raw['rows'] if r['id']==identity])!=1 for identity in ids):
                raise ValueError('Incomplete local copy contracts')
            rows=[r for r in raw['rows'] if r['id'] in ids]
            report={**raw,'scope':'USER_AUTHORIZED_LOCAL_V1_DUAL_COPY','rows':rows,'count':3,'skipped':0,
                    'status':'PASS' if all(r['status']=='PASS' for r in rows) else 'FAIL',
                    'sourceReceipt':{'path':str(files[0]),'sha256':sha(files[0]),'originalStatus':raw['status'],'exitCode':result.returncode},
                    'excludedLegacyCriterion':'RAW_LF_IN_WINDOWS_CF_UNICODETEXT; replaced by explicit local-v1 user scope'}
    report['observation']=observation
    with report_path.open('x',encoding='utf-8') as file:json.dump(report,file,ensure_ascii=False,indent=2)
    if args.mode=='ui':publish_live_ui(report_path,reader,feed,args.campaign)
    print(json.dumps({'status':report['status'],'count':report['count'],'report':str(report_path)}))
    return 0 if report['status']=='PASS' else 1


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--mode',choices=['units','ui','copy'],required=True)
    for name in ['candidate','lab-root','core-root','node','events','campaign','report']:
        parser.add_argument('--'+name,required=True)
    parser.add_argument('--frontend')
    raise SystemExit(main(parser.parse_args()))
