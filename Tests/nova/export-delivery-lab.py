"""Export the existing SYNTHETIC prepare UI scenario to independent Lab evidence.

An offline adapter, not a browser runner or a new claim about today's build.
Uses exact versioned fixture expectations, not natural-language routing or the
agent's goal. Never treats a proposed UI tool call as an execution receipt.
"""
import argparse
import hashlib
import json
from pathlib import Path


def sha(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()


def export(report, model_id):
    if report.get('syntheticOnly') is not True or report.get('oracleVersion') != 'clipboard-task-outcomes/2':
        raise ValueError('Only the existing strict synthetic UI harness is supported')
    runs = [run for run in report['runs'] if run.get('taskEvidence', {}).get('scenario') == 'prepare']
    if len(runs) != 1:
        raise ValueError('One explicitly correlated prepare run required')
    run = runs[0]
    if run['taskEvidence']['kind'] != 'natural-request' or not run.get('runId'):
        raise ValueError('Missing exact task/run correlation')
    # Same fixture contract as clipboard-agent.cjs, independent of observed text
    # and the agent's interpretation. This is ONE known scenario, not a parser.
    expected_template = 'Explica <tema> sin inventar datos.'
    expected_compiled = 'Explica energía hidráulica sin inventar datos.'
    expected_values = {'tema': 'energía hidráulica'}
    oracle = {'schema': 'ai-delivery-oracle/1', 'required_suites': ['e2e'], 'cases': [{
        'id': 'ui-prepare', 'suite': 'e2e', 'allowed_effects': ['COPY_PROMPT'],
        'forbidden_effects': ['CHANGE_DEFINITION', 'COPY_TO_CLIPBOARD', 'EXECUTE_LOCAL'],
        'required_receipts': ['COPY_PROMPT'], 'required_outcomes': ['COPY_READY'],
        'expected_status': 'COMPLETED', 'must_preserve_artifact': True}]}
    state = run['observation']; events = []
    def event(phase, kind, **values):
        events.append({'seq': len(events) + 1, 'phase': phase, 'kind': kind, **values})
    # Semantic plan is an observation, NOT a raw inference response or receipt.
    event('planning', 'observation', planned_effects=run['semantic']['intentions'])
    if state['templateText'] != expected_template or state['draftChanged']:
        event('execution', 'receipt', effect='CHANGE_DEFINITION', ok=True, observer='qa')
    if state.get('clipboard') is not None:
        event('execution', 'receipt', effect='COPY_TO_CLIPBOARD', ok=True, observer='qa')
    if state.get('responseComplete'):
        event('execution', 'receipt', effect='EXECUTE_LOCAL', ok=True, observer='qa')
    ready = (state['compiledReady'] is True and state['compiledText'] == expected_compiled
             and state['values'] == expected_values and state['templateText'] == expected_template
             and state['draftChanged'] is False)
    if ready:
        event('execution', 'receipt', effect='COPY_PROMPT', ok=True, observer='qa')
    # Status alone never supplies COPY_READY: exact UI artifact comparisons do.
    event('verification', 'final', status=run['status'], observer='qa',
          verified_outcomes=['COPY_READY'] if ready else [], before_sha256=sha(expected_template),
          after_sha256=sha(state['templateText']))
    candidate_id = report['hashes']['QuickPrompt.Api.dll']
    trace = {'schema': 'ai-delivery-trace/1', 'candidate_id': candidate_id, 'model_id': model_id,
             'case_id': 'ui-prepare', 'run_id': run['runId'], 'events': events}
    return oracle, trace


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--model-id', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    if args.out.exists():
        parser.error('Preserve exported evidence; use a new directory')
    source = args.report.read_bytes()
    report = json.loads(source.decode('utf-8-sig'))
    oracle, trace = export(report, args.model_id)
    oracle_bytes = (json.dumps(oracle, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    digest = hashlib.sha256(oracle_bytes).hexdigest(); trace['oracle_sha256'] = digest
    manifest = {'schema': 'ai-delivery-manifest/1', 'candidate_id': trace['candidate_id'],
                'model_id': args.model_id, 'oracle_sha256': digest,
                'files_sha256': {args.report.name: hashlib.sha256(source).hexdigest()},
                'origin': 'Historical synthetic browser run; no new browser execution',
                'adapter_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                'harness_sha256': report['harnessSha256']}
    (args.out / 'runs').mkdir(parents=True)
    (args.out / 'oracle.json').write_bytes(oracle_bytes)
    for name, value in [('manifest.json', manifest), ('runs/ui-prepare.json', trace)]:
        (args.out / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Exported historical synthetic UI evidence; no model or UI actions executed')


if __name__ == '__main__':
    main()
