import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('trx_results', Path(__file__).with_name('trx-results.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class TrxIdentityTests(unittest.TestCase):
    def parse(self, results, total=None):
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / 'test.trx'
            file.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                + ''.join('<UnitTestResult ' + result + '/>' for result in results)
                + '</Results><ResultSummary><Counters total="' + str(len(results) if total is None else total)
                + '"/></ResultSummary></TestRun>', encoding='utf-8')
            return module.backend_results(file)

    def test_truncated_same_name_keeps_all_independent_executions(self):
        rows = self.parse(['testId="group" executionId="one" testName="same..." outcome="Passed"',
                           'testId="group" executionId="two" testName="same..." outcome="Failed"'])
        self.assertEqual(['backend/group/one', 'backend/group/two'], [r['id'] for r in rows])
        self.assertEqual(['PASS', 'FAIL'], [r['status'] for r in rows])
        self.assertTrue(all(r['bindingGroup'] == 'backend/group' and r['groupExecutionCount'] == 2 for r in rows))
        self.assertTrue(all(r['identity']['parameterValues'] == 'NOT_CAPTURED' for r in rows))

    def test_distinct_adapter_tests_with_same_display_name_are_distinct(self):
        rows = self.parse(['testId="a" executionId="one" testName="same..." outcome="Passed"',
                           'testId="b" executionId="two" testName="same..." outcome="NotExecuted"'])
        self.assertEqual(['backend/a', 'backend/b'], [r['id'] for r in rows])
        self.assertEqual('SKIP', rows[1]['status'])

    def test_duplicate_execution_is_rejected_instead_of_deduplicated(self):
        row = 'testId="group" executionId="one" testName="case" outcome="Passed"'
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            self.parse([row, row])

    def test_missing_execution_identity_and_missing_rows_cannot_pass(self):
        with self.assertRaisesRegex(ValueError, 'identities'):
            self.parse(['testId="group" testName="case" outcome="Passed"'])
        with self.assertRaisesRegex(ValueError, 'Every declared'):
            self.parse(['testId="group" executionId="one" testName="case" outcome="Passed"'], total=2)

    def test_unknown_outcome_is_failure(self):
        self.assertEqual('FAIL', self.parse(['testId="a" executionId="x" testName="case" outcome="Unknown"'])[0]['status'])

    def test_preserved_real_reports_keep_all_executions_and_same_groups(self):
        root = Path(__file__).resolve().parents[2] / 'docs/evidence/closure-lab-20261021'
        files = [root / name / 'backend.trx' for name in ['lab-live-743f209dec34', 'lab-live-c61984b93518']]
        if not all(file.exists() for file in files):
            self.skipTest('Local preserved TRX receipts unavailable; synthetic contract controls still apply')
        reports = [module.backend_results(file) for file in files]
        self.assertEqual([1117, 1117], [len(rows) for rows in reports])
        self.assertTrue(all(len({r['id'] for r in rows}) == 1117 for rows in reports))
        self.assertEqual({r['bindingGroup'] for r in reports[0]}, {r['bindingGroup'] for r in reports[1]})
        self.assertEqual(1110, len({r['bindingGroup'] for r in reports[0]}))


if __name__ == '__main__':
    unittest.main()
