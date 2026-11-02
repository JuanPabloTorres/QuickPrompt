"""Bounded local Docker Model Runner transport; configuration is host authority.

The framework retains parsing, schema validation and its shared repair budget.
This adapter never configures the runner, pulls models or executes proposed tools.
"""
import asyncio
import copy
import json
import re
import time
import uuid
from urllib.parse import unquote

import httpx2

from .inference import local_base_url


def validate_dmr_mapping(model_alias, physical_model):
    """Require an explicit logical identity and immutable OCI model reference."""
    if (not isinstance(model_alias, str) or not 1 <= len(model_alias) <= 120
            or any(ord(char) < 33 or ord(char) == 127 for char in model_alias)):
        raise ValueError('Invalid configured model alias')
    if (not isinstance(physical_model, str) or len(physical_model) > 240
            or '://' in physical_model
            or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._:/-]*@sha256:[0-9a-f]{64}', physical_model)):
        raise ValueError('DMR requires a configured immutable model reference')


class DmrContractError(ValueError):
    pass


def structural_schema(schema):
    """Relax only the native mask; the original Draft2020 validator remains final.

    Supported positive schema positions preserve F(x) => mask(x). Never walk
    literal enum/const data as schemas. Reject structures whose relaxation is
    ambiguous rather than silently narrowing valid output or changing decoding.
    """
    retained = {'type', 'required', 'enum', 'const', 'title', 'description', '$ref'}
    structural = {'properties', '$defs', 'items', 'additionalProperties', 'anyOf', 'allOf', 'oneOf'}
    relaxed = {'minLength', 'maxLength', 'pattern', 'minItems', 'maxItems', 'uniqueItems',
               'minProperties', 'maxProperties', 'minimum', 'maximum', 'exclusiveMinimum',
               'exclusiveMaximum', 'multipleOf', 'format', '$schema', '$comment', 'default', 'examples'}
    original_paths, mask_paths, references = set(), set(), []

    def walk(node, original_path=(), mask_path=(), depth=0):
        if depth > 64 or not isinstance(node, (dict, bool)):
            raise DmrContractError('Unsupported structural schema')
        original_paths.add(original_path)
        mask_paths.add(mask_path)
        if isinstance(node, bool):
            return node
        if set(node) - retained - structural - relaxed or 'oneOf' in node and 'anyOf' in node:
            raise DmrContractError('Unsupported structural schema')
        result = {key: copy.deepcopy(value) for key, value in node.items() if key in retained}
        if '$ref' in node:
            references.append(node['$ref'])
        for key in ('properties', '$defs'):
            if key in node:
                if not isinstance(node[key], dict):
                    raise DmrContractError('Unsupported schema map')
                result[key] = {name: walk(value, original_path + (key, name), mask_path + (key, name), depth + 1)
                               for name, value in node[key].items()}
        for key in ('items', 'additionalProperties'):
            if key in node:
                result[key] = walk(node[key], original_path + (key,), mask_path + (key,), depth + 1)
        for key in ('anyOf', 'allOf', 'oneOf'):
            if key in node:
                if not isinstance(node[key], list):
                    raise DmrContractError('Unsupported schema union')
                target = 'anyOf' if key == 'oneOf' else key
                result[target] = [walk(value, original_path + (key, str(index)), mask_path + (target, str(index)), depth + 1)
                                  for index, value in enumerate(node[key])]
        return result

    result = walk(schema)
    for reference in references:
        if not isinstance(reference, str) or not reference.startswith('#/'):
            raise DmrContractError('Unsupported structural reference')
        parts = unquote(reference[2:], errors='strict').split('/')
        if any(re.search(r'~(?![01])', part) for part in parts):
            raise DmrContractError('Unsupported structural reference')
        target = tuple(part.replace('~1', '/').replace('~0', '~') for part in parts)
        if target not in original_paths or target not in mask_paths:
            raise DmrContractError('Reference does not retain a schema position')
    return result


class NativeDmrTransport(httpx2.AsyncBaseTransport):
    maximum_bytes = 512000

    def __init__(self, endpoint, timeout, model_alias, physical_model):
        local_base_url(endpoint)  # Same loopback/no-credentials/no-path boundary.
        validate_dmr_mapping(model_alias, physical_model)
        self.endpoint = endpoint.rstrip('/')
        self.timeout = timeout
        self.model_alias = model_alias
        self.physical_model = physical_model
        self.model_id = 'sha256:' + physical_model.rsplit('@sha256:', 1)[1]
        self.failure_code = None  # Set only from validated transport metadata, never model text.
        self.http = httpx2.AsyncClient(
            timeout=timeout, trust_env=False, follow_redirects=False,
            transport=httpx2.AsyncHTTPTransport(retries=0, trust_env=False),
        )

    async def _json(self, method, path, **kwargs):
        async with self.http.stream(method, self.endpoint + path, **kwargs) as response:
            if response.status_code >= 500:
                raise ConnectionError('Local runner unavailable')
            if response.status_code != 200:
                raise DmrContractError('Local runner rejected the contract')
            body = bytearray()
            async for chunk in response.aiter_bytes():
                if len(body) + len(chunk) > self.maximum_bytes:
                    raise DmrContractError('Local runner reply exceeds its budget')
                body.extend(chunk)
            return json.loads(body)

    async def _configuration(self, options, thinking):
        configs = await self._json('GET', '/engines/_configure', params={'model': self.physical_model})
        if not isinstance(configs, list) or len(configs) != 1 or not isinstance(configs[0], dict):
            raise DmrContractError('Ambiguous runner configuration')
        entry = configs[0]
        config = entry.get('Config')
        if (entry.get('ModelID') != self.model_id or entry.get('Backend') != 'llama.cpp'
                or entry.get('Mode') != 'completion' or not isinstance(config, dict)):
            raise DmrContractError('Runner identity does not match configuration')
        context = config.get('context-size')
        reasoning = config.get('llamacpp', {}).get('reasoning-budget') if isinstance(config.get('llamacpp'), dict) else None
        if (type(context) is not int or context != options['num_ctx']
                or type(reasoning) is not int or reasoning not in ((-1,) if thinking else (0, -1))
                or config.get('runtime-flags') != ['--parallel', '1']):
            raise DmrContractError('Runner limits do not match the request')

    def _packet(self, value):
        if (not isinstance(value, dict) or value.get('model') != self.model_alias
                or value.get('stream') is not None and value.get('stream') is not False
                or value.get('tools') or value.get('tool_choice')):
            raise DmrContractError('Unexpected inference identity or streaming')
        options = value.get('options', {})
        thinking = value.get('think', False)
        output_limit = value.get('max_completion_tokens', value.get('max_tokens'))
        if (not isinstance(options, dict) or type(thinking) is not bool
                or type(options.get('num_ctx')) is not int or not 1024 <= options['num_ctx'] <= 32768
                or type(options.get('num_predict')) is not int or not 1 <= options['num_predict'] <= 6000
                or type(output_limit) is not int or output_limit != options['num_predict']
                or 'max_tokens' in value and value['max_tokens'] != output_limit):
            raise DmrContractError('Unexpected inference limits')
        packet = {key: value[key] for key in ('messages', 'temperature', 'response_format') if key in value}
        if 'response_format' in packet:
            output_format = copy.deepcopy(packet['response_format'])
            if output_format == {'type': 'json_object'}:
                # PromptedOutput supplies the schema in messages and keeps the
                # same host validator. JSON mode constrains syntax only; do not
                # convert it into a native schema mask or silently change modes.
                pass
            elif isinstance(output_format, dict) and output_format.get('type') == 'json_schema':
                output_format['json_schema']['schema'] = structural_schema(output_format['json_schema']['schema'])
            else:
                raise DmrContractError('Unsupported native output format')
            packet['response_format'] = output_format
        packet.update(model=self.physical_model, stream=False, max_tokens=output_limit,
                      chat_template_kwargs={'enable_thinking': thinking})
        for name in ('top_p', 'top_k', 'min_p', 'seed'):
            if name in options:
                packet[name] = options[name]
            elif name in value:
                packet[name] = value[name]
        return packet, options, thinking

    def _reply(self, value):
        choices = value.get('choices') if isinstance(value, dict) else None
        if not isinstance(choices, list) or len(choices) != 1 or not isinstance(choices[0], dict):
            raise DmrContractError('Incomplete model response')
        choice = choices[0]
        if choice.get('finish_reason') == 'length':
            self.failure_code = 'MODEL_OUTPUT_LIMIT'
            raise DmrContractError('Model output token limit reached')
        message = choice.get('message')
        if (choice.get('finish_reason') != 'stop' or not isinstance(message, dict)
                or message.get('role') != 'assistant' or not isinstance(message.get('content'), str)
                or message.get('tool_calls')):
            raise DmrContractError('Incomplete or unsupported model response')
        usage = value.get('usage')
        if (not isinstance(usage, dict)
                or any(type(usage.get(key)) is not int or usage[key] < 0
                       for key in ('prompt_tokens', 'completion_tokens', 'total_tokens'))
                or usage['total_tokens'] != usage['prompt_tokens'] + usage['completion_tokens']):
            raise DmrContractError('Missing model usage')
        # Do not publish DMR's filesystem-valued model name, reasoning or diagnostics.
        return {'id': uuid.uuid4().hex, 'object': 'chat.completion', 'created': int(time.time()),
                'model': self.model_alias,
                'choices': [{'index': 0, 'message': {'role': 'assistant', 'content': message['content']}, 'finish_reason': 'stop'}],
                'usage': {key: usage[key] for key in ('prompt_tokens', 'completion_tokens', 'total_tokens')}}

    async def handle_async_request(self, request):
        if request.method != 'POST' or request.url.path != '/v1/chat/completions':
            return httpx2.Response(400, json={'error': {'message': 'Unsupported inference operation'}})
        try:
            if len(request.content) > self.maximum_bytes:
                raise DmrContractError('Inference request exceeds its budget')
            packet, options, thinking = self._packet(json.loads(request.content))
            async with asyncio.timeout(self.timeout):
                await self._configuration(options, thinking)
                result = await self._json('POST', '/engines/v1/chat/completions', json=packet)
            return httpx2.Response(200, json=self._reply(result))
        except (httpx2.HTTPError, ConnectionError, TimeoutError):
            return httpx2.Response(503, json={'error': {'message': 'Local model unavailable'}})
        except (ValueError, KeyError, TypeError):
            return httpx2.Response(422, json={'error': {'message': 'Local inference contract rejected'}})

    async def aclose(self):
        await self.http.aclose()
