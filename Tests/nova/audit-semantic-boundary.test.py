import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('boundary_audit', Path(__file__).with_name('audit-semantic-boundary.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class BoundaryAuditTests(unittest.TestCase):
    names = {'PROPOSE_ADAPTATION': 'PROPOSE_ADAPTATION', 'COPY_PROMPT': 'PREPARE_COPY_PREVIEW',
             'COPY_TO_CLIPBOARD': 'WRITE_CLIPBOARD_NOW', 'EXECUTE_LOCAL': 'EXECUTE_LOCAL'}

    def check_effects(self, operations):
        return audit.compare_effects({'effects': [{'operation': op} for op in operations]},
              ['PROPOSE_ADAPTATION', 'COPY_PROMPT'], ['COPY_TO_CLIPBOARD', 'EXECUTE_LOCAL'], self.names)

    def test_omitted_adaptation_fails_even_with_valid_preview(self):
        result = self.check_effects(['PREPARE_COPY_PREVIEW'])
        self.assertFalse(result['pass'])
        self.assertEqual(result['missing'], ['PROPOSE_ADAPTATION'])

    def test_unrequested_write_cannot_be_compensated_by_completeness(self):
        result = self.check_effects(['PROPOSE_ADAPTATION', 'PREPARE_COPY_PREVIEW', 'WRITE_CLIPBOARD_NOW'])
        self.assertFalse(result['pass'])
        self.assertEqual(result['extra'], ['WRITE_CLIPBOARD_NOW'])
        self.assertEqual(result['forbidden'], ['WRITE_CLIPBOARD_NOW'])

    def test_both_requested_goals_pass_without_write(self):
        self.assertTrue(self.check_effects(['PREPARE_COPY_PREVIEW', 'PROPOSE_ADAPTATION'])['pass'])

    def test_qa_assisted_profile_cannot_claim_production_evidence(self):
        with self.assertRaisesRegex(ValueError, 'Unassisted'):
            audit.validate_identity({'profile': {'semanticCore': 'LAB_ASSISTED'}}, {})

    def test_foreign_runtime_cannot_be_compared(self):
        with self.assertRaisesRegex(ValueError, 'Foreign runtime'):
            audit.validate_identity({'profile': {'semanticCore': 'ADAPTIVE_CORE'}, 'runtimeHashes': {'x': 'other'}},
                                    {'runtimeHashes': {'x': 'pinned'}})

    def test_model_substitution_cannot_be_hidden(self):
        with self.assertRaisesRegex(ValueError, 'Qwen identity'):
            audit.validate_identity({'profile': {'semanticCore': 'ADAPTIVE_CORE'}, 'runtimeHashes': {}, 'model': 'other'},
                                    {'runtimeHashes': {}})

    def test_real_frozen_replay_separates_schema_from_intent(self):
        path = Path(__file__).resolve().parents[2] / 'docs/evidence/closure-lab-20261021/semantic-boundary-audit-20261022/audit.json'
        if not path.exists():
            self.skipTest('Executed reproduction not available on this machine')
        result = audit.read(path)
        self.assertEqual(len(result['rows']), 4)
        self.assertEqual(sum(r['observed']['schemaPass'] for r in result['rows']), 4)
        self.assertEqual(sum(r['status'] == 'PASS' for r in result['rows']), 2)
        self.assertTrue(all(r['observed']['compiledReplay']['acceptedByHost'] for r in result['rows']))
        self.assertFalse(result['acceptanceComplete'])
        self.assertFalse(result['auditChecks']['historicalProviderHttpCaptured'])

    def test_serialized_real_http_capture_has_no_test_oracle(self):
        import hashlib
        import json
        folder = Path(__file__).resolve().parents[2] / 'docs/evidence/closure-lab-20261021/semantic-boundary-live-20261022'
        if not folder.exists():
            self.skipTest('Real public capture not present')
        recorded = audit.read(folder / 'audit.json')['liveCapture']
        http = audit.read(folder / 'actual-http-request-1.json')
        self.assertEqual(hashlib.sha256(http['bodyUtf8'].encode()).hexdigest(), http['bodySha256'])
        body = json.loads(http['bodyUtf8'])
        self.assertEqual(body, recorded['packets'][0]['packet'])
        self.assertEqual(body['messages'], recorded['request']['messages'])
        user = json.loads(next(m['content'] for m in body['messages'] if m['role'] == 'user'))
        self.assertFalse(set(user) & {'expected', 'required', 'forbidden', 'oracle'})
        self.assertEqual(recorded['reply']['usage']['requests'], 1)
        self.assertNotIn('thinking', recorded['reply'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
