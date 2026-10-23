"""Synthetic Lab evidence audit. Offline replay is NEVER fresh model evidence.

Consumes immutable goal, reports and exact correlated public fixture events.
Invokes the frozen .NET implementation, then the frozen Core/framework with a
stubbed provider. No network, private reasoning or domain/UI effects in replay.
"""
import argparse
import asyncio
import hashlib
import importlib.metadata
import json
from pathlib import Path
import subprocess
import sys
import os
from datetime import datetime, timezone
from uuid import uuid4
from unittest.mock import patch


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def write(path, value):
    with Path(path).open('x', encoding='utf-8') as file:
        json.dump(value, file, ensure_ascii=False, indent=2)


def compare_effects(output, required, forbidden, names):
    """Independent typed oracle, never sent to the model/framework."""
    actual = {x['operation'] for x in output['effects']}
    expected = {names[x] for x in required}
    denied = {names[x] for x in forbidden}
    return {'expected': sorted(expected), 'observed': sorted(actual),
            'missing': sorted(expected - actual), 'extra': sorted(actual - expected),
            'forbidden': sorted(actual & denied),
            'pass': actual == expected and not actual & denied}


def validate_identity(report, reference):
    if report.get('profile', {}).get('semanticCore') != 'ADAPTIVE_CORE':
        raise ValueError('Unassisted Adaptive Core report required')
    if report.get('runtimeHashes') != reference['runtimeHashes']:
        raise ValueError('Foreign runtime report; comparison refused')
    if report.get('model') != 'qwen3:8b':
        raise ValueError('Selected local Qwen identity required')


async def framework_replay(protocol, output):
    # NativeOllamaTransport still runs unchanged. Only its provider.request port
    # is replaced in this private diagnostic process, before any HTTP operation.
    from adaptive_core.inference import InferenceRequest, infer
    from adaptive_core.provider import OllamaProvider
    packets = []

    def provider_request(_self, path, packet):
        if path != '/api/chat':
            raise AssertionError('Unexpected provider route')
        packets.append(packet)
        return {'done': True, 'done_reason': 'stop', 'model': packet['model'],
                'message': {'role': 'assistant', 'content': json.dumps(output)},
                'prompt_eval_count': 0, 'eval_count': 0}

    with patch.object(OllamaProvider, 'request', provider_request):
        result = await infer(InferenceRequest.model_validate(protocol), 'http://127.0.0.1:11434')
    if result['status'] != 'proposed' or len(packets) != 1 or result['output'] != output:
        raise ValueError('Framework replay failed; no semantic conclusion permitted')
    return packets[0]


async def capture_live(protocol, output_directory, digest_expected, observation_sink=None):
    """One bounded real inference; tee at the existing provider port. No effects.

    Caller must hold the existing exclusive .NET evaluation lease. Do not run
    this from UI/user data; only the validated synthetic reproduction above.
    """
    import urllib.request
    from adaptive_core.inference import InferenceRequest, infer
    from adaptive_core.provider import OllamaProvider
    request = {**protocol, 'requestId': uuid4().hex}
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def identity():
        with opener.open('http://127.0.0.1:11434/api/tags', timeout=5) as response:
            models = json.load(response)['models']
        found = [m for m in models if m['name'] == request['model']]
        if len(found) != 1 or found[0]['digest'] != digest_expected:
            raise ValueError('Selected local model identity changed')
        return found[0]['digest']

    before = identity()
    packets, observations = [], []
    original = OllamaProvider.request

    def tee(provider, path, packet):
        # Public INPUT packet only, never result.message.thinking.
        if path != '/api/chat':
            raise ValueError('Unexpected inference route')
        receipt = {'observedAt': datetime.now(timezone.utc).isoformat(), 'path': path, 'packet': packet}
        packets.append(receipt)
        write(output_directory / ('actual-provider-request-'+str(len(packets))+'.json'), receipt)
        open_http = provider.http.open
        def capture_serialized(http_request, *a, **kw):
            body = http_request.data
            wire = {'observedAt': datetime.now(timezone.utc).isoformat(),
                    'url': http_request.full_url, 'bodyUtf8': body.decode('utf-8'),
                    'bodySha256': hashlib.sha256(body).hexdigest()}
            write(output_directory / ('actual-http-request-'+str(len(packets))+'.json'), wire)
            receipt['wireBodySha256'] = wire['bodySha256']
            return open_http(http_request, *a, **kw)
        with patch.object(provider.http, 'open', capture_serialized):
            return original(provider, path, packet)

    def observe(event):
        observations.append(event)
        if observation_sink is not None:observation_sink(event)
    with patch.object(OllamaProvider, 'request', tee):
        reply = await infer(InferenceRequest.model_validate(request), 'http://127.0.0.1:11434', observer=observe)
    after = identity()
    write(output_directory / 'actual-core-request.json', request)
    write(output_directory / 'actual-core-result.json', reply)
    write(output_directory / 'actual-core-events.json', observations)
    if before != after or not packets:
        raise ValueError('Live capture incomplete')
    return {'scope': 'FRESH_REAL_CORE_INFERENCE_NO_DOMAIN_OR_UI_EFFECTS',
            'request': request, 'reply': reply, 'packets': packets,
            'modelDigestBefore': before, 'modelDigestAfter': after, 'events': observations}


def run(args):
    os.environ.setdefault('PYDANTIC_AI_NO_BANNER', '1')
    from jsonschema import Draft202012Validator
    goal = read(args.goal)
    if goal.get('schema') != 'ai-delivery-goal/1' or not goal.get('captureInputs'):
        raise ValueError('Explicit public synthetic goal required')
    repo = Path(__file__).resolve().parents[2]
    frozen = Path(args.frozen).resolve()
    if not frozen.is_relative_to(repo / '.playwright/closure-lab-20261021'):
        raise ValueError('Owned frozen runtime required')
    reference = read(args.reference)
    manifest = read(repo / 'docs/evidence/closure-lab-20261021' / reference['candidate'] / 'manifest.json')
    if frozen.name != reference['candidate']:
        raise ValueError('Frozen candidate mismatch')
    for item in manifest['files']:
        path = (frozen / item['path']).resolve()
        if not path.is_relative_to(frozen) or digest(path) != item['sha256']:
            raise ValueError('Frozen artifact integrity mismatch')
    for name, expected in reference['runtimeHashes'].items():
        if digest(frozen / 'eval' / name) != expected:
            raise ValueError('Pinned runtime mismatch')
    dataset = read(args.dataset)
    if not dataset.get('syntheticOnly') or dataset.get('split') == 'holdout':
        raise ValueError('Only development/validation synthetic fixtures allowed')
    if goal['context'] != 'selected_report' or goal.get('inputs'):
        raise ValueError('This reproduction requires selected source and no case values')
    source = {'id': '30000000-0000-0000-0000-000000000001', 'revision': 1,
              'title': dataset['fixtures'][0][0], 'templateText': dataset['fixtures'][0][1]}
    records = []
    for specification in args.reports:
        path, case_id = specification.rsplit('#', 1)
        report = read(path)
        validate_identity(report, reference)
        rows = [r for r in report['rows'] if r['id'] == case_id]
        if len(rows) != 1 or rows[0]['message'] != goal['request'] or rows[0]['context'] != goal['context']:
            raise ValueError('Report does not match the exact mission fixture')
        row = rows[0]
        if len(row['attempts']) != 1 or 'contract' not in row['attempts'][0]:
            raise ValueError('One public captured proposal required')
        records.append({'path': path, 'reportSha256': digest(path), 'row': row, 'report': report,
                        'output': row['attempts'][0]['contract'], 'events': []})
    if len({r['report']['modelDigest'] for r in records}) != 1:
        raise ValueError('Model digest changed')
    by_run = {r['row']['runId']: r for r in records}
    if len(by_run) != len(records):
        raise ValueError('Duplicate model receipt')
    with Path(args.journal).open(encoding='utf-8-sig') as journal:
        for line in journal:
            event = json.loads(line)
            record = by_run.get(event.get('runId'))
            if record and event.get('caseId') == record['row']['id']:
                record['events'].append(event)
    for r in records:
        inputs = [e for e in r['events'] if e.get('kind') == 'fixture_input']
        settings = [e for e in r['events'] if e.get('kind') == 'inference_started']
        if len(inputs) != 1 or len(settings) != 1:
            raise ValueError('Missing or ambiguous correlated input capture')
        r['messages'] = inputs[0]['inputs']['messages']
        r['settings'] = settings[0]['observed']
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=False)
    write(out / 'probe-input.json', {'syntheticOnly': True, 'context': goal['context'],
          'message': goal['request'], 'model': records[0]['report']['model'], 'source': source,
          'proposals': [{'id': r['row']['runId'], 'output': r['output']} for r in records]})
    command = subprocess.run(['dotnet', str(Path(args.probe).resolve()), str(out / 'probe-input.json'),
                              str(out / 'host-replay.json')], capture_output=True, text=True, timeout=30)
    (out / 'probe.log').write_text(command.stdout + command.stderr, encoding='utf-8')
    if command.returncode:
        raise ValueError('Frozen host replay failed; inspect probe.log')
    host = read(out / 'host-replay.json')
    for name, actual in host['runtimeHashes'].items():
        if actual != reference['runtimeHashes'][name]:
            raise ValueError('Probe loaded different domain binaries')
    packet = host['packet']
    if any(packet['messages'] != r['messages'] for r in records):
        raise ValueError('Reconstructed messages differ from actual capture; cannot infer contract equivalence')
    if any(r['settings']['settings'] != packet['options'] or r['settings']['thinking'] != packet['think'] for r in records):
        raise ValueError('Reconstructed settings differ from actual capture')
    sys.path.insert(0, str(frozen / 'sdk'))
    schema = packet['format']
    validator = Draft202012Validator(schema)
    names = {x['operation']: x['modelName'] for x in host['operations']}
    live = None
    if args.capture_live or args.reuse_live_capture:
        if args.capture_live:
            if os.environ.get('NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE') != '1':
                raise ValueError('Live capture requires the evaluation lease wrapper')
            live = asyncio.run(capture_live(host['protocol'], out, records[0]['report']['modelDigest']))
        else:
            origin = Path(args.reuse_live_capture).resolve()
            if not origin.is_relative_to(repo / 'docs/evidence/closure-lab-20261021'):
                raise ValueError('Owned public capture required')
            live = read(origin)['liveCapture']
            if live['modelDigestBefore'] != records[0]['report']['modelDigest'] or live['modelDigestAfter'] != live['modelDigestBefore']:
                raise ValueError('Foreign capture identity')
            if {**live['request'], 'requestId': host['protocol']['requestId']} != host['protocol']:
                raise ValueError('Capture protocol does not match this reproduction')
            for index, sent in enumerate(live['packets'], 1):
                http = read(origin.parent / ('actual-http-request-'+str(index)+'.json'))
                if json.loads(http['bodyUtf8']) != sent['packet'] or hashlib.sha256(http['bodyUtf8'].encode()).hexdigest() != sent['wireBodySha256']:
                    raise ValueError('Serialized provider receipt mismatch')
            live = {**live, 'provenance': {'path': str(origin), 'sha256': digest(origin), 'newInference': False}}
            write(out / 'actual-core-result.json', live['reply'])
        if live['reply']['status'] != 'proposed':
            raise ValueError('Real inference failed; preserve actual-core-result.json')
        fresh_input = read(out / 'probe-input.json')
        fresh_input['proposals'] = [{'id': live['request']['requestId'], 'output': live['reply']['output']}]
        write(out / 'live-probe-input.json', fresh_input)
        fresh_replay = subprocess.run(['dotnet', str(Path(args.probe).resolve()), str(out / 'live-probe-input.json'),
                                      str(out / 'live-host-replay.json')], capture_output=True, text=True, timeout=30)
        if fresh_replay.returncode:
            raise ValueError('Live output host replay failed')
        fresh_host = read(out / 'live-host-replay.json')
        if fresh_host['packet'] != packet:
            raise ValueError('Live replay packet mismatch')
        host['rows'].extend(fresh_host['rows'])
        mapped_events = [{**e, 'campaignId': out.name, 'runId': live['request']['requestId'], 'caseId': live['request']['requestId'],
                          'owner': 'Adaptive Core / real provider capture'} for e in live['events']]
        records.append({'path': str(out / 'actual-core-result.json'), 'reportSha256': digest(out / 'actual-core-result.json'),
                        'row': {'runId': live['request']['requestId']}, 'events': mapped_events,
                        'output': live['reply']['output'], 'fresh': True})
    result_rows = []
    for i, (r, replay) in enumerate(zip(records, host['rows'])):
        native = asyncio.run(framework_replay(host['protocol'], r['output']))
        write(out / f'native-replay-{i+1}.json', native)
        schema_errors = [e.message for e in validator.iter_errors(r['output'])]
        effect_check = compare_effects(r['output'], goal['required'], goal['forbidden'], names)
        transport_check = {'messagesPreserved': native['messages'] == packet['messages'],
                           'optionsPreserved': native['options'] == packet['options'],
                           'thinkingPreserved': native['think'] == packet['think'],
                           'schemaObjectEquivalent': native['format'] == schema,
                           'schemaEquivalentExceptFrameworkTitle': {k:v for k,v in native['format'].items() if k != 'title'} == {k:v for k,v in schema.items() if k != 'title'},
                           'nativeSchemaAcceptsOutput': Draft202012Validator(native['format']).is_valid(r['output'])}
        observed = {'rawProposal': r['output'], 'compiledReplay': replay,
                    'schemaPass': not schema_errors, 'schemaErrors': schema_errors,
                    'semanticEffects': effect_check, 'transportReplay': transport_check,
                    'fieldMatrix': [
                        {'field': 'effects', 'expected': effect_check['expected'], 'observed': effect_check['observed'], 'pass': effect_check['pass']},
                        {'field': 'source', 'expected': 'OPEN: '+source['title'], 'observed': r['output'].get('source'), 'pass': r['output'].get('source') == 'OPEN: '+source['title']},
                        {'field': 'values', 'expected': {}, 'observed': r['output'].get('values', {}), 'pass': r['output'].get('values', {}) == {}},
                        {'field': 'supportingPassage', 'expected': 0, 'observed': [x['supportingPassage'] for x in r['output']['effects']], 'pass': all(x['supportingPassage'] == 0 for x in r['output']['effects'])},
                        {'field': 'providerId', 'expected': None, 'observed': r['output'].get('providerId'), 'compiled': (replay.get('plan') or {}).get('providerId'), 'pass': r['output'].get('providerId') is None, 'impact': 'IRRELEVANT_TO_REQUESTED_EFFECTS_CLEARED_BY_HOST'}],
                    'fullPacket': packet, 'protocol': host['protocol']}
        if r.get('fresh'):
            observed['actualProviderRequests'] = live['packets']
            observed['actualProtocolRequest'] = live['request']
            observed['captureProvenance'] = live.get('provenance', {'newInference': True})
        status = 'PASS' if effect_check['pass'] and not schema_errors and replay['acceptedByHost'] else 'FAIL'
        comparisons = [{'field': 'JSON.schema', 'expected': True, 'observed': not schema_errors, 'status': 'PASS' if not schema_errors else 'FAIL'},
                       {'field': 'host.accepted', 'expected': True, 'observed': replay['acceptedByHost'], 'status': 'PASS' if replay['acceptedByHost'] else 'FAIL'}]
        comparisons += [{'field': x['field'], 'expected': x['expected'], 'observed': x['observed'], 'status': 'PASS' if x['pass'] else 'FAIL'} for x in observed['fieldMatrix']]
        result_rows.append({'id': r['row']['runId'], 'title': ('Captura HTTP real · Qwen' if r.get('fresh') else Path(r['path']).parent.name)+' · esquema / objetivos',
            'status': status, 'inputs': {'request': goal['request'], 'source': source},
            'expected': {'required': goal['required'], 'forbidden': goal['forbidden'], 'outcomes': goal['outcomes']},
            'observed': observed, 'comparisons': comparisons, 'events': r['events'], 'runId': r['row']['runId'],
            'location': next((e['location'] for e in r['events'] if e.get('kind') == 'inference_finished'), None),
            'scope': 'FRESH_REAL_PROVIDER_INFERENCE_AND_HOST_REPLAY' if r.get('fresh') else 'RECORDED_MODEL_RESPONSE_AND_OFFLINE_FROZEN_REPLAY',
            'provenance': {'path': r['path'], 'sha256': r['reportSha256']},
            'firstDivergence': None if effect_check['pass'] else 'PUBLIC_STRUCTURED_PROPOSAL_BEFORE_HOST_BIND'})
    summary = {'schema': 'ai-delivery-tests/1', 'campaignId': out.name, 'status': 'FAIL' if any(r['status'] == 'FAIL' for r in result_rows) else 'PASS',
        'count': len(result_rows), 'skipped': 0, 'acceptanceComplete': False,
        'model': records[0]['report']['model'], 'modelDigest': records[0]['report']['modelDigest'], 'profile': {'semanticCore': 'FROZEN_CONTRACT_BOUNDARY_AUDIT'},
        'mission': {'id': goal['id'], 'hash': goal['hash']}, 'runtimeHashes': host['runtimeHashes'],
        'auditChecks': {'actualMessagesMatchReconstruction': True, 'actualSettingsMatchReconstruction': True,
                        'sameModelDigest': True, 'frozenHostBinariesMatch': True,
                        'historicalProviderHttpCaptured': False, 'replayNetworkCalls': 0},
        'limits': ['Provider payloads are OFFLINE reconstructions, not historical HTTP captures.',
                   'No private reasoning inspected. No UI/domain effect is executed by this audit.',
                   'Schema validity is not semantic correctness; an audit that finds the fault does not certify the app.'],
        'dependencies': {n: importlib.metadata.version(n) for n in ['pydantic-ai-slim','pydantic','openai','httpx2','jsonschema']},
        'liveCapture': live, 'rows': result_rows}
    write(out / 'audit.json', summary)
    print(json.dumps({'report': str(out / 'audit.json'), 'status': summary['status'],
                      'schemaPassed': sum(r['observed']['schemaPass'] for r in result_rows),
                      'semanticPassed': sum(r['status'] == 'PASS' for r in result_rows), 'count': len(result_rows)}))
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    for name in ['goal','reference','frozen','dataset','probe','journal','output']:
        parser.add_argument('--'+name, required=True)
    parser.add_argument('--reports', nargs='+', required=True, help='Synthetic report path#case-id')
    parser.add_argument('--capture-live', action='store_true')
    parser.add_argument('--reuse-live-capture')
    if '--capture-live' in sys.argv and '--reuse-live-capture' in sys.argv:
        parser.error('Choose new inference or preserved capture, never both')
    run(parser.parse_args())
