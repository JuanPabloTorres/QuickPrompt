"""TEST ONLY: exact public protocol replay at one inference boundary.

No intent recognition, permissions, business effects or production fallback.
All unmatched requests use the unchanged frozen worker. A work-order schema
mismatch fails closed, so an accidental real interpretation cannot masquerade
as the synthetic counterfactual. Receipts distinguish replay from inference.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False,
                                    separators=(',', ':')).encode()).hexdigest()


def run():
    config = json.loads(Path(os.environ['NOVA_COUNTERFACTUAL_CONFIG']).read_text(encoding='utf-8-sig'))
    if config['scope'] != 'SYNTHETIC_COUNTERFACTUAL_NOT_RELEASE':
        raise ValueError('Explicit synthetic scope required')
    raw = sys.stdin.buffer.read(128001)
    if len(raw) > 128000:
        raise ValueError('Protocol budget exceeded')
    request = json.loads(raw)
    identity = {key: value for key, value in request.items() if key != 'requestId'}
    expected = {key: value for key, value in config['protocol'].items() if key != 'requestId'}
    work_order = 'effects' in (request.get('outputSchema') or {}).get('properties', {})
    replay = digest(identity) == digest(expected)
    folder = Path(config['receipts'])
    folder.mkdir(parents=True, exist_ok=True)
    receipt = {'scope': config['scope'], 'requestId': request['requestId'],
               'request': request, 'requestSha256': digest(identity),
               'expectedSha256': digest(expected), 'replayed': replay,
               'realProviderCallsAtReplayedBoundary': 0 if replay else None}
    target = folder / (request['requestId'] + '.json')
    try:
        if work_order and not replay:
            raise ValueError('Unmatched work-order protocol; no model call permitted')
        if replay:
            # The compatibility codec requires one logical response. This is a
            # simulated usage count, explicitly NOT a measured provider call.
            reply = {'version': 'adaptive-inference/1', 'requestId': request['requestId'],
                     'status': 'proposed', 'output': config['proposal'],
                     'usage': {'requests': 1, 'inputTokens': 0, 'outputTokens': 0}, 'error': None}
            receipt['simulatedUsage'] = True
            receipt['reply'] = reply
            print(json.dumps(reply, ensure_ascii=False))
            return 0
        worker = Path(config['worker'])
        if hashlib.sha256(worker.read_bytes()).hexdigest() != config['workerSha256']:
            raise ValueError('Frozen delegate worker changed')
        result = subprocess.run([sys.executable, '-u', '-B', str(worker), *sys.argv[1:]],
                                input=raw, capture_output=True, timeout=190)
        receipt['delegateExitCode'] = result.returncode
        receipt['reply'] = json.loads(result.stdout)
        sys.stdout.buffer.write(result.stdout)
        sys.stderr.buffer.write(result.stderr)
        return result.returncode
    except Exception as error:
        receipt['error'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        with target.open('x', encoding='utf-8') as stream:
            json.dump(receipt, stream, ensure_ascii=False, indent=2)


if __name__ == '__main__':
    raise SystemExit(run())
