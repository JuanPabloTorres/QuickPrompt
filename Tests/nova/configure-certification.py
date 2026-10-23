"""Configure QuickPrompt's full local battery in the existing generic Lab."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def load_composition(repo, reference_path, inputs_path):
    """One measured composition for commands, goal adapter and certification."""
    reference=read(reference_path)
    candidate=reference.get('candidate','')
    if reference.get('schema')!='ai-delivery-candidate/1' or not re.fullmatch(r'[a-z0-9-]{1,100}',candidate):
        raise ValueError('Explicit frozen candidate reference required')
    runtime=(repo/'.playwright/closure-lab-20261021'/candidate).resolve()
    original=Path(inputs_path) if inputs_path else repo/'docs/evidence/closure-lab-20261021'/candidate/'certification-inputs.json'
    inputs=read(original)
    if inputs.get('schema')!='quickprompt-certification-inputs/1' or inputs.get('candidate')!=candidate:
        raise ValueError('Certification inputs must identify the same frozen candidate')
    manifest_path=repo/'docs/evidence/closure-lab-20261021'/candidate/'manifest.json'
    manifest=read(manifest_path)
    if manifest.get('candidate')!=candidate or not manifest.get('files'):
        raise ValueError('Candidate manifest identity or files missing')
    files={}
    for entry in manifest['files']:
        name=entry['path'].replace('\\','/')
        file=(runtime/name).resolve()
        if name in files or not file.is_relative_to(runtime) or sha(file)!=entry['sha256'].upper():
            raise ValueError('Frozen candidate integrity mismatch')
        files[name]=entry['sha256'].upper()
    hashes=reference.get('runtimeHashes',{})
    if not hashes or any(files.get('eval/'+name)!=value.upper() for name,value in hashes.items()):
        raise ValueError('Candidate reference DLL hashes do not match the manifest')
    frontend=(repo/inputs['frontendRoot']).resolve()
    if not frontend.is_relative_to((repo/'.playwright').resolve()):
        raise ValueError('Frozen frontend inside the project required')
    entries=[]
    for file in sorted(frontend.rglob('*')):
        if file.is_symlink():raise ValueError('Frontend snapshot must not contain symbolic links')
        if file.is_file():entries.append([file.relative_to(frontend).as_posix(),sha(file)])
    entries.sort(key=lambda item:item[0])
    measured=hashlib.sha256(json.dumps(entries,ensure_ascii=False,separators=(',',':')).encode('utf-8')).hexdigest().upper()
    if not entries or measured!=inputs.get('webSha256','').upper():
        raise ValueError('Certification frontend identity mismatch')
    return reference,runtime,original,inputs,manifest,files


def main(args):
    repo=Path(__file__).resolve().parents[2]
    lab=Path(args.lab_root).resolve()
    source=Path(args.source_config) if args.source_config else lab/'artifacts/closure-20261021/config-missions.json'
    config=read(source)
    config=copy.deepcopy(config)
    config['candidateRef']=str(Path(args.candidate_reference or config['candidateRef']).resolve())
    reference,runtime,original,inputs,manifest,files=load_composition(repo,config['candidateRef'],args.inputs_template)
    config['flowHealthIdentity']={'runtimeHashes':reference['runtimeHashes'],
        'model':inputs['model'],'modelDigest':inputs['modelDigest'],'profile':inputs['profile'],
        'coreArtifacts':{'workerSha256':files['nova_worker.py'],
            'sdkHashes':{Path(name).name:value for name,value in files.items() if name.startswith('sdk/adaptive_core/') and name.endswith('.py')}}}
    frontend=str(repo/inputs['frontendRoot'])
    generated={'local-units','local-ui','local-copy','local-release-decision'}
    config['missions']=[m for m in config['missions'] if m['id'] not in generated]
    for mission in config['missions']:
        if mission['id']=='lab-tests':mission.setdefault('environment',{})['PYTHONPATH']=str(runtime/'sdk')
    adapter=config['goalAdapter']
    command=adapter['command']
    if '-FrozenCandidate' in command:
        command[command.index('-FrozenCandidate')+1]='{frozenCandidate}'
    else:command.extend(['-FrozenCandidate','{frozenCandidate}'])
    adapter['operations']=list(dict.fromkeys(adapter['operations']+[
        'REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY','REQUEST_DELETE','PAUSE','RESUME']))
    adapter['outcomes']=list(dict.fromkeys(adapter['outcomes']+['REVIEW','PAUSED','RESUMED']))
    adapter['providers']=['ollama','chatgpt','claude','gemini','copilot','perplexity']
    common=[str(args.python),'-X','utf8','-B',str(repo/'Tests/nova/certification-stage.py'),
        '--candidate','{frozenCandidate}','--lab-root',str(lab),'--core-root',str(Path(args.core_root).resolve()),
        '--node',str(Path(args.node).resolve()),'--events','{events}','--campaign','{campaign}']
    for name,title in [('units','Componentes · backend, React, Core y aceptación'),
                       ('ui','Agente · batería completa de UI real con Qwen'),('copy','Texto · Windows CRLF y archivos UTF-8 LF')]:
        report=str(repo/'docs/evidence/closure-lab-20261021'/'{campaign}'/(name+'.json'))
        config['missions'].append({'id':'local-'+name,'title':title,'kind':'tests','cwd':str(repo),
            'command':common+['--mode',name,'--report',report,'--frontend',frontend],
            'reportTemplate':report,'environment':{'ASPNETCORE_ENVIRONMENT':'Development'}})
    decision_report=str(repo/'docs/evidence/closure-lab-20261021'/'{campaign}'/'certification.json')
    config['missions'].append({'id':'local-release-decision','title':'Decisión final · sólo informes de esta batería',
        'kind':'tests','cwd':str(repo),'requiresCertification':True,
        'command':[str(Path(args.node).resolve()),str(repo/'Tests/nova/certify-lab.cjs'),
                   '--candidate','{frozenCandidate}','--inputs','{certificationInputs}','--report',decision_report],
        'reportTemplate':decision_report})
    def stage(identity,mission=None,requires=None):
        return {'id':identity,'missionId':mission or identity,'requires':requires or [],
                'expected':{'status':'PASS','skipped':0},'allRows':{'status':'PASS'}}
    stages=[stage('components','local-units'),stage('lab','lab-tests')]
    for split in ['development','validation','holdout']:
        item=stage(split,'core-'+split,[] if split=='development' else ['development'] if split=='validation' else ['development','validation'])
        item['expected']={'status':'PASS_SEMANTIC_CONTRACTS','frozenContractGate':'PASS','count':16}
        item['allRows']={'correct':True,'effectScopePass':True}
        stages.append(item)
    stages.extend([stage('browser','local-ui'),stage('copy','local-copy'),stage('decision','local-release-decision')])
    stages[-1]['expected']={'status':'PASS','count':11,'skipped':0}
    stages[-1]['bindInputs']={'template':str(original),
        'outputRoot':str(repo/'docs/evidence/closure-lab-20261021/certification-campaigns'),
        'reports':{'development':'development','validation':'validation','holdout':'holdout','browser':'browser','copyContracts':'copy'}}
    config['certificationSuite']={'schema':'ai-delivery-certification-suite/1',
        'title':'QuickPrompt local · todos los criterios obligatorios','releaseProfile':'local-v1',
        'decisionStage':'decision','currentReport':str(repo/'docs/evidence/closure-lab-20261021'/reference['candidate']/'certification.json'),
        'policy':'Cierre sólo con cada etapa y cada criterio obligatorio PASS; omisiones, bloqueos y evidencia ausente impiden certificar.',
        'stages':stages}
    matrix=json.loads((repo/'docs/nova/VERSION_CLOSURE_MATRIX.json').read_text(encoding='utf-8-sig'))
    config['certificationSuite']['requiredCriteria']=[r[0] for r in matrix['rows'] if r[0] not in ['EXT-02','EXT-03','VAR-04']]+['windows-unicode-crlf','utf8-lf-file']
    config['certificationSuite']['criteriaBindings']={
        'windows-unicode-crlf':{'stage':'copy','cases':['windows-standard-unicode-text-and-lines'],'allRows':{'status':'PASS'}},
        'utf8-lf-file':{'stage':'copy','cases':['downloaded-utf8-lf-exact-no-side-effects','downloaded-unsaved-draft-utf8-lf-no-side-effects'],'allRows':{'status':'PASS'}}}
    inventory_path=Path(args.component_inventory) if args.component_inventory else repo/'Tests/nova/datasets/local-certification-components-v1.json'
    inventory=json.loads(inventory_path.read_text(encoding='utf-8'))
    manifest_path=repo/'docs/evidence/closure-lab-20261021'/reference['candidate']/'manifest.json'
    if (inventory['candidate']!=reference['candidate'] or inventory['manifestSha256'].upper()!=sha(manifest_path)):
        raise ValueError('Review component test inventory for this exact candidate before binding acceptance')
    semantic_checks=[]
    for split in ['development','validation','holdout']:
        dataset=json.loads((repo/'Tests/nova/datasets/recovery-v1'/(split+'.json')).read_text(encoding='utf-8-sig'))
        semantic_checks.append({'stage':split,'cases':[r['id'] for r in dataset['scenarios']],
                                'allRows':{'correct':True,'effectScopePass':True}})
    config['certificationSuite']['criteriaBindings'].update({
        'REL-01':{'stage':'components','groups':inventory['backend']['groups'],'allRows':{'status':'PASS'}},
        'REL-02':{'stage':'components','cases':inventory['react']['cases'],'allRows':{'status':'PASS'}},
        'REL-05':{'allOf':semantic_checks}})
    config['certificationSuite']['executionFiles']=[str(p) for p in [
        *sorted((lab/'ai_delivery_lab').glob('*.py')),*[p for p in sorted((lab/'ai_delivery_lab/web').glob('*')) if p.is_file()],
        repo/'Tests/nova/certification-stage.py',repo/'Tests/nova/certify-lab.cjs',repo/'Tests/nova/artifact-hashes.cjs',
        repo/'Tests/nova/trx-results.py',repo/'Tests/nova/trx-results.test.py',
        repo/'Tests/nova/audit-semantic-boundary.py',repo/'Tests/nova/audit-semantic-boundary.test.py',
        repo/'Tests/nova/configure-certification.py',
        repo/'Tests/nova/configure-certification.test.py',
        repo/'Tests/nova/compilation-provenance.test.py',
        *[repo/'Tests/nova/datasets/recovery-v1'/name for name in ['development.json','validation.json','holdout.json','thresholds.json']],
        inventory_path,
        repo/'Tests/nova/clipboard-agent.cjs',repo/'Tests/nova/native-clipboard-probe.cjs',repo/'Tests/nova/verify-core-recovery.ps1',
        *sorted((lab/'tests').glob('test_*.py')),*sorted((repo/'Tests/nova').glob('*.test.cjs'))]]
    code=runtime/'source/backend'
    config['flowContracts']=[
        {'id':'semantic-outcomes','title':'Interpretación de objetivos','owner':'Adaptive Core / Qwen → contrato .NET',
         'purpose':'Recoger todos los objetivos originales y no añadir efectos ajenos.',
         'method':'Work order estructurado, binding y comparación independiente de efectos.',
         'resultFields':['intentCorrect','effectScopePass'],
         'files':[str(code/'QuickPrompt.Infrastructure/AdaptiveCoreInterpreter.cs'),str(code/'QuickPrompt.Infrastructure/QuickPromptCoreContract.cs')]},
        {'id':'source-resolution','title':'Origen e identidad','owner':'NOVA task planning / dominio .NET',
         'purpose':'Ligar el origen correcto a una identidad y revisión reales.',
         'method':'Referencias de contexto, resolución en SQLite y verificación de ID.',
         'resultFields':['targetCorrect'],'files':[str(code/'QuickPrompt.Application/TaskPlanning.cs'),str(code/'QuickPrompt.Application/TaskSourceResolver.cs')]},
        {'id':'literal-values','title':'Valores del caso','owner':'Binding y ámbito de datos .NET',
         'purpose':'Aplicar sólo valores literales a campos autorizados, sin convertir datos en órdenes.',
         'method':'Ámbito del origen y contraste esperado/obtenido.',
         'resultFields':['valuesCorrect'],'files':[str(code/'QuickPrompt.Infrastructure/AdaptiveCoreInterpreter.cs')]},
        {'id':'provider','title':'Destino vinculado','owner':'ProviderRegistry / dominio .NET',
         'purpose':'Preparar el proveedor pedido sin envío externo implícito.',
         'method':'Catálogo de proveedores, objetivo PREPARE_PROVIDER y destino final.',
         'resultFields':['providerCorrect'],'files':[str(code/'QuickPrompt.Application/Providers.cs')]}
    ]
    output=Path(args.output)
    with output.open('x',encoding='utf-8') as file:json.dump(config,file,ensure_ascii=False,indent=2)
    print(output.resolve())


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for name in ['lab-root','core-root','python','node','output']:parser.add_argument('--'+name,required=True)
    for name in ['source-config','candidate-reference','inputs-template','component-inventory']:parser.add_argument('--'+name)
    main(parser.parse_args())
