"""Bounded synthetic causal contrast; never production or release acceptance.

Only the selected catalog, source-context or cross-field schema factor varies.
Framework/provider/protocol/budgets are frozen and hashed.
Original dataset labels are used AFTER inference by the independent comparator.
No holdout, private data, new model or additional judge is used.
"""
import asyncio
import copy
import hashlib
import importlib.util
import json
import os
import shutil
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
coupling = '--provider-coupling' in sys.argv[2:]
grouping = '--grouped-outcomes' in sys.argv[2:]
feedback = '--feedback-detail' in sys.argv[2:]
operation_provider = '--operation-provider' in sys.argv[2:]
stage_context = '--stage-context' in sys.argv[2:]
nonthinking = '--nonthinking' in sys.argv[2:]
if sum([coupling,grouping,operation_provider,stage_context,nonthinking])>1:raise ValueError('One factor per contrast')
if feedback and not coupling:raise ValueError('Feedback contrast requires the unchanged provider-coupling schema')
current = coupling or grouping or operation_provider or stage_context or nonthinking
candidate = 'dual-copy-title-authority-safe' if current else 'input-authority-v9'
FROZEN = ROOT / '.playwright/closure-lab-20261021' / candidate
OUT = Path(sys.argv[1]).resolve()
if OUT.exists() or os.environ.get('NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE') != '1':
    raise ValueError('NEW output and caller-held exclusive evaluation lease required')
OUT.mkdir(parents=True)
sdk = FROZEN / 'sdk'
if feedback:
    sdk = OUT / 'sdk'
    shutil.copytree(FROZEN / 'sdk', sdk, ignore=shutil.ignore_patterns('__pycache__'))
    shutil.copyfile(ROOT.parent / 'Adaptive-AI-Core/adaptive_core/inference.py', sdk / 'adaptive_core/inference.py')
sys.path.insert(0, str(sdk))
spec = importlib.util.spec_from_file_location('boundary_audit', Path(__file__).with_name('audit-semantic-boundary.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
read = audit.read
write = audit.write

def provider_coupling_schema(schema):
    result=copy.deepcopy(schema)
    provider_effect={'type':'object','properties':{'operation':{'const':'PREPARE_PROVIDER'}},'required':['operation']}
    result['allOf']=[
        {'if':{'properties':{'providerId':{'type':'string'}},'required':['providerId']},
         'then':{'properties':{'effects':{'contains':provider_effect}}}},
        {'if':{'properties':{'effects':{'contains':provider_effect}},'required':['effects']},
         'then':{'properties':{'providerId':{'type':'string'}},'required':['providerId']}}]
    return result

def operation_provider_schema(schema):
    result=copy.deepcopy(schema)
    destination=result['properties'].pop('providerId')
    result['required']=[name for name in result['required'] if name!='providerId']
    ordinary,clarification=result['properties']['effects']['items']['anyOf']
    preparation=copy.deepcopy(ordinary)
    ordinary['properties']['operation']['enum'].remove('PREPARE_PROVIDER')
    preparation['properties']['operation']['enum']=['PREPARE_PROVIDER']
    preparation['properties']['providerId']={**destination['anyOf'][0],
        'description':'Registry destination identity for this PREPARE_PROVIDER outcome only. It does not authorize submission.'}
    preparation['required'].append('providerId')
    result['properties']['effects']['items']['anyOf']=[ordinary,clarification,preparation]
    return result
base = read(ROOT / 'docs/evidence/closure-lab-20261021/causal-study-20261022/counterfactual-host.json')
reference = read(ROOT / 'docs/evidence/closure-lab-20261021' / candidate / 'manifest.json')
for item in reference['files']:
    if audit.digest(FROZEN / item['path']) != item['sha256']:
        raise ValueError('Frozen manifest mismatch')
dataset = read(ROOT / 'Tests/nova/datasets/recovery-v1/validation.json')
baseline = read(ROOT / ('docs/evidence/closure-lab-20261021/rel05-safe-validation-20261022/validation.json' if current else 'docs/evidence/closure-lab-20261021/causal-v9-reproduction-20261022/validation.json'))
cases = [c for c in dataset['scenarios'] if c['id'][-2:] in ['03', '05', '11', '16']]
catalog = {
    'PROPOSE_ADAPTATION': 'Change the reusable instruction definition. This outcome alone neither prepares a copy nor selects a destination. Include the separate preparation outcome when it is also requested.',
    'PREPARE_PROVIDER': 'Select the destination identified by providerId and prepare the compiled instruction for transfer there without submitting it. This does not revise the instruction definition; revision is a separate requested outcome.',
    'PREPARE_COPY_PREVIEW': 'Render the current or revised instruction as a copy preview. This neither revises the instruction definition nor writes the clipboard. Revision and an immediate clipboard write are separate requested outcomes.'}
arms = ['nonthinking_only'] if nonthinking else ['without_application_model'] if stage_context else ['operation_provider_only'] if operation_provider else ['provider_coupling_only'] if feedback else ['grouped_outcomes_only'] if grouping else ['unchanged', 'provider_coupling_only'] if coupling else ['unchanged', 'catalog_description_only', 'source_text_context_only']
plan = {'scope': 'CONTROLLED_SYNTHETIC_DIAGNOSTIC_NOT_RELEASE', 'frozenCandidate': candidate,
        'model': baseline['model'], 'digest': baseline['modelDigest'],
        'arms': arms,
        'cases': [c['id'] for c in cases], 'maxCases': len(arms)*len(cases), 'limits': base['protocol']['limits'],
        'oraclePolicy': 'Original validation oracle is evaluator-only, unchanged and excluded from all model inputs.',
        'acceptance': 'Compare all required outcomes, forbidden/extra planned effects, source, outcome and Claude destination; diagnostic evidence only. Reject any arm with a failing control.',
        'previousRepresentationExperiments': 'coverage-v5 classification 3/4 and reviewed-v6 full validation 14/16; not repeated or accepted',
        'compositionCause': 'NOT_ISOLATED; no conclusion from catalog or source-context contrasts alone'}
if coupling:
    plan['hypothesis']='Contradictory operation/destination metadata passes the original output schema and is silently discarded downstream. Cross-field validation may enable the existing bounded correction; mere metadata deletion does not fulfill the independent oracle.'
    from jsonschema import Draft202012Validator
    receipt_path=ROOT/'docs/evidence/closure-lab-20261021/safe-live-browser-development-20261022/browser.json'
    recorded=read(receipt_path)
    row=next(row for row in recorded['rows'] if row['id']=='real-qwen-provider_prepare')
    original_schema=next(e['inputs']['schema'] for e in row['events'] if e['kind']=='model_request')
    output=next(e['observed']['output'] for e in row['events'] if e['kind']=='core_proposal_observed')
    changed=provider_coupling_schema(original_schema)
    removed={**output,'providerId':None}
    correct={**output,'effects':[{'operation':'PREPARE_PROVIDER','supportingPassage':0}]}
    checks={'originalAcceptsRecordedContradiction':Draft202012Validator(original_schema).is_valid(output),
            'candidateRejectsRecordedContradiction':not Draft202012Validator(changed).is_valid(output),
            'metadataRemovalIsStructurallyValidButNotGoalCompletion':Draft202012Validator(changed).is_valid(removed),
            'correctCounterfactualIsStructurallyValid':Draft202012Validator(changed).is_valid(correct)}
    write(OUT/'contract-preflight.json',{'scope':'OFFLINE_PUBLIC_FIXTURE_NO_INFERENCE','receipt':str(receipt_path),
        'receiptSha256':audit.digest(receipt_path),'proposal':output,'correctSyntheticProposal':correct,'checks':checks})
    if not all(checks.values()):raise ValueError('Cross-field contrast preflight failed')
if feedback:
    plan['hypothesis']='Schema repair lacks the failed constraint. Only Core schema feedback varies versus the preserved provider-coupling arm; messages, schema, host, model and usage limits remain identical.'
    plan['controlReceipt']=str(ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/provider-coupling-study/results.json')
    plan['sdkHashes']={str(p.relative_to(sdk)):audit.digest(p) for p in sorted(sdk.rglob('*.py'))}
    plan['variant']='schema-feedback-detail-diagnostic'
if operation_provider:
    from jsonschema import Draft202012Validator
    proposed=operation_provider_schema(base['protocol']['outputSchema'])
    validator=Draft202012Validator(proposed)
    def fixture(effects):return {'source':'OPEN: Preparar reuniones','effects':effects}
    adaptation={'operation':'PROPOSE_ADAPTATION','supportingPassage':0}
    copy_preview={'operation':'PREPARE_COPY_PREVIEW','supportingPassage':0}
    preparation={'operation':'PREPARE_PROVIDER','supportingPassage':0,'providerId':'claude'}
    checks={'adaptationAndCopyHaveNoDestinationField':validator.is_valid(fixture([adaptation,copy_preview])),
            'providerPreparationCarriesItsOwnDestination':validator.is_valid(fixture([preparation])),
            'destinationWithoutPreparationRejected':not validator.is_valid(fixture([{**adaptation,'providerId':'claude'}])),
            'preparationWithoutDestinationRejected':not validator.is_valid(fixture([{'operation':'PREPARE_PROVIDER','supportingPassage':0}])),
            'globalDestinationRejected':not validator.is_valid({**fixture([adaptation]),'providerId':'ollama'})}
    if not all(checks.values()):raise ValueError('Operation destination schema preflight failed')
    write(OUT/'contract-preflight.json',{'scope':'OFFLINE_SYNTHETIC_SCHEMA_ONLY','checks':checks})
    plan['hypothesis']='A global mandatory destination solicits irrelevant metadata and cross-field repair can add an unrequested outcome. Attach destination exclusively to its owning operation, without changing messages, capabilities or effects.'
    plan['variant']='operation-local-provider-diagnostic'
    plan['controlReceipt']=str(ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/provider-coupling-study/results.json')
if stage_context:
    plan['hypothesis']='Workflow context repeats implementation-member and authoring-stage representation metadata alongside the original request. Remove only applicationModel to test interference; preserve original messages, schema, capability catalog, state, source roles, literals and authority rules.'
    plan['variant']='stage-context-ablation-diagnostic'
    plan['controlReceipt']=str(ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/provider-coupling-study/results.json')
if nonthinking:
    plan['hypothesis']='Original source/catalog/schema contrasts did not qualify semantic behavior. Test the existing Qwen3 non-thinking mode as a single provider configuration factor, with the original packet and all numeric inference limits unchanged.'
    plan['variant']='qwen3-nonthinking-diagnostic'
    plan['controlReceipt']=str(ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/provider-coupling-study/results.json')
if grouping:
    artifact=ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/grouped-output-schema-probe.json'
    outcome_schema=read(artifact)['protocol']['outputSchema']['properties']['outcomes']
    offered={op for group in outcome_schema['properties'].values() for op in group['items']['properties']['operation']['enum']}
    original=set(base['protocol']['outputSchema']['properties']['effects']['items']['anyOf'][0]['properties']['operation']['enum'])|{'ASK_CLARIFICATION'}
    if offered!=original:raise ValueError('Grouped schema changes the offered capability set')
    plan['hypothesis']='The flat outcome array can stop after one responsibility. Mandatory typed responsibility groups may retain independent instruction and delivery outcomes; only the output representation changes.'
    plan['schemaArtifact']={'path':str(artifact),'sha256':audit.digest(artifact),'scope':'Old prepared schema as test data only; current SDK, prompt, host and compiler remain in use.'}
    plan['controlReceipt']=str(ROOT/'docs/evidence/closure-lab-20261021/causal-study-20261022/provider-coupling-study/results.json')
write(OUT / 'plan.json', plan)
write(OUT / 'harness-source.json', {'studySha256':audit.digest(Path(__file__)), 'captureSha256':audit.digest(Path(__file__).with_name('audit-semantic-boundary.py'))})
lab_path = os.environ.get('NOVA_EVAL_LAB_EVENTS')
campaign = os.environ.get('NOVA_EVAL_LAB_CAMPAIGN')
journal = None
if lab_path and campaign:
    sys.path.insert(0, str(ROOT.parent/'AI-Delivery-Lab'))
    from ai_delivery_lab.telemetry import Journal, safe, location
    journal = Journal(lab_path, campaign)

def publish(rows, status):
    report={'schema':'ai-delivery-tests/1','profile':{'semanticCore':'CONTROLLED_SCHEMA_CONTRAST_NOT_CERTIFICATION'},
            'candidate':candidate+('+schema-feedback-detail-diagnostic' if feedback else '+operation-local-provider-diagnostic' if operation_provider else '+stage-context-ablation-diagnostic' if stage_context else '+qwen3-nonthinking-diagnostic' if nonthinking else ''),'model':baseline['model'],'status':status,'count':len(rows),'rows':rows,
            'skipped':0,'acceptanceComplete':False,'limits':'Independent diagnostic contrast only; no UI or release acceptance.'}
    temp=OUT/'live.tmp';temp.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');temp.replace(OUT/'live.json')

def observe(event):
    if journal:
        row=safe({**event,'campaignId':campaign,'caseId':journal.case_id,'runId':event['requestId']})
        fd=os.open(lab_path,os.O_WRONLY|os.O_CREAT|os.O_APPEND,0o600)
        try:
            raw=(json.dumps(row,ensure_ascii=False)+'\n').encode('utf-8')
            if os.write(fd,raw)!=len(raw):raise OSError('Incomplete observation append')
        finally:os.close(fd)
rows = []
publish(rows,'RUNNING')
for arm in plan['arms']:
    for case in cases:
        folder = OUT / (arm + '-' + case['id'][-2:]); folder.mkdir()
        protocol = copy.deepcopy(base['protocol'])
        packet = json.loads(protocol['messages'][1]['content'])
        packet['originalUserRequest'] = case['message']
        packet['directivePassages'] = [{'id': 0, 'text': case['message']}]
        if arm == 'catalog_description_only':
            for operation in packet['operations']:
                if operation['operation'] in catalog:
                    operation['description'] = catalog[operation['operation']]
        elif arm == 'source_text_context_only':
            packet['context']['sourceObservations'][0]['text'] = dataset['fixtures'][0][1]
        elif arm == 'provider_coupling_only':
            protocol['outputSchema']=provider_coupling_schema(protocol['outputSchema'])
        elif arm == 'grouped_outcomes_only':
            del protocol['outputSchema']['properties']['effects']
            protocol['outputSchema']['properties']['outcomes']=copy.deepcopy(outcome_schema)
            protocol['outputSchema']['required']=['outcomes' if name=='effects' else name for name in protocol['outputSchema']['required']]
        elif arm == 'operation_provider_only':
            protocol['outputSchema']=operation_provider_schema(protocol['outputSchema'])
        elif arm == 'without_application_model':
            del packet['applicationModel']
        elif arm == 'nonthinking_only':
            protocol['limits']['thinking']=False
        protocol['messages'][1]['content'] = json.dumps(packet, ensure_ascii=False, separators=(',', ':'))
        # Body serialization differs equally in all arms; native bytes and exact
        # digests are retained. Do not conflate this with historical byte replay.
        if journal:
            journal.case_id=arm+'/'+case['id']
            journal.emit('case_started','testing','RUNNING',owner='Controlled schema contrast',where=location(publish),
                         inputs={'request':case['message'],'arm':arm},expected={'oracle':'original validation; evaluator only'})
        result = asyncio.run(audit.capture_live(protocol, folder, baseline['modelDigest'], observe))
        reply = result['reply']; raw_output=reply.get('output');output=raw_output
        if grouping and isinstance(raw_output,dict):
            output={**raw_output,'effects':[effect for group in raw_output['outcomes'].values() for effect in group]}
            del output['outcomes']
        if operation_provider and isinstance(raw_output,dict):
            output={**raw_output,'providerId':next((effect['providerId'] for effect in raw_output['effects'] if effect['operation']=='PREPARE_PROVIDER'),None),
                    'effects':[{key:value for key,value in effect.items() if key!='providerId'} for effect in raw_output['effects']]}
        probe_input = {'syntheticOnly': True, 'context': 'selected_report', 'message': case['message'],
                       'model': baseline['model'], 'source': read(ROOT / 'docs/evidence/closure-lab-20261021/causal-study-20261022/counterfactual-input.json')['source'],
                       'proposals': [{'id': arm, 'output': output}]}
        host = None
        if reply['status'] == 'proposed':
            write(folder / 'probe-input.json', probe_input)
            probe = 'semantic-probe-dual-copy-final' if current else 'semantic-probe-v1'
            proc = subprocess.run(['dotnet', str(ROOT / '.playwright/closure-lab-20261021' / probe / 'Probe.dll'),
                                   str(folder / 'probe-input.json'), str(folder / 'host.json')], capture_output=True)
            (folder / 'probe.log').write_bytes(proc.stdout + proc.stderr)
            if proc.returncode == 0: host = read(folder / 'host.json')['rows'][0]
        observed = host.get('plan') if host else None
        previous = next(r for r in baseline['rows'] if r['id'] == case['id'])
        checks = {'hostAccepted': bool(host and host['acceptedByHost']),
                  'required': bool(observed and set(case['required']) <= set(observed['intentions'])),
                  'forbiddenAbsent': bool(observed and not set(case['forbidden']) & set(observed['intentions'])),
                  'effectCeiling': bool(observed and set(observed['intentions']) <= set(previous['expectedEffectCeiling'])),
                  'outcome': bool(observed and observed['outcome'] in case['outcomes']),
                  'source': bool(output and output.get('source') == 'OPEN: Preparar reuniones'),
                  'destination': case['id'][-2:] != '16' or bool(observed and observed['providerId'] == 'claude')}
        row = {'id': arm + '/' + case['id'], 'arm': arm, 'caseId': case['id'], 'status': 'PASS' if all(checks.values()) else 'FAIL',
               'expected': case, 'proposal': raw_output, 'projection':output if grouping or operation_provider else None,
               'bound': observed, 'checks': checks, 'usage': reply['usage'],
               'error': reply.get('error'), 'actualProviderRequests': len(result['packets'])}
        row['observed']={'proposal':raw_output,'bound':observed,'checks':checks,'usage':reply['usage']}
        rows.append(row)
        if journal:journal.emit('check_observed','verification',row['status'],owner='Independent original validation comparator',
            where=location(publish),expected=case,observed={'proposal':output,'checks':checks})
        publish(rows,'RUNNING')
        write(folder / 'row.json', row)
        write(OUT / ('checkpoint-%02d.json' % len(rows)), {'scope': plan['scope'], 'status': 'RUNNING',
                                                        'count': len(rows), 'rows': rows})
        print(row['id'], row['status'], flush=True)
for item in reference['files']:
    if audit.digest(FROZEN / item['path']) != item['sha256']: raise ValueError('Frozen runtime changed during study')
if feedback and any(audit.digest(sdk / name)!=expected for name,expected in plan['sdkHashes'].items()):
    raise ValueError('Diagnostic SDK changed during study')
write(OUT / 'results.json', {'scope': plan['scope'], 'status': 'DIAGNOSTIC_COMPLETE', 'count': len(rows), 'rows': rows})
publish(rows,'PASS' if all(row['status']=='PASS' for row in rows) else 'FAIL')
