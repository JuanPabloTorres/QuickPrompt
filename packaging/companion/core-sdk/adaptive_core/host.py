"""One bounded UTF-8 request/reply over stdio. The host owns domain effects."""
import argparse
import asyncio
import json
import os
import sys
from .inference import PROTOCOL, InferenceRequest, infer

def main():
    os.environ.setdefault('PYDANTIC_AI_NO_BANNER', '1')
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser()
    parser.add_argument('--endpoint', required=True)
    parser.add_argument('--provider', default='ollama')
    parser.add_argument('--model-alias')
    parser.add_argument('--physical-model')
    parser.add_argument('--events')
    parser.add_argument('--campaign-id',default='application')
    parser.add_argument('--case-id')
    parser.add_argument('--public-structured-output',action='store_true')
    args = parser.parse_args()
    request_id = None
    try:
        raw = sys.stdin.buffer.read(128001)
        if len(raw) > 128000:
            raise ValueError('Request too large')
        request = InferenceRequest.model_validate(json.loads(raw))
        request_id = request.requestId
        from .observation import JsonlObservationSink
        observer=JsonlObservationSink(args.events,args.campaign_id,args.case_id or request.requestId) if args.events else None
        reply = asyncio.run(infer(request, args.endpoint, provider=args.provider,
            model_alias=args.model_alias, physical_model=args.physical_model, observer=observer))
        if observer and args.public_structured_output and request.mode!='text' and reply['status']=='proposed':
            from .observation import ObservationPort
            ObservationPort(observer,request.requestId).emit('proposal_observed','inference','OBSERVED',observed={'output':reply['output']})
    except ImportError:
        reply = {'version': PROTOCOL, 'requestId': request_id, 'status': 'failed', 'output': None,
                 'usage': {'requests': 0, 'inputTokens': 0, 'outputTokens': 0}, 'error': 'DEPENDENCY_MISSING'}
    except Exception:
        reply = {'version': PROTOCOL, 'requestId': request_id, 'status': 'failed', 'output': None,
                 'usage': {'requests': 0, 'inputTokens': 0, 'outputTokens': 0}, 'error': 'INVALID_PROTOCOL_REQUEST'}
    print(json.dumps(reply, ensure_ascii=False))
    return 0 if reply['status'] == 'proposed' else 2

if __name__ == '__main__':
    raise SystemExit(main())
