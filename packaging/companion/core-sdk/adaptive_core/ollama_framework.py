"""Native Ollama transport for the framework's OpenAI model adapter.

Ollama's /v1 API does not expose num_ctx. Translate only the HTTP envelope so
the selected native context/thinking limits remain effective. Agent iteration,
JSON parsing, usage and schema-repair stay in Pydantic AI.
"""
import asyncio
import json
import time
import uuid
import httpx2
from .provider import OllamaProvider, ProviderError


class NativeOllamaTransport(httpx2.AsyncBaseTransport):
    def __init__(self, endpoint, timeout):
        self.provider = OllamaProvider(endpoint, timeout=timeout)

    async def handle_async_request(self, request):
        if request.method != 'POST' or request.url.path != '/v1/chat/completions':
            return httpx2.Response(400, json={'error': {'message': 'Unsupported inference operation'}})
        value = json.loads(request.content)
        options = dict(value.get('options', {}))
        for source, target in [('temperature', 'temperature'), ('max_tokens', 'num_predict'), ('top_p', 'top_p')]:
            if source in value:
                options[target] = value[source]
        packet = {'model': value['model'], 'messages': value['messages'], 'stream': False,
                  'think': value.get('think', False), 'options': options}
        output = value.get('response_format', {})
        if output.get('type') == 'json_schema':
            packet['format'] = output['json_schema']['schema']
        try:
            result = await asyncio.to_thread(self.provider.request, '/api/chat', packet)
        except ProviderError:
            return httpx2.Response(503, json={'error': {'message': 'Local model unavailable'}})
        if not result.get('done') or result.get('done_reason') == 'length':
            return httpx2.Response(422, json={'error': {'message': 'Incomplete model response'}})
        prompt, completion = result.get('prompt_eval_count', 0), result.get('eval_count', 0)
        return httpx2.Response(200, json={'id': uuid.uuid4().hex, 'object': 'chat.completion',
            'created': int(time.time()), 'model': value['model'],
            'choices': [{'index': 0, 'message': {'role': 'assistant', 'content': result['message'].get('content', '')},
                         'finish_reason': 'length' if result.get('done_reason') == 'length' else 'stop'}],
            'usage': {'prompt_tokens': prompt, 'completion_tokens': completion, 'total_tokens': prompt + completion}})
