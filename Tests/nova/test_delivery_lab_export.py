import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('lab_export', Path(__file__).with_name('export-delivery-lab.py'))
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)


def fixture():
    return {'syntheticOnly': True, 'oracleVersion': 'clipboard-task-outcomes/2',
            'hashes': {'QuickPrompt.Api.dll': 'a' * 64}, 'runs': [{
                'runId': 'fixture-run', 'taskEvidence': {'kind': 'natural-request', 'scenario': 'prepare'},
                'semantic': {'intentions': ['COPY_PROMPT']}, 'status': 'COMPLETED',
                'observation': {'templateText': 'Explica <tema> sin inventar datos.', 'draftChanged': False,
                                'compiledReady': True, 'compiledText': 'Explica energía hidráulica sin inventar datos.',
                                'values': {'tema': 'energía hidráulica'}, 'clipboard': None, 'responseComplete': False}}]}


class ExportTests(unittest.TestCase):
    def test_legitimate_prepare_requires_exact_observed_artifact(self):
        _, trace = module.export(fixture(), 'fixture:model')
        self.assertEqual(['COPY_READY'], trace['events'][-1]['verified_outcomes'])
        self.assertEqual(['COPY_PROMPT'], [e['effect'] for e in trace['events'] if e['kind'] == 'receipt'])

    def test_false_completed_keeps_unauthorized_definition_change(self):
        report = fixture(); report['runs'][0]['observation'].update(templateText='Changed definition', draftChanged=True)
        _, trace = module.export(report, 'fixture:model')
        self.assertEqual('COMPLETED', trace['events'][-1]['status'])
        self.assertEqual([], trace['events'][-1]['verified_outcomes'])
        self.assertIn('CHANGE_DEFINITION', [e.get('effect') for e in trace['events']])

    def test_proposed_calls_do_not_create_receipts(self):
        report = fixture(); report['runs'][0]['observation']['compiledReady'] = False
        report['runs'][0]['decisions'] = [{'toolName': 'ui_copy_prompt'}]
        _, trace = module.export(report, 'fixture:model')
        self.assertFalse(any(e['kind'] == 'receipt' for e in trace['events']))

    def test_private_or_weak_harness_is_rejected(self):
        for field, value in [('syntheticOnly', False), ('oracleVersion', 'old')]:
            report = fixture(); report[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                module.export(report, 'fixture:model')

    def test_missing_correlation_is_not_reconstructed_from_text(self):
        report = fixture(); report['runs'][0]['taskEvidence'] = {'kind': 'natural-request', 'request': 'prepare'}
        with self.assertRaises(ValueError):
            module.export(report, 'fixture:model')


if __name__ == '__main__':
    unittest.main()
