"""Import TRX executions without treating truncated display names as identity.

An adapter test ID may represent an entire parameterized theory. Execution IDs
identify its individual observations only within this run. Neither ID proves
that the adapter captured full parameter values or product source locations.
"""
from collections import Counter
import hashlib
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def backend_results(file, source_root=None):
    root = ET.parse(file).getroot()
    tests = root.findall('.//{*}UnitTestResult')
    counters = root.find('.//{*}Counters')
    if not tests or counters is None or int(counters.get('total', '-1')) != len(tests):
        raise ValueError('Every declared backend execution must be represented')
    if any(not t.get('testId') or not t.get('executionId') or not t.get('testName') for t in tests):
        raise ValueError('Adapter test, execution and display identities required')
    pairs = [(t.get('testId'), t.get('executionId')) for t in tests]
    if len(set(pairs)) != len(pairs) or len({p[1] for p in pairs}) != len(pairs):
        raise ValueError('Duplicate backend execution identity; preserve invalid source report')
    groups = Counter(t.get('testId') for t in tests)
    definitions = {t.get('id'): t.find('{*}TestMethod') for t in root.findall('.//{*}UnitTest')}
    receipt_hash = hashlib.sha256(Path(file).read_bytes()).hexdigest()
    locations = {}
    rows = []
    for test in tests:
        adapter, execution = test.get('testId'), test.get('executionId')
        group = 'backend/' + adapter
        method = definitions.get(adapter)
        row = {
            'id': group + ('/' + execution if groups[adapter] > 1 else ''),
            'title': test.get('testName'),
            'status': {'Passed': 'PASS', 'Failed': 'FAIL', 'NotExecuted': 'SKIP'}.get(test.get('outcome'), 'FAIL'),
            'expected': 'Passed', 'observed': test.get('outcome'),
            'bindingGroup': group, 'groupExecutionCount': groups[adapter],
            'identity': {'adapterTestId': adapter, 'executionId': execution,
                         'basis': 'adapter_test_and_run_execution' if groups[adapter] > 1 else 'adapter_test',
                         'executionIdScope': 'THIS_RUN', 'parameterValues': 'NOT_CAPTURED'},
            'testMethod': None if method is None else {'class': method.get('className'), 'name': method.get('name')},
            'execution': {'startedAt': test.get('startTime'), 'finishedAt': test.get('endTime'), 'duration': test.get('duration')},
            'sourceReceipt': {'path': str(Path(file).resolve()), 'sha256': receipt_hash, 'kind': 'test_adapter_result'},
        }
        if source_root is not None and method is not None:
            class_name, method_name = method.get('className'), method.get('name')
            symbol = (class_name, method_name)
            if symbol not in locations:
                matches = []
                for source in Path(source_root).rglob(class_name.rsplit('.', 1)[-1] + '.cs'):
                    pattern = re.compile(r'^\s*(?:public|internal|private|protected)\s+.*\b' + re.escape(method_name) + r'\s*\(')
                    lines = [(number, text) for number, text in enumerate(source.read_text(encoding='utf-8-sig').splitlines(), 1) if pattern.search(text)]
                    if len(lines) == 1:
                        matches.append({'file': str(source.resolve()), 'line': lines[0][0],
                            'symbol': class_name + '.' + method_name, 'kind': 'static_test_definition'})
                locations[symbol] = matches[0] if len(matches) == 1 else None
            if locations[symbol]:row['location'] = locations[symbol]
        rows.append(row)
    return rows
