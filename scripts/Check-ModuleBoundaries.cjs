// Architecture fitness checks, not natural-language intent routing.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const ts = require(path.join(root, 'apps/web/node_modules/typescript'));
const checks = [], errors = [];
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
function files(directory, suffix) {
  return fs.readdirSync(path.join(root, directory), { withFileTypes: true }).flatMap(entry => {
    const name = directory + '/' + entry.name;
    if (entry.isDirectory() && !['obj', 'bin', 'node_modules'].includes(entry.name)) return files(name, suffix);
    return entry.isFile() && suffix.some(extension => name.endsWith(extension)) ? [name] : [];
  });
}
function check(label, run) {
  const start = errors.length; run();
  checks.push({ label, status: errors.length === start ? 'PASS' : 'FAIL' });
}
const graph = new Map();
const apiProject = 'backend/QuickPrompt.Api/QuickPrompt.Api.csproj';
function loadProject(file) {
  if (graph.has(file)) return;
  const references = [...read(file).matchAll(/<ProjectReference\b[^>]*\bInclude="([^"]+)"/g)]
    .map(match => path.relative(root, path.resolve(root, path.dirname(file), match[1].replaceAll('\\', '/'))).replaceAll('\\', '/'));
  graph.set(file, references);
  references.forEach(loadProject);
}
loadProject(apiProject);
loadProject('backend/QuickPrompt.Mcp/QuickPrompt.Mcp.csproj');
loadProject('src/QuickPrompt.Web/QuickPrompt.Web.csproj');
loadProject('backend/QuickPrompt.Accounts/QuickPrompt.Accounts.csproj');
loadProject('backend/QuickPrompt.Cloud/QuickPrompt.Cloud.csproj');
loadProject('backend/QuickPrompt.Companion/QuickPrompt.Companion.csproj');
check('Companion and inference share transport without importing persistence', () => {
  const permitted = new Set(['backend/QuickPrompt.Inference/QuickPrompt.Inference.csproj', 'backend/QuickPrompt.Application/QuickPrompt.Application.csproj']);
  const references = graph.get('backend/QuickPrompt.Companion/QuickPrompt.Companion.csproj') || [];
  if (references.length !== permitted.size || references.some(reference => !permitted.has(reference))) errors.push('Companion must depend only on its shared inference/application contracts.');
  for (const file of [...files('backend/QuickPrompt.Companion', ['.cs']), ...files('backend/QuickPrompt.Inference', ['.cs'])])
    if (/\bLocalStore\b|\bNpgsql\b|\bMicrosoft\.Data\.Sqlite\b/.test(read(file))) errors.push('Persistence dependency in local inference/companion: ' + file);
});
check('Account persistence is explicit and never shares the local SQLite store', () => {
  const references = graph.get('backend/QuickPrompt.Accounts/QuickPrompt.Accounts.csproj') || [];
  const allowed = new Set(['src/QuickPrompt.Domain/QuickPrompt.Domain.csproj', 'backend/QuickPrompt.Application/QuickPrompt.Application.csproj']);
  if (references.length !== allowed.size || references.some(reference => !allowed.has(reference)))
    errors.push('Accounts must reuse domain/application contracts without depending on the local host/store or inference adapters.');
  for (const file of files('backend/QuickPrompt.Accounts', ['.cs']))
    if (/\bLocalStore\b|\bMicrosoft\.Data\.Sqlite\b/.test(read(file)))
      errors.push('Accounts bypasses PostgreSQL isolation: ' + file);
});
check('MCP delegates to the loopback API without owning persistence or model inference', () => {
  const project = 'backend/QuickPrompt.Mcp/QuickPrompt.Mcp.csproj';
  const references = graph.get(project) || [];
  if (references.length !== 1 || !references[0].endsWith('/QuickPrompt.Application/QuickPrompt.Application.csproj'))
    errors.push('MCP must reference only the shared application contracts.');
  for (const file of files('backend/QuickPrompt.Mcp', ['.cs']))
    if (/\bLocalStore\b|\bMicrosoft\.Data\.Sqlite\b|\bQuickPrompt\.Web\.Infrastructure\b|\bProcess\.Start\b/.test(read(file)))
      errors.push('MCP bypasses its API boundary: ' + file);
});
check('Production project dependency graph is acyclic and excludes evaluation', () => {
  const visited = new Set(), active = new Set();
  function visit(file) {
    if (active.has(file)) { errors.push('Dependency cycle: ' + file); return; }
    if (visited.has(file)) return;
    active.add(file);
    if (file.includes('QuickPrompt.Evaluation')) errors.push('Evaluation is reachable from production: ' + file);
    for (const target of graph.get(file) || []) visit(target);
    active.delete(file); visited.add(file);
  }
  graph.forEach((_, file) => visit(file));
});
check('Infrastructure owns its services without linking frontend source files', () => {
  const project = read('backend/QuickPrompt.Infrastructure/QuickPrompt.Infrastructure.csproj');
  if (project.includes('src/QuickPrompt.Web') || project.includes('src\\QuickPrompt.Web')) errors.push('Infrastructure includes frontend source.');
  for (const target of graph.get('backend/QuickPrompt.Infrastructure/QuickPrompt.Infrastructure.csproj') || [])
    if (target.includes('QuickPrompt.Web')) errors.push('Infrastructure references frontend project: ' + target);
});
check('Application has no dependency on HTTP, SQLite or infrastructure implementations', () => {
  for (const file of files('backend/QuickPrompt.Application', ['.cs'])) {
    if (/^using\s+(?:Microsoft\.AspNetCore|Microsoft\.Data\.Sqlite|QuickPrompt\.Web\.Infrastructure)\b/m.test(read(file)))
      errors.push('Forbidden application import: ' + file);
  }
});
check('React delegates backend transport, library and live agent operations to service ports', () => {
  function prefix(node) {
    if (!node) return '';
    if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) return node.text;
    if (ts.isTemplateExpression(node)) return node.head.text;
    if (ts.isBinaryExpression(node) && node.operatorToken.kind === ts.SyntaxKind.PlusToken) return prefix(node.left);
    return '';
  }
  for (const directory of ['apps/web/src/app', 'apps/web/src/features', 'apps/web/src/hooks'])
    for (const file of files(directory, ['.ts', '.tsx'])) {
      const source = ts.createSourceFile(file, read(file), ts.ScriptTarget.Latest, true);
      function visit(node) {
        if (ts.isCallExpression(node) && ts.isIdentifier(node.expression)) {
          const endpoint = prefix(node.arguments[0]);
          if (node.expression.text === 'fetch' && endpoint.startsWith('/api/')) errors.push('Direct backend fetch: ' + file);
          if (['request', 'openApiResponse'].includes(node.expression.text)
              && ['/prompts', '/templates', '/categories', '/groups', '/library', '/providers', '/transfers', '/ai/local', '/nova/preferences', '/nova/live', '/nova/sessions', '/nova/prepare', '/nova/propose', '/nova/drafts', '/nova/editor-drafts', '/nova/semantic'].some(domain => endpoint === domain || endpoint.startsWith(domain + '/') || endpoint.startsWith(domain + '?')))
            errors.push('Domain operation outside its service port: ' + file);
        }
        if (ts.isNewExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'EventSource'
            && prefix(node.arguments?.[0]).startsWith('/api/v1/nova/')) {
          errors.push('Direct agent event connection outside its service port: ' + file);
        }
        ts.forEachChild(node, visit);
      }
      visit(source);
    }
});
check('Library and agent service contracts do not import React or feature/hook state', () => {
  for (const file of ['library.ts', 'live.ts', 'preparation.ts', 'providers.ts', 'preferences.ts']) {
    const source = ts.createSourceFile(file, read('apps/web/src/services/' + file), ts.ScriptTarget.Latest, true);
    for (const statement of source.statements)
      if (ts.isImportDeclaration(statement) && ts.isStringLiteral(statement.moduleSpecifier)) {
        const name = statement.moduleSpecifier.text;
        if (name === 'react' || name.includes('/features/') || name.includes('/hooks/')) errors.push('UI dependency in domain service ' + file + ': ' + name);
      }
  }
});
check('Library HTTP endpoints depend on application ports instead of concrete persistence', () => {
  const source = read('backend/QuickPrompt.Api/LibraryEndpoints.cs');
  if (/^using\s+QuickPrompt\.Web\.Infrastructure\b/m.test(source) || /\bLocalStore\b/.test(source))
    errors.push('Library endpoints depend on concrete persistence.');
});
check('Server-rendered core workflows use their capability ports', () => {
  for (const file of files('src/QuickPrompt.Web/Components/Pages', ['.cs', '.razor']).filter(file => path.basename(file).startsWith('Home')))
    if (/\bStore\.(?:FindTemplate|QueryTemplates|ActiveTemplateCount|Versions|Save|Delete|Settings|SaveSettings|History|Executions|Preview|Compile|FavoriteFinal|SaveExecution|ClearHistory|SaveProposal|DraftProposal|StageDraftImprovement|Reject)\s*\(/.test(read(file)))
      errors.push('Migrated core workflow bypasses its capability port: ' + file);
});
check('Server-rendered organization consumers use explicit capability ports', () => {
  const consumers = ['src/QuickPrompt.Web/Components/Pages/Home.Organization.cs', 'src/QuickPrompt.Web/Components/Pages/Home.Simplicity.cs', 'src/QuickPrompt.Web/Components/Pages/Home.razor.cs',
    'src/QuickPrompt.Web/Components/Shared/GroupLibrary.razor', 'src/QuickPrompt.Web/Components/Shared/GroupMembershipPicker.razor', 'src/QuickPrompt.Web/Components/Shared/InstructionLibrary.razor'];
  for (const file of consumers) {
    if (/\bStore\.(?:Groups|GroupsFor|SaveGroup|DeleteGroup|AssignGroup|CreateGroupAndAssign|FindInstruction|QueryInstructions|InstructionChoices|InstructionVersions|SaveInstruction|DeleteInstruction|Combinations|SaveCombination|DeleteCombination)\s*\(/.test(read(file)))
      errors.push('Organization operation bypasses its capability port: ' + file);
    if (file.includes('/Shared/') && /@inject\s+LocalStore\b/.test(read(file))) errors.push('Organization component injects concrete persistence: ' + file);
  }
});
check('Server-rendered components and panel interop do not access concrete persistence', () => {
  for (const file of [...files('src/QuickPrompt.Web/Components', ['.cs', '.razor']), 'src/QuickPrompt.Web/Infrastructure/PanelSession.cs'])
    if (/\bLocalStore\b|\bStore\./.test(read(file))) errors.push('Concrete persistence in UI/interop consumer: ' + file);
});
check('Native startup and settings depend on segregated database lifecycle ports', () => {
  for (const file of ['apps/mobile/App.xaml.cs', 'apps/mobile/ViewModels/SettingViewModel.cs', 'apps/mobile/Presentation/ViewModels/BaseViewModel.cs'])
    if (/\bDatabaseServiceManager\b/.test(read(file))) errors.push('Concrete lifecycle coordinator in native consumer: ' + file);
  const contract = read('apps/mobile/ApplicationLayer/Common/Interfaces/IDatabaseLifecycle.cs');
  for (const [port, method] of [['IDatabaseInitialization', 'InitializeAsync'], ['IDatabaseRestore', 'RestoreAsync']]) {
    if (!contract.includes('interface ' + port) || !contract.includes('Task ' + method + '();'))
      errors.push('Missing native lifecycle port: ' + port);
    if (!read('apps/mobile/MauiProgram.cs').includes('AddSingleton<' + port + '>(sp => sp.GetRequiredService<DatabaseServiceManager>())'))
      errors.push('Native lifecycle alias must resolve the existing coordinator: ' + port);
  }
});
check('Model transport can be composed without constructing the semantic interpreter', () => {
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  if (!composition.includes('AddSingleton<IAgentModelTransport>(sp => sp.GetRequiredService<AdaptiveCoreModelTransport>())')
      || !composition.includes('AddSingleton<IValidatedAgentModelTransport>(sp => sp.GetRequiredService<AdaptiveCoreModelTransport>())'))
    errors.push('Model transport must resolve its independent owner.');
  for (const file of ['backend/QuickPrompt.Inference/AdaptiveCoreModelTransport.cs'])
    if (/\bPreparationInference\b|\bLocalStore\b|\bSemanticPlanCompiler\b/.test(read(file))) errors.push('Semantic/domain dependency in model transport: ' + file);
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/InferenceReuse.cs')))
    errors.push('Old interpreter cache owner remains after migration.');
});
check('Server-rendered components do not classify persistence vendor exceptions', () => {
  for (const file of files('src/QuickPrompt.Web/Components', ['.cs', '.razor']))
    if (/\bMicrosoft\.Data\.Sqlite\b|\bSqliteException\b/.test(read(file))) errors.push('Persistence vendor failure type in UI: ' + file);
});
check('Production entry point delegates to a trusted host composition factory', () => {
  if (read('backend/QuickPrompt.Api/Program.cs').trim() !== 'QuickPrompt.Nova.Api.QuickPromptHost.Run(args);')
    errors.push('API entry point duplicates host startup.');
  const host = read('backend/QuickPrompt.Api/QuickPromptHost.cs');
  const hook = host.indexOf('configureServices?.Invoke(builder.Services);'), build = host.indexOf('var app = builder.Build();');
  if (!host.includes('Action<IServiceCollection>? configureServices = null') || hook < 0 || build <= hook)
    errors.push('Trusted service composition must finish before Build.');
  if (host.includes('QuickPrompt.Evaluation')) errors.push('Production host depends on evaluation.');
});
check('Legacy inference adapter consumes semantic ports without owning model transport or interpreters', () => {
  const adapter = read('backend/QuickPrompt.Infrastructure/NovaInference.cs');
  if (/\bHttpClient\b|\bSocketsHttpHandler\b|\bPreparationInference\b/.test(adapter)) errors.push('Private model/interpreter construction in NovaInference.');
  if (!adapter.includes('ISemanticIntentInterpreter semantic')) errors.push('Legacy adapter lacks its semantic port.');
});
check('Contract metadata exporter is independent of the API artifact it generates', () => {
  const exporter = 'backend/QuickPrompt.ContractExport/QuickPrompt.ContractExport.csproj';
  const visited = new Set();
  function visit(file) {
    if (visited.has(file)) return; visited.add(file);
    if (file.includes('QuickPrompt.Api') || file.includes('QuickPrompt.Evaluation')) errors.push('Contract export depends on API/evaluation: ' + file);
    for (const reference of read(file).matchAll(/<ProjectReference\b[^>]*\bInclude="([^"]+)"/g))
      visit(path.relative(root, path.resolve(root, path.dirname(file), reference[1].replaceAll('\\', '/'))).replaceAll('\\', '/'));
  }
  visit(exporter);
  const generator = read('scripts/nova-openapi.cjs');
  if (generator.includes('qwen3:8b') || generator.includes('qwen2.5:7b-instruct')) errors.push('OpenAPI generator duplicates local model identities.');
});
check('UI action selection can be composed without the semantic interpreter or authoring client', () => {
  const selector = read('backend/QuickPrompt.Inference/UiDecisionModel.cs');
  if (/\bPreparationInference\b|\bILocalLlmClient\b|\bLocalStore\b|\bHttpClient\b/.test(selector))
    errors.push('UI selector depends on semantic/authoring/storage/HTTP implementation.');
  if (!selector.includes('IAgentModelTransport transport')) errors.push('UI selector lacks its borrowed transport port.');
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  if (!composition.includes('AddSingleton<IUiDecisionModel>(sp => new UiDecisionModel(sp.GetRequiredService<IAgentModelTransport>()))'))
    errors.push('Visible execution must compose its UI selector independently.');
});
check('Candidate membership has an independent owner and no ranked compatibility authority', () => {
  const selector = read('backend/QuickPrompt.Inference/SemanticCandidateSelector.cs');
  if (/\bPreparationInference\b|\bILocalLlmClient\b|\bLocalStore\b|\.Score\b/.test(selector))
    errors.push('Candidate selector depends on unrelated capabilities or retrieval rank.');
  if (!read('backend/QuickPrompt.Api/ApplicationModules.cs').includes('AddSingleton<ICandidateSelector>(sp => new SemanticCandidateSelector(sp.GetRequiredService<IAgentModelTransport>()))'))
    errors.push('Candidate selector is not independently composed.');
  const compatibility = read('backend/QuickPrompt.Application/StructuredIntentCompatibility.cs');
  if (/\bRankedCandidateCompatibility\b|\.Score\b/.test(compatibility))
    errors.push('Legacy retrieval rank still grants source selection authority.');
  if (!compatibility.includes('"CAPABILITY_UNAVAILABLE"')) errors.push('Missing membership capability does not fail explicitly.');
});
check('Field extraction has an independent provider and an explicit orchestration port', () => {
  const extractor = read('backend/QuickPrompt.Inference/PromptInputValueExtractor.cs');
  if (/\bPreparationInference\b|\bIPromptGenerator\b|\bILocalLlmClient\b|\bLocalStore\b/.test(extractor))
    errors.push('Field extraction depends on unrelated interpretation, authoring or persistence.');
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  if (!composition.includes('AddSingleton<IInputValueExtractor>(sp => new PromptInputValueExtractor(sp.GetRequiredService<IAgentModelTransport>()))')
      || !read('backend/QuickPrompt.Application/AgentOrchestrator.cs').includes('IInputValueExtractor? inputExtractor = null')
      || !composition.includes('new PromptInputScopeInterpreter(sp.GetRequiredService<IInputValueExtractor>())'))
    errors.push('Field extraction is not independently composed and shared.');
  for (const file of ['backend/QuickPrompt.Application/AgentOrchestrator.Semantic.cs', 'backend/QuickPrompt.Application/AgentOrchestrator.Visible.cs'])
    if (/generator is IInputValueExtractor/.test(read(file))) errors.push('Orchestration still requires extraction to be implemented by the generator: ' + file);
});
check('Input scope is independently composed and its old partial implementation is retired', () => {
  const provider = read('backend/QuickPrompt.Inference/PromptInputScopeInterpreter.cs');
  if (/\bPreparationInference\b|\bIIntentInterpreter\b|\bIPromptGenerator\b|\bLocalStore\b|\bHttpClient\b/.test(provider))
    errors.push('Input scope depends on unrelated interpretation, authoring, persistence or HTTP.');
  if (!read('backend/QuickPrompt.Api/ApplicationModules.cs').includes('AddSingleton<IInputScopeInterpreter>(sp => new PromptInputScopeInterpreter(sp.GetRequiredService<IInputValueExtractor>()))'))
    errors.push('Input scope lacks independent factory composition.');
  for (const file of ['backend/QuickPrompt.Application/TaskPlanning.cs', 'backend/QuickPrompt.Application/TaskSourceResolver.cs'])
    if (/interpreter is (?:not )?IInputScopeInterpreter/.test(read(file))) errors.push('Scope consumer still requires the semantic interpreter to publish it: ' + file);
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/InputScopeInference.cs')))
    errors.push('Old scope partial remains after migration.');
  const choice = read('backend/QuickPrompt.Application/ActionChoiceInterpreter.cs');
  if (/\bIAgentModelTransport\b|\bIPromptRetriever\b|\bLocalStore\b/.test(choice))
    errors.push('Explicit action boundary acquired inference, retrieval or persistence.');
});
check('Retired fidelity review stays outside production composition and shares packet encoding', () => {
  const reviewer = read('backend/QuickPrompt.Inference/DraftFidelityReviewer.cs');
  if (/\bPreparationInference\b|\bILocalLlmClient\b|\bLocalStore\b|\bHttpClient\b/.test(reviewer))
    errors.push('Fidelity review depends on unrelated interpretation, editing, persistence or HTTP.');
  if (!reviewer.includes('IValidatedAgentModelTransport transport')) errors.push('Fidelity reviewer lacks its validated transport port.');
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  // The compatibility reviewer remains loadable for historical consumers, but
  // proposal authoring no longer treats a second model as content authority.
  if (/AddSingleton<IDraftFidelityReviewer>|GetRequiredService<IDraftFidelityReviewer>/.test(composition))
    errors.push('Retired fidelity review has re-entered production composition.');
  if (!read('backend/QuickPrompt.Inference/PromptDraftGenerator.cs').includes('return await GenerateDraftProposal('))
    errors.push('Production authoring does not delegate to the complete proposal owner.');
  for (const file of ['backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs', 'backend/QuickPrompt.Inference/DraftFidelityReviewer.cs'])
    if (!read(file).includes('AgentStagePacket.Encode(')) errors.push('Stage packet encoding has a second owner: ' + file);
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/DraftFidelity.cs')))
    errors.push('Old embedded fidelity implementation remains after migration.');
});
check('Draft generation has an independent factory and one implementation behind the existing port', () => {
  for (const file of ['backend/QuickPrompt.Inference/PromptDraftGenerator.cs', 'backend/QuickPrompt.Inference/PromptDraftGenerator.Authoring.cs']) {
    const source = read(file);
    if (/\bPreparationInference\b|\bLocalStore\b|\bHttpClient\b|\bISemanticIntentInterpreter\b/.test(source))
      errors.push('Generation depends on interpretation, persistence or a private transport: ' + file);
  }
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  if (!composition.includes('AddSingleton<IPromptGenerator>(sp => new PromptDraftGenerator(')
      || !read('backend/QuickPrompt.Application/AgentOrchestrator.cs').includes('IPromptGenerator generator'))
    errors.push('Generation is not independently composed and injected.');
  if (composition.includes('AddSingleton<IPromptGenerator>(sp => sp.GetRequiredService<PreparationInference>())'))
    errors.push('Generation still resolves the whole interpreter.');
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/PreparationInference.Authoring.cs')))
    errors.push('Embedded authoring implementation remains after migration.');
  const compatibility = read('backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs');
  if (/GenerateCore\(|EditTemplate\(|BindAuthoringReferences\(/.test(compatibility))
    errors.push('Generation algorithm is duplicated in the compatibility owner.');
});
check('Production preserves explicit user choice and an independent content owner', () => {
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  for (const port of ['IIntentInterpreter', 'ISemanticIntentInterpreter'])
    if (!composition.includes('AddSingleton<' + port + '>(sp => sp.GetRequiredService<ActionChoiceInterpreter>())'))
      errors.push('Workflow port does not preserve explicit choice: ' + port);
  if (/GetRequiredService<AdaptiveCoreInterpreter>|AddSingleton<AdaptiveCoreInterpreter>/.test(composition))
    errors.push('Retired free decoder is registered in production.');
  if (!composition.includes('AddSingleton<ISemanticContentInterpreter, PromptContentInterpreter>()'))
    errors.push('Content interpretation lacks its domain owner.');
  if (/AddSingleton<(?:SemanticIntentInterpreter|OllamaAgentModelTransport|ISourceReferenceResolver|ISemanticClauseReviewer|IGroundedSourceSelector)>/.test(composition))
    errors.push('Retired native planner/transport is registered in production.');
  for (const file of files('backend/QuickPrompt.Infrastructure', ['.cs']))
    if (/(?:class SemanticIntentInterpreter|class OllamaAgentModelTransport)/.test(read(file)))
      errors.push('Native compatibility implementation remains in production: ' + file);
  const launcher = read('agents/nova-core/nova_worker.py');
  if (/CREATE_DRAFT|COPY_PROMPT|class WorkOrder|AgentRuntime|OllamaProvider/.test(launcher))
    errors.push('Python launcher still owns application or inference logic.');
  for (const retired of ['bridge.py','run.py','test_bridge.py'])
    if (fs.existsSync(path.join(root,'agents/nova-core',retired))) errors.push('Alternate domain engine remains: ' + retired);
  const selector = read('backend/QuickPrompt.Inference/UiDecisionModel.cs');
  if (/retryReason|json_schema_retry|for \(var attempt/.test(selector)) errors.push('UI domain selector owns a protocol-repair loop.');
});
check('Web hosts share the owned local-client factory without claiming borrowed HTTP resources', () => {
  const client = read('backend/QuickPrompt.Inference/OllamaClient.cs');
  if (!client.includes('ILocalLlmClient, IDisposable') || !client.includes('public static OllamaClient CreateOwned(')
      || !/if \(ownsHttp\) http\??\.Dispose\(\);/.test(client))
    errors.push('Local inference adapter lacks explicit owned versus borrowed resource lifecycle.');
  for (const file of ['backend/QuickPrompt.Api/ApplicationModules.cs', 'src/QuickPrompt.Web/Program.cs']) {
    const host = read(file);
    if (!host.includes('OllamaClient.CreateOwned(') || /new OllamaClient\(new HttpClient/.test(host))
      errors.push('Web host leaves an unowned inference HTTP client: ' + file);
  }
});
check('Account NOVA composes canonical capabilities only through its private companion port', () => {
  const source = read('backend/QuickPrompt.Cloud/AccountNovaInference.cs');
  if (!source.includes('proposals.ForConnection(actor, libraryId, connectionId)') || !source.includes('OllamaClient.ForTransport(transport)'))
    errors.push('Account NOVA does not bind its private model port outside packets.');
  if (/\bnew HttpClient\b|OllamaClient\.CreateOwned|\bnew AdaptiveCoreModelTransport\b|\bProcess\.Start\b/.test(source))
    errors.push('Account NOVA constructs a server-local provider instead of using the approved companion.');
  for (const owner of ['PromptDraftGenerator','PromptInputValueExtractor','SemanticCandidateSelector','UiDecisionModel'])
    if (!source.includes('new ' + owner + '(')) errors.push('Account NOVA does not reuse canonical capability: ' + owner);
  if (/new DraftFidelityReviewer\(|IDraftFidelityReviewer/.test(source))
    errors.push('Account NOVA allocates retired fidelity review.');
});
check('React application composes service ports and visible sessions without feature singleton imports', () => {
  const modulePorts = { library: new Set(['prompts', 'templates', 'collections', 'archives']), live: new Set(['live', 'startVisible', 'observeOperation', 'surfaceCapability', 'releaseSurface']),
    preparation: new Set(['preparation']), providers: new Set(['providers']), preferences: new Set(['preferences']) };
  for (const file of [...files('apps/web/src/features', ['.ts', '.tsx']), 'apps/web/src/app/App.tsx', 'apps/web/src/hooks/useLibrary.ts']) {
    const tree = ts.createSourceFile(file, read(file), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
    for (const node of tree.statements) {
      if (!ts.isImportDeclaration(node) || node.importClause?.isTypeOnly || !node.importClause?.namedBindings || !ts.isNamedImports(node.importClause.namedBindings)) continue;
      const banned = modulePorts[node.moduleSpecifier.text.split('/').at(-1)];
      if (banned) for (const item of node.importClause.namedBindings.elements)
        if (!item.isTypeOnly && banned.has(item.propertyName?.text ?? item.name.text)) errors.push('Feature consumes global service instead of application port: ' + file);
    }
  }
  const entry = read('apps/web/src/main.tsx');
  if (!entry.includes('const services = createApplicationServices();') || !entry.includes('ApplicationServiceProvider services={services}'))
    errors.push('React entry does not own its stable service composition.');
  const liveTree = ts.createSourceFile('live.ts', read('apps/web/src/services/live.ts'), ts.ScriptTarget.Latest, true);
  if (liveTree.statements.some(node => ts.isVariableStatement(node) && node.declarationList.declarations.some(d => ts.isIdentifier(d.name) && d.name.text === 'surfaces')))
    errors.push('Visible surface registry remains globally shared across compositions.');
  const application = read('apps/web/src/services/application.ts');
  if (/from ['"].*(?:react|features\/|hooks\/)/.test(application)) errors.push('Connection composition imports UI/domain state.');
});
check('Model admission and defaults have one pure policy owner without provider-layer export dependencies', () => {
  const policy = read('src/QuickPrompt.Domain/LocalModelPolicy.cs');
  if (/HttpClient|Microsoft\.Data\.Sqlite|Microsoft\.AspNetCore/.test(policy)) errors.push('Product model policy depends on a transport/host/database.');
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/Local/LocalModelPolicy.cs')))
    errors.push('Provider-local policy copy remains after migration.');
  for (const file of [...files('backend/QuickPrompt.Application', ['.cs']), ...files('backend/QuickPrompt.Api', ['.cs'])])
    if (read(file).includes('qwen3:8b') || read(file).includes('qwen2.5:7b-instruct')) errors.push('Model identities duplicated outside policy: ' + file);
  const forwards = read('backend/QuickPrompt.Infrastructure/ForwardedLocalContracts.cs');
  if (!forwards.includes('TypeForwardedTo(typeof(LocalModelPolicy))') || !forwards.includes('TypeForwardedTo(typeof(LocalModelCapability))'))
    errors.push('Existing public policy identities lack assembly forwarding.');
  if (read('backend/QuickPrompt.ContractExport/QuickPrompt.ContractExport.csproj').includes('QuickPrompt.Infrastructure'))
    errors.push('Metadata exporter still depends on model adapters.');
});
check('Semantic workflows receive independent capabilities instead of requiring generator or UI traits', () => {
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  for (const port of ['ISourceReferenceResolver', 'ISemanticClauseReviewer', 'IGroundedSourceSelector'])
    if (composition.includes('AddSingleton<' + port + '>'))
      errors.push('Retired native planning port is present in the Core factory: ' + port);
  if (!composition.includes('AddSingleton<ISemanticAdviceResponder>(sp => new PromptAdviceResponder('))
    errors.push('Advice must have an independent owner outside legacy planning.');
  const advice = read('backend/QuickPrompt.Inference/PromptAdviceResponder.cs');
  if (/\bSemanticIntentInterpreter\b/.test(advice)) errors.push('Advice depends on legacy planning.');
  const resolver = read('backend/QuickPrompt.Application/TaskSourceResolver.cs');
  if (resolver.includes('selector is IGroundedSourceSelector') || resolver.includes('interpreter is ISemanticClauseReviewer')
      || !resolver.includes('groundedSelector ?? selector as IGroundedSourceSelector')) errors.push('Source workflow depends on unrelated provider traits.');
  const visible = read('backend/QuickPrompt.Application/AgentOrchestrator.Visible.cs');
  const manual = read('backend/QuickPrompt.Application/AgentOrchestrator.Semantic.cs');
  if (visible.includes('model is ISemanticAdviceResponder') || manual.includes('generator is ISemanticAdviceResponder'))
    errors.push('Explanation still requires capabilities from the generator/UI provider.');
});
check('Legacy aggregate exists only in evaluation compatibility and cannot enter the production composition', () => {
  for (const directory of ['backend/QuickPrompt.Infrastructure', 'backend/QuickPrompt.Application', 'backend/QuickPrompt.Api', 'src/QuickPrompt.Domain', 'src/QuickPrompt.Web'])
    for (const file of files(directory, ['.cs', '.csproj']))
      if (/\bPreparationInference\b/.test(read(file))) errors.push('Legacy aggregate remains in production: ' + file);
  const compatibility = read('backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs');
  if (!compatibility.includes('public sealed class PreparationInference') || /partial class PreparationInference/.test(compatibility))
    errors.push('Historical compatibility facade was not retained without embedded semantic algorithms.');
  if (fs.existsSync(path.join(root, 'backend/QuickPrompt.Infrastructure/PreparationInference.cs')))
    errors.push('Legacy aggregate is still compiled by Infrastructure.');
});
check('Agent composition freezes technical configuration before deferred services resolve', () => {
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  const agent = composition.slice(composition.indexOf('public static IServiceCollection AddNovaAgentModule'), composition.indexOf('public static IServiceCollection AddVisibleExecutionModule'));
  if (!agent.includes('var settings = NovaAgentSettings.Read(configuration);') || /configuration\[|configuration\.GetValue/.test(agent))
    errors.push('Agent factory still samples live host configuration inside deferred registrations.');
  const profile = read('backend/QuickPrompt.Api/NovaAgentSettings.cs');
  if (!profile.includes('internal sealed record NovaAgentSettings(') || !profile.includes('PreparationOptions Budget')
      || /static.*(?:settings|currentProfile)\s*[=;]/.test(profile)) errors.push('Runtime profile is not an immutable composition-owned value.');
  const visible = composition.slice(composition.indexOf('public static IServiceCollection AddVisibleExecutionModule'));
  if (!visible.includes('Path.GetFullPath(worker), node)') || !visible.includes('var worker = configuration[') || !visible.includes('var node = configuration['))
    errors.push('Visible worker factory still captures mutable configuration.');
});
check('UI proposals use application-owned sessions instead of a global driver writer', () => {
  const driver = read('apps/web/src/features/nova/AgentUiDriver.ts');
  if (/currentDriver|export const setAgentDriver|export async function applyAgentProposal/.test(driver)) errors.push('Driver still owns a global proposal destination.');
  const session = read('apps/web/src/features/nova/AgentProposalSession.ts');
  if (/from ['"]react|document\.|window\.|fetch\(/.test(session)) errors.push('Proposal lease owner depends on DOM, React or HTTP.');
  const provider = read('apps/web/src/features/nova/AgentUiContext.tsx');
  if (!provider.includes('useState(createAgentProposalSession)') || !read('apps/web/src/app/App.tsx').includes('<AgentUiProvider><ApplicationView /></AgentUiProvider>'))
    errors.push('Application does not own a stable proposal session.');
  const runtime = read('apps/web/src/features/nova/SameTabAgent.tsx');
  if (!runtime.includes('proposalSession.register(driver)') || !runtime.includes('releaseProposal();')) errors.push('Driver lifetime does not release its own proposal registration.');
  if (!read('apps/web/src/features/prompts/EditorPreparation.tsx').includes('useAgentProposalSession()')) errors.push('Editor does not consume its proposal scope.');
});
check('UI driver composes typed command dispatch separately from DOM mechanics', () => {
  const driver = read('apps/web/src/features/nova/AgentUiDriver.ts');
  if (!driver.includes('commandFactory: AgentUiCommandFactory = createAgentUiCommands') || !driver.includes('this.commands.find(action.tool)'))
    errors.push('Driver does not compose its command registry through the typed factory.');
  if (/tool === ['"]ui_|tool === ['"]start/.test(driver)) errors.push('Driver retains embedded command dispatch branches.');
  const commands = read('apps/web/src/features/nova/AgentUiCommands.ts');
  if (!commands.includes('export interface AgentUiCommandHost') || !commands.includes('export class AgentUiCommandRegistry')
      || !commands.includes('Object.freeze') || !commands.includes("INVALID_UI_COMMAND_REGISTRY")) errors.push('Command factory lacks explicit host or validated immutable registry.');
  if (/host\.previous|host\.progress|host\.target|fetch\(|from ['"].*(?:ApplicationModules|services\/application)/.test(commands))
    errors.push('UI command providers own observation finalization, transport or server composition.');
});
check('Prompt deletion presentation has one lifecycle owner over existing library and live ports', () => {
  const application = read('apps/web/src/app/App.tsx');
  if (!application.includes('= usePromptDeletion(prompts, live, {') || /nova-review-deletion|nova-enable-deletion|deletionReview\.current|setDeleteTarget|setDeletionReady/.test(application))
    errors.push('Application still owns deletion protocol state or listeners.');
  const owner = read('apps/web/src/features/prompts/usePromptDeletion.ts');
  if (!owner.includes('export interface PromptDeletionHost') || !owner.includes('export interface PromptDeletionController')
      || !owner.includes("Pick<PromptLibraryClient, 'get' | 'remove'>") || !owner.includes("Pick<LiveClient, 'get' | 'deletion'>")
      || !owner.includes('[prompts, live]')) errors.push('Deletion owner lacks narrow ports or service-bound lifecycle.');
  if (/fetch\(|localStorage|sessionStorage|from ['"].*(?:App|ApplicationModules|services\/application)/.test(owner))
    errors.push('Deletion presentation owns persistence, global connections or shell composition.');
});
check('Editor navigation shares a private revision-bound draft session instead of shell-owned cache rules', () => {
  const application = read('apps/web/src/app/App.tsx');
  if (!application.includes('useState(createEditorDraftSession)') || !application.includes('editorDrafts.read(selected)')
      || !application.includes('editorDrafts.read(fresh)') || /editorDrafts\.current|cached\?\.revision === fresh\.revision/.test(application))
    errors.push('Editor draft cache or compatibility rule remains distributed in the shell.');
  const session = read('apps/web/src/features/prompts/EditorDraftSession.ts');
  if (!session.includes('export interface EditorDraftSession') || !session.includes('implements EditorDraftSession')
      || !session.includes('export function createEditorDraftSession') || !session.includes('draft.revision !== source.revision'))
    errors.push('Editor session lacks a typed factory or source revision invariant.');
  if (/fetch\(|window\.|document\.|localStorage|sessionStorage|from ['"]react/.test(session))
    errors.push('Draft snapshots depend on UI runtime, persistence or transport.');
});
check('Editor context preparation has one borrowed-port owner without UI or parser authority', () => {
  const application = read('apps/web/src/app/App.tsx');
  if (!application.includes('createEditorContextPreparer(templates, preparation)') || /templates\.inspect|preparation\.editorDraft/.test(application))
    errors.push('Editor context inspection/submission remains embedded in the shell.');
  const owner = read('apps/web/src/features/prompts/EditorContextPreparation.ts');
  if (!owner.includes('export interface EditorContextPreparer') || !owner.includes('implements EditorContextPreparer')
      || !owner.includes("Pick<TemplateClient, 'inspect'>") || !owner.includes("Pick<PreparationClient, 'editorDraft'>")
      || !owner.includes('signal?.throwIfAborted()')) errors.push('Editor context owner lacks narrow ports or cancellation boundaries.');
  if (/fetch\(|document\.|window\.|localStorage|sessionStorage|from ['"]react/.test(owner))
    errors.push('Editor context owner depends on UI runtime, global HTTP or persistence.');
});
check('HTTP editor preparation delegates to application owners without inline evaluation or session writes', () => {
  const endpoints = read('backend/QuickPrompt.Api/LiveEndpoints.cs');
  if (!endpoints.includes('IEditorDraftPreparation preparation') || !endpoints.includes('EditorTools.Compile(input.TemplateText, input.Values)')
      || /new DeterministicPromptEvaluator|sessions\.ObserveDraft|ValidateValues\(/.test(endpoints))
    errors.push('Editor application rules remain embedded in HTTP endpoints.');
  const owner = read('backend/QuickPrompt.Application/EditorDraftPreparation.cs');
  if (!owner.includes('IEditorDraftPreparation') || !owner.includes('IPromptCatalog library, IPromptEvaluator evaluator')
      || !owner.includes('IPreparedDraftStore drafts, AgentSessions sessions') || !owner.includes('evaluator.Evaluate('))
    errors.push('Editor use case lacks existing explicit capability owners.');
  if (/Microsoft\.AspNetCore|HttpContext|IServiceProvider|IIntentInterpreter|IPromptGenerator|ILocalLlmClient/.test(owner))
    errors.push('Deterministic editor preparation depends on host resolution or model capabilities.');
  if (!read('backend/QuickPrompt.Api/ApplicationModules.cs').includes('AddSingleton<IEditorDraftPreparation, EditorDraftPreparation>()'))
    errors.push('Editor preparation is absent from the composition factory.');
});
check('Prepared draft completion and saving use segregated application capabilities', () => {
  const endpoints = read('backend/QuickPrompt.Api/PreparationEndpoints.cs');
  if (!endpoints.includes('IPreparedDraftCompletion completion') || !endpoints.includes('IPreparedDraftSaving saving')
      || /drafts\.Save\(|drafts\.Fingerprint\(input\.OperationId\)|SAVE_REQUIRES_REVIEW/.test(endpoints))
    errors.push('Draft lifecycle rules remain embedded in HTTP.');
  const completion = read('backend/QuickPrompt.Application/PreparedDraftCompletion.cs');
  if (!completion.includes(': IPreparedDraftCompletion')) errors.push('Missing typed completion capability.');
  if (!completion.includes('return drafts.Get(input.OperationId) ?? throw new ConflictException') || completion.includes('return drafts.Get(input.OperationId)!'))
    errors.push('Receipt replay can acknowledge an unavailable result.');
  const saving = read('backend/QuickPrompt.Application/PreparedDraftSaving.cs');
  if (!saving.includes(': IPreparedDraftSaving') || /LiveRuns|AgentSessions|IIntentInterpreter|IPromptGenerator|ILocalLlmClient|HttpContext/.test(saving))
    errors.push('Saving depends on unrelated live/model capabilities.');
  const composition = read('backend/QuickPrompt.Api/ApplicationModules.cs');
  for (const name of ['Completion', 'Saving'])
    if (!composition.includes('AddSingleton<IPreparedDraft' + name + ', PreparedDraft' + name + '>()')) errors.push('Missing lifecycle factory: ' + name);
});
check('Prompt value fields have one feature owner and three composed consumers', () => {
  const owner = 'apps/web/src/features/prompts/PromptVariableFields.tsx';
  for (const file of files('apps/web/src', ['.tsx'])) {
    const source = ts.createSourceFile(file, read(file), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
    function visit(node) {
      if (ts.isJsxAttribute(node) && node.name.getText(source) === 'data-testid' && file !== owner) {
        function marker(child) {
          if (ts.isStringLiteral(child) && child.text === 'variable-field') errors.push('Workspace field markup duplicated outside its owner: ' + file);
          ts.forEachChild(child, marker);
        }
        marker(node);
      }
      ts.forEachChild(node, visit);
    }
    visit(source);
  }
  for (const file of ['features/prompts/PromptFocus.tsx', 'features/prompts/EditorPreparation.tsx', 'features/nova/QuickActions.tsx']) {
    const source = read('apps/web/src/' + file);
    if (!source.includes('import { PromptVariableFields }') || !source.includes('<PromptVariableFields '))
      errors.push('Prompt value consumer bypasses its shared field owner: ' + file);
  }
});
const report = { status: errors.length ? 'FAIL' : 'PASS', scope: 'STATIC_ARCHITECTURE_NOT_RUNTIME_OR_AGENT_RELIABILITY', checks, projects: Object.fromEntries(graph), errors };
if (process.argv[2]) {
  const output = path.resolve(root, process.argv[2]);
  assert.ok(output.startsWith(path.join(root, 'docs/evidence') + path.sep), 'Use an owned evidence destination.');
  assert.ok(!fs.existsSync(output), 'Preserve existing reports.');
  fs.writeFileSync(output, JSON.stringify(report, null, 2));
}
console.log(JSON.stringify(report, null, 2));
process.exitCode = errors.length ? 1 : 0;

