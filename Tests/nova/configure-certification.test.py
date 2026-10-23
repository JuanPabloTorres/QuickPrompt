"""Composition regressions: reject mixed artifacts before any process starts."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('configure',Path(__file__).with_name('configure-certification.py'))
configure=importlib.util.module_from_spec(spec);spec.loader.exec_module(configure)


class CampaignCompositionTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.repo=Path(self.temp.name)/'repo';self.lab=Path(self.temp.name)/'lab'
        self.candidate='synthetic-candidate'
        self.runtime=self.repo/'.playwright/closure-lab-20261021'/self.candidate
        self.evidence=self.repo/'docs/evidence/closure-lab-20261021'/self.candidate
        self.frontend=self.repo/'.playwright/frozen-web'
        self.write(self.frontend/'index.html','<main>synthetic</main>')
        self.files={}
        for name in ['eval/QuickPrompt.Api.dll','eval/QuickPrompt.Evaluation.dll',
                     'eval/QuickPrompt.Infrastructure.dll','eval/QuickPrompt.Application.dll',
                     'eval/QuickPrompt.Domain.dll','nova_worker.py','sdk/adaptive_core/inference.py']:
            self.write(self.runtime/name,'synthetic '+name)
            self.files[name]=configure.sha(self.runtime/name)
        self.manifest=self.evidence/'manifest.json'
        self.write_json(self.manifest,{'candidate':self.candidate,'files':[
            {'path':name,'sha256':digest} for name,digest in self.files.items()]})
        self.reference=self.lab/'reference.json'
        self.write_json(self.reference,{'schema':'ai-delivery-candidate/1','candidate':self.candidate,
            'runtimeHashes':{Path(n).name:h for n,h in self.files.items() if n.endswith('.dll')}})
        entries=[['index.html',configure.sha(self.frontend/'index.html')]]
        web_hash=hashlib.sha256(json.dumps(entries,separators=(',',':')).encode()).hexdigest().upper()
        self.inputs=self.evidence/'inputs.json'
        self.write_json(self.inputs,{'schema':'quickprompt-certification-inputs/1','candidate':self.candidate,
            'frontendRoot':'.playwright/frozen-web','webSha256':web_hash,'profile':{},
            'model':'qwen3:8b','modelDigest':'a'*64})

    def write(self,path,value):
        path.parent.mkdir(parents=True,exist_ok=True);path.write_text(value,encoding='utf-8')

    def write_json(self,path,value):self.write(path,json.dumps(value))

    def load(self):return configure.load_composition(self.repo,self.reference,self.inputs)

    def test_measured_composition_accepts_a_template_outside_the_manifest_folder(self):
        external=self.lab/'templates/inputs.json'
        self.write(external,self.inputs.read_text())
        self.assertEqual(self.candidate,configure.load_composition(self.repo,self.reference,external)[0]['candidate'])

    def test_foreign_candidate_inputs_rejected(self):
        data=configure.read(self.inputs);data['candidate']='older-candidate';self.write_json(self.inputs,data)
        with self.assertRaisesRegex(ValueError,'same frozen candidate'):self.load()

    def test_foreign_reference_hash_rejected_even_when_manifest_itself_is_intact(self):
        data=configure.read(self.reference);data['runtimeHashes']['QuickPrompt.Api.dll']='f'*64
        self.write_json(self.reference,data)
        with self.assertRaisesRegex(ValueError,'DLL hashes'):self.load()

    def test_changed_frontend_rejected(self):
        self.write(self.frontend/'index.html','<main>another build</main>')
        with self.assertRaisesRegex(ValueError,'frontend identity'):self.load()

    def test_changed_runtime_rejected(self):
        self.write(self.runtime/'eval/QuickPrompt.Api.dll','different binary')
        with self.assertRaisesRegex(ValueError,'integrity'):self.load()

    def test_manifest_path_escape_rejected(self):
        data=configure.read(self.manifest)
        data['files'].append({'path':'../../../../outside.dll','sha256':'a'*64})
        self.write_json(self.manifest,data)
        with self.assertRaisesRegex(ValueError,'integrity'):self.load()

    def test_generator_replaces_existing_local_missions_and_pins_goals_to_same_candidate(self):
        source=self.lab/'source.json'
        self.write_json(source,{'candidateRef':str(self.reference),'goalAdapter':{
            'command':['pwsh','-File','run-lab-goal.ps1'],'operations':['OPEN_PROMPT'],'outcomes':['READ']},
            'missions':[{'id':'local-ui','command':['old-frontend']},{'id':'lab-tests','environment':{'PYTHONPATH':'old-sdk'}}]})
        inventory=self.evidence/'inventory.json'
        self.write_json(inventory,{'candidate':self.candidate,'manifestSha256':configure.sha(self.manifest),
            'backend':{'groups':[{'id':'backend/test','count':1}]},'react':{'cases':['react/test']}})
        self.write_json(self.repo/'docs/nova/VERSION_CLOSURE_MATRIX.json',{'rows':[['REL-01'],['REL-02'],['REL-05'],['VAR-04'],['EXT-02'],['EXT-03']]})
        for split in ['development','validation','holdout']:
            self.write_json(self.repo/'Tests/nova/datasets/recovery-v1'/(split+'.json'),{'scenarios':[{'id':split+'-test'}]})
        output=self.lab/'configured.json';before=source.read_bytes()
        args=argparse.Namespace(lab_root=str(self.lab),core_root=str(self.repo/'core'),python='python',node='node',
            source_config=str(source),candidate_reference=str(self.reference),inputs_template=str(self.inputs),
            component_inventory=str(inventory),output=str(output))
        old=configure.__file__;configure.__file__=str(self.repo/'Tests/nova/configure-certification.py')
        self.addCleanup(setattr,configure,'__file__',old)
        configure.main(args)
        result=configure.read(output)
        self.assertEqual(len(result['missions']),len({m['id'] for m in result['missions']}))
        for mission in result['missions']:
            if mission['id'] in ['local-ui','local-copy','local-units']:
                self.assertEqual(str(self.frontend),mission['command'][mission['command'].index('--frontend')+1])
        self.assertEqual(str(self.runtime/'sdk'),next(m for m in result['missions'] if m['id']=='lab-tests')['environment']['PYTHONPATH'])
        self.assertEqual(['-FrozenCandidate','{frozenCandidate}'],result['goalAdapter']['command'][-2:])
        decision=result['certificationSuite']['stages'][-1]
        self.assertEqual(str(self.inputs),decision['bindInputs']['template'])
        self.assertEqual(str(self.evidence/'certification.json'),result['certificationSuite']['currentReport'])
        self.assertIn('REQUEST_DELETE',result['goalAdapter']['operations'])
        self.assertIn('claude',result['goalAdapter']['providers'])
        self.assertEqual(before,source.read_bytes(),'Historical configuration must remain intact')
        with self.assertRaises(FileExistsError):configure.main(args)


if __name__=='__main__':unittest.main()
