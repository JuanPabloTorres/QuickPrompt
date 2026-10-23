import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from ai_delivery_lab.visual_store import VisualStore

spec=importlib.util.spec_from_file_location('stage',Path(__file__).with_name('certification-stage.py'))
stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)


class ObservationRelayTests(unittest.TestCase):
    def test_actual_ids_times_and_run_correlation_survive_without_duplicate_append(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);feed=root/'owned';feed.mkdir();target=root/'visible.jsonl'
            reader=VisualStore({'sourceRoots':[str(root)],'journal':str(target),'artifacts':str(root/'artifacts')})
            event={'schema':'ai-delivery-event/1','id':'observed-event','campaignId':'app',
                   'runId':'actual-run','observedAt':'2026-10-22T13:00:00Z','phase':'input',
                   'kind':'human_request','status':'OBSERVED','inputs':{'apiKey':'never-public','value':'fixture'}}
            (feed/'app.jsonl').write_text(json.dumps(event)+'\n{',encoding='utf8')
            seen=set()
            self.assertEqual(1,stage.relay_development_events(reader,feed,target,'qa-campaign',seen))
            self.assertEqual(0,stage.relay_development_events(reader,feed,target,'qa-campaign',seen))
            rows=[json.loads(line) for line in target.read_text(encoding='utf8').splitlines()]
            self.assertEqual(1,len(rows));observed=rows[0]
            for key in ('id','runId','observedAt'):self.assertEqual(event[key],observed[key])
            self.assertEqual('qa-campaign',observed['campaignId']);self.assertEqual('app',observed['originCampaignId'])
            self.assertEqual('[REDACTED]',observed['inputs']['apiKey'])
            self.assertEqual('fixture',observed['inputs']['value'])

    def test_native_core_uses_existing_adapter_and_unknown_records_do_not_become_events(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);feed=root/'owned';feed.mkdir();target=root/'visible.jsonl'
            reader=VisualStore({'sourceRoots':[str(root)],'journal':str(target),'artifacts':str(root/'artifacts')})
            event={'schema':'adaptive-observation/1','id':'core-event','campaignId':'core',
                   'requestId':'request','runId':'run','kind':'model_started','phase':'inference',
                   'status':'RUNNING','observedAt':'2026-10-22T13:01:00Z'}
            (feed/'core.jsonl').write_text(json.dumps(event)+'\n'+json.dumps({'schema':'unknown','private':'not an event'})+'\n',encoding='utf8')
            self.assertEqual(1,stage.relay_development_events(reader,feed,target,'qa-campaign',set()))
            observed=json.loads(target.read_text(encoding='utf8'))
            self.assertEqual('core_model_started',observed['kind']);self.assertEqual('core-event',observed['id'])
            self.assertEqual('Adaptive Core',observed['owner']);self.assertNotIn('private',observed)

    def test_absent_feed_does_not_invent_activity(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);target=root/'visible.jsonl'
            reader=VisualStore({'sourceRoots':[str(root)],'journal':str(target),'artifacts':str(root/'artifacts')})
            self.assertEqual(0,stage.relay_development_events(reader,root/'missing',target,'qa',set()))
            self.assertFalse(target.exists())

    def test_live_counts_preserve_partial_status_and_only_exact_scenario_runs(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);feed=root/'lab-feed';feed.mkdir();raw=root/'ui'/'owned';raw.mkdir(parents=True)
            reader=VisualStore({'sourceRoots':[str(root)],'journal':str(feed/'app.jsonl'),'artifacts':str(root/'artifacts')})
            receipt={'status':'RUNNING','checks':[{'name':'real-qwen-prepare','status':'PASS'}],
                     'runs':[{'runId':'matched','taskEvidence':{'scenario':'prepare'}},
                             {'runId':'foreign','taskEvidence':{'scenario':'actual'}}]}
            (raw/'results.json').write_text(json.dumps(receipt),encoding='utf8')
            events=[{'schema':'ai-delivery-event/1','id':i,'runId':i,'observedAt':'2026-10-22T13:00:00Z'} for i in ('matched','foreign')]
            (feed/'app.jsonl').write_text(''.join(json.dumps(e)+'\n' for e in events),encoding='utf8')
            report=root/'browser.json';stage.publish_live_ui(report,reader,feed,'campaign')
            live=json.loads((root/'live.json').read_text(encoding='utf8'))
            self.assertEqual('RUNNING',live['status']);self.assertFalse(live['acceptanceComplete'])
            self.assertEqual(1,live['count']);self.assertEqual(['matched'],[e['id'] for e in live['rows'][0]['events']])
            self.assertFalse(report.exists())
            original={'status':'FAIL','count':1,'rows':[{'id':'final','status':'FAIL'}]}
            report.write_text(json.dumps(original),encoding='utf8');stage.publish_live_ui(report,reader,feed,'campaign')
            self.assertEqual(report.read_bytes(),(root/'live.json').read_bytes())


if __name__=='__main__':unittest.main()
