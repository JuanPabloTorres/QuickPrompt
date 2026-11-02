"""Application-neutral inference. No host data access or effect execution."""
import ipaddress
import json
import copy
from typing import Literal
from urllib.parse import urlsplit
from pydantic import BaseModel, ConfigDict, Field, model_validator

PROTOCOL = 'adaptive-inference/1'

class Contract(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)

class Message(Contract):
    role: Literal['system', 'user', 'assistant']
    content: str = Field(max_length=64000)

class Limits(Contract):
    requests: int = Field(default=2, ge=1, le=2)
    timeoutSeconds: int = Field(default=90, ge=1, le=180)
    contextTokens: int = Field(default=12288, ge=1024, le=32768)
    outputTokens: int = Field(default=2600, ge=1, le=6000)
    temperature: float = Field(default=0, ge=0, le=2)
    thinking: bool = False
    topP: float | None = Field(default=None, ge=0, le=1)
    topK: int | None = Field(default=None, ge=1, le=1000)
    minP: float | None = Field(default=None, ge=0, le=1)
    seed: int | None = Field(default=None, ge=0, le=2147483647)

class InferenceRequest(Contract):
    version: Literal['adaptive-inference/1']
    requestId: str = Field(pattern=r'^[a-fA-F0-9-]{32,36}$')
    model: str = Field(min_length=1, max_length=120)
    mode: Literal['text', 'structured', 'tools']
    messages: list[Message] = Field(min_length=1, max_length=12)
    outputSchema: dict | None = None
    decoding: Literal['native', 'prompted'] = 'native'
    limits: Limits = Field(default_factory=Limits)

    @model_validator(mode='after')
    def check_contract(self):
        if (self.mode == 'text') != (self.outputSchema is None):
            raise ValueError('Structured requests require an output schema; text requests do not')
        if self.mode == 'text' and self.decoding != 'native':
            raise ValueError('Output decoding applies only to structured proposals')
        if self.outputSchema is not None and self.outputSchema.get('type') != 'object' and '$ref' not in self.outputSchema:
            raise ValueError('Structured output requires an explicit object schema')
        if len(json.dumps(self.model_dump(), ensure_ascii=False).encode('utf-8')) > 128000:
            raise ValueError('Request exceeds the byte budget')
        return self

def validate_schema(schema):
    """Schemas cannot authorize resolving external resources."""
    from jsonschema import Draft202012Validator
    def walk(node, depth=0):
        if depth > 64:
            raise ValueError('Schema nesting limit')
        if isinstance(node, dict):
            if '$ref' in node and not str(node['$ref']).startswith('#/'):
                raise ValueError('Only local schema references are permitted')
            for value in node.values():
                walk(value, depth + 1)
        elif isinstance(node, list):
            for value in node:
                walk(value, depth + 1)
    walk(schema)
    Draft202012Validator.check_schema(schema)
    return Draft202012Validator(schema)

def local_base_url(endpoint):
    address = urlsplit(endpoint)
    if (address.scheme != 'http' or not ipaddress.ip_address(address.hostname).is_loopback
            or address.username or address.password or address.path not in {'', '/'}
            or address.query or address.fragment):
        raise ValueError('This adapter requires an explicit loopback HTTP Ollama endpoint')
    return endpoint.rstrip('/') + '/v1'

async def infer(request: InferenceRequest, endpoint: str, *, provider='ollama', model_alias=None,
                physical_model=None, model_override=None, output_check=None, observer=None):
    """Framework result is a proposal, never host task completion."""
    from pydantic_ai import Agent, ModelRetry, NativeOutput, PromptedOutput, StructuredDict, UsageLimits
    from pydantic_ai.usage import RunUsage
    from pydantic_ai.models.ollama import OllamaModel
    from pydantic_ai.providers.ollama import OllamaProvider
    from pydantic_ai.messages import ModelRequest, ModelResponse, UserPromptPart, TextPart
    from openai import AsyncOpenAI
    import httpx2
    from .ollama_framework import NativeOllamaTransport
    from .observation import ObservationPort
    observations = ObservationPort(observer, request.requestId)
    usage = RunUsage()
    client = None
    transport = None
    try:
        base_url = local_base_url(endpoint)
        if provider not in {'ollama', 'docker-model-runner'}:
            raise ValueError('Unsupported configured provider')
        if provider == 'ollama' and (model_alias is not None or physical_model is not None):
            raise ValueError('Ollama does not accept a configured model mapping')
        if provider == 'docker-model-runner':
            from .dmr_framework import NativeDmrTransport, validate_dmr_mapping
            validate_dmr_mapping(model_alias, physical_model)
            if request.model != model_alias:
                raise ValueError('Request model does not match the configured alias')
        validator = validate_schema(request.outputSchema) if request.outputSchema is not None else None
        observations.emit('contract_validated','validation','PASS',observed={'model':request.model,'mode':request.mode})
        # Same parser, schema validator and shared retry budget. Prompted output
        # asks for a typed proposal without forcing token-by-token native grammar.
        output_mode = PromptedOutput if request.decoding == 'prompted' else NativeOutput
        # StructuredDict annotates its input with a title. Keep the authoritative
        # caller schema and its validator untouched, for either local provider.
        output = output_mode(StructuredDict(copy.deepcopy(request.outputSchema), name='host_result')) if validator else str
        instructions = '\n'.join(message.content for message in request.messages if message.role == 'system')
        # Adapt the wire conversation to the framework's native message history.
        # Serializing role envelopes as user text loses speaker identity and adds
        # a second encoding layer around the actual request/context.
        conversation = [message for message in request.messages if message.role != 'system']
        prompt = conversation.pop().content if conversation and conversation[-1].role == 'user' else None
        history = [ModelRequest(parts=[UserPromptPart(message.content)]) if message.role == 'user'
                   else ModelResponse(parts=[TextPart(message.content)]) for message in conversation]
        if model_override is None:
            transport = (NativeOllamaTransport(endpoint, request.limits.timeoutSeconds) if provider == 'ollama'
                         else NativeDmrTransport(endpoint, request.limits.timeoutSeconds, model_alias, physical_model))
            client = AsyncOpenAI(base_url=base_url, api_key='local-only', max_retries=0,
                http_client=httpx2.AsyncClient(transport=transport, trust_env=False))
        model = model_override or OllamaModel(request.model, provider=OllamaProvider(openai_client=client))
        agent = Agent(model, output_type=output, instructions=instructions, retries=0)
        if validator or output_check:
            @agent.output_validator
            def validate_output(value):
                failure = next(validator.iter_errors(value), None) if validator else None
                if failure:
                    observations.emit('schema_rejected','validation','FAIL',code='OUTPUT_SCHEMA_REJECTED',observed={'keyword':failure.validator})
                    raise ModelRetry('Schema violation at ' + '/'.join(map(str, failure.absolute_path)) + ': ' + failure.validator)
                # Optional trusted-host validator, sharing the SAME usage budget
                # and framework repair loop. Core never imports domain/Lab code.
                # Only bounded public codes reach the model, not raw diagnostics.
                issue = output_check(value) if output_check else None
                if issue:
                    import re
                    code = issue if isinstance(issue, str) and re.fullmatch(r'[A-Z_]{1,64}', issue) else 'HOST_OUTPUT_REJECTED'
                    observations.emit('host_constraint_rejected','validation','FAIL',code=code)
                    raise ModelRetry('Host proposal constraint: ' + code)
                observations.emit('output_validated','validation','PASS')
                return value
        native_options = {'num_ctx': request.limits.contextTokens, 'num_predict': request.limits.outputTokens}
        for key, value in [('top_p', request.limits.topP), ('top_k', request.limits.topK), ('min_p', request.limits.minP)]:
            if value is not None:
                native_options[key] = value
        if request.limits.seed is not None:
            native_options['seed'] = request.limits.seed
        observations.emit('model_started','inference','RUNNING',observed={'model':request.model,'requestLimit':request.limits.requests})
        result = await agent.run(prompt, message_history=history, usage=usage,
            retries={'output': request.limits.requests - 1},
            usage_limits=UsageLimits(request_limit=request.limits.requests),
            model_settings={'temperature': request.limits.temperature,
                'max_tokens': request.limits.outputTokens, 'timeout': request.limits.timeoutSeconds,
                'extra_body': {'think': request.limits.thinking,
                    'options': native_options}})
        observations.emit('model_finished','inference','PASS',observed={'requests':usage.requests,'inputTokens':usage.input_tokens,'outputTokens':usage.output_tokens})
        return {'version': PROTOCOL, 'requestId': request.requestId, 'status': 'proposed',
                'output': result.output, 'usage': {'requests': usage.requests,
                    'inputTokens': usage.input_tokens, 'outputTokens': usage.output_tokens}, 'error': None}
    except Exception as error:
        from pydantic_ai.exceptions import ModelHTTPError
        # The native adapter owns finish metadata. Never classify errors by a
        # provider/model message, reasoning text, response body or exception text.
        if provider == 'docker-model-runner' and getattr(transport, 'failure_code', None) == 'MODEL_OUTPUT_LIMIT':
            code = 'MODEL_OUTPUT_LIMIT'
        else:
            code = 'MODEL_UNAVAILABLE' if (isinstance(error, ModelHTTPError) and error.status_code >= 500 or isinstance(error, (ConnectionError, TimeoutError))) else 'INFERENCE_CONTRACT_FAILED'
        observations.emit('inference_failed','inference','FAIL',code=code,observed={'requests':usage.requests})
        return {'version': PROTOCOL, 'requestId': request.requestId, 'status': 'failed',
                'output': None, 'usage': {'requests': usage.requests,
                    'inputTokens': usage.input_tokens, 'outputTokens': usage.output_tokens}, 'error': code}
    finally:
        if client is not None:
            await client.close()
