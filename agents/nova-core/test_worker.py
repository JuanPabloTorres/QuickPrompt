"""Cross-process protocol tests; use the configured Core inference environment."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[2]
SDK = ROOT.parent / 'Adaptive-AI-Core'

class WorkerTests(unittest.TestCase):
    def invoke(self, version='adaptive-inference/1', invalid=False):
        packets = []
        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                packets.append(json.loads(self.rfile.read(int(self.headers['Content-Length']))))
                content = '{"title":42}' if invalid else '{"title":"energía 🪴"}'
                body = json.dumps({'done': True, 'message': {'content': content}, 'eval_count': 6}).encode()
                self.send_response(200); self.send_header('Content-Type','application/json')
                self.send_header('Content-Length',str(len(body))); self.end_headers(); self.wfile.write(body)
            def log_message(self, *_): pass
        server=ThreadingHTTPServer(('127.0.0.1',0), Handler)
        thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
        request_id=uuid4().hex
        request={'version':version,'requestId':request_id,'model':'synthetic:model','mode':'structured',
            'messages':[{'role':'user','content':'Return the synthetic title.'}],
            'outputSchema':{'type':'object','properties':{'title':{'type':'string'}},'required':['title'],'additionalProperties':False}}
        try:
            process=subprocess.run([sys.executable,str(Path(__file__).with_name('nova_worker.py')),'--sdk-root',str(SDK),
                '--endpoint',f'http://127.0.0.1:{server.server_port}'],input=json.dumps(request),text=True,encoding='utf-8',capture_output=True,timeout=30)
        finally:
            server.shutdown();server.server_close();thread.join(timeout=5)
        return process, json.loads(process.stdout), packets, request_id

    def test_real_framework_and_launcher_return_one_correlated_unicode_proposal(self):
        process,reply,packets,request_id=self.invoke()
        self.assertEqual(0,process.returncode,process.stderr)
        self.assertEqual(request_id,reply['requestId']); self.assertEqual('proposed',reply['status'])
        self.assertEqual('energía 🪴',reply['output']['title']); self.assertEqual(1,len(packets))
        self.assertNotIn('completed',reply.values())

    def test_old_quickprompt_protocol_is_retired_before_model_access(self):
        process,reply,packets,_=self.invoke(version='nova-core-plan-11')
        self.assertEqual(2,process.returncode); self.assertEqual('INVALID_PROTOCOL_REQUEST',reply['error']); self.assertEqual([],packets)

    def test_invalid_outputs_exhaust_exactly_two_framework_attempts(self):
        process,reply,packets,_=self.invoke(invalid=True)
        self.assertEqual(2,process.returncode); self.assertEqual('failed',reply['status']); self.assertEqual(2,len(packets))
        self.assertEqual(2,reply['usage']['requests']); self.assertIsNone(reply['output'])

    def test_launcher_has_no_domain_or_inference_implementation(self):
        source=Path(__file__).with_name('nova_worker.py').read_text(encoding='utf-8-sig')
        for removed in ['CREATE_DRAFT','COPY_PROMPT','WorkOrder','SourceChoices','AgentRuntime','OllamaProvider']:
            self.assertNotIn(removed,source)
        self.assertLess(len(source.splitlines()),20)

if __name__=='__main__': unittest.main()
