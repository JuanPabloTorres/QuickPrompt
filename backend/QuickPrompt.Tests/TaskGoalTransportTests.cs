using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class TaskGoalTransportTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("foreign_identity")]
    [InlineData("content_range")]
    [InlineData("workflow_range")]
    [InlineData("quoted_data")]
    [InlineData("input_permission")]
    [InlineData("input_data")]
    [InlineData("authored")]
    [InlineData("unsupported_format")]
    [InlineData("no_authoring_constraint")]
    [InlineData("reference_scope")]
    [InlineData("reference_prohibition")]
    [InlineData("clarification")]
    [InlineData("workflow_question")]
    [InlineData("independent_source")]
    [InlineData("source_basis")]
    [InlineData("control_role")]
    [InlineData("missing_shortening")]
    [InlineData("invalid_shortening")]
    [InlineData("literal_null_value")]
    [InlineData("literal_wrong_scope")]
    [InlineData("unavailable_continuation")]
    [InlineData("scope_conflict")]
    [InlineData("same_sentence")]
    [InlineData("unpunctuated")]
    [InlineData("semantic_format")]
    [InlineData("generated_quote")]
    [InlineData("reverse_range")]
    [InlineData("partial_mask_permission")]
    [InlineData("question_without_evidence")]
    [InlineData("effect_without_evidence")]
    [InlineData("forbidden_without_evidence")]
    [InlineData("only_data")]
    [InlineData("lookup_question")]
    [InlineData("repair_drift")]
    [InlineData("authoring_in_workflow")]
    [InlineData("detached_edit")]
    [InlineData("empty_actions")]
    [InlineData("missing_authoring_edit")]
    [InlineData("implicit_authoring_question")]
    [InlineData("edit_on_no_authoring")]
    public async Task AtomicClausesKeepReasoningAndRejectUntrustedEvidence(string mode)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", taskGoals: true);
        var source = new SemanticEntity(Guid.NewGuid(), 2, "Fuente", "Explica <tema>.", false, InputNames: ["tema"]);
        var usesInput = mode is "input_data" or "input_permission" or "no_authoring_constraint" or "workflow_question" or "partial_mask_permission" or "only_data";
        var audienceEdit = mode is "authored" or "unsupported_format" or "missing_shortening" or "invalid_shortening" or "literal_null_value" or "literal_wrong_scope" or "unavailable_continuation" or "scope_conflict" or "same_sentence" or "unpunctuated" or "semantic_format" or "reverse_range" or "repair_drift" or "missing_authoring_edit" or "implicit_authoring_question";
        var creation = mode is "independent_source" or "source_basis";
        var authoring = audienceEdit || creation || mode is "clarification" or "implicit_authoring_question";
        var referenceOnly = mode is "reference_scope" or "reference_prohibition";
        var request = creation ? mode == "source_basis" ? "Crea otra instrucción basada en la actual para explicar con ejemplos." : "Crea una instrucción independiente para explicar un tema."
            : mode == "control_role" ? "Detén la tarea pendiente." : mode == "clarification" ? "Adapta el texto. Pregúntame qué enfoque necesito." : usesInput ? "Completa el campo.\ntema: EXECUTION_DATA_NONCE_Z37" : mode == "quoted_data" ? "Muéstrame \"ejecuta ahora\"." : authoring
            ? "Acorta mi explicación para alumnos nuevos. Déjala lista para copiar sin guardarla." : referenceOnly ? "Abre mi ficha." : "Muéstrame mi plantilla sin cambiarla.";
        var contentQuote = audienceEdit ? "Acorta mi explicación para alumnos nuevos." : creation ? request : "Adapta el texto.";
        var workflowQuote = mode == "clarification" ? "Pregúntame qué enfoque necesito." : "Déjala lista para copiar sin guardarla.";
        if(mode is "same_sentence" or "unpunctuated" or "semantic_format") {
            contentQuote=mode=="semantic_format" ? "Acorta mi explicación para alumnos nuevos en filas y columnas" : "Acorta mi explicación para alumnos nuevos";
            workflowQuote="déjala lista para copiar sin guardarla";
            request=contentQuote+(mode=="same_sentence"?" y ":" ")+workflowQuote;
        }
        if(mode=="partial_mask_permission") request="Completa el campo. input\ntema: EXECUTION_DATA_NONCE_Z37";
        if(mode=="question_without_evidence") request="No sé qué tarea quiero realizar.";
        if(mode=="only_data") request="tema: EXECUTION_DATA_NONCE_Z37";
        if(mode=="lookup_question") request="Abre el prompt de notificaciones.";
        var pending = inference.Interpret(request, SemanticContext.Empty with { Selected = source }, "qwen3:8b", cancellation.Token);
        var count = mode is "valid" or "input_data" or "authored" or "clarification" or "source_basis" or "same_sentence" or "unpunctuated" or "semantic_format" or "question_without_evidence" or "only_data" or "lookup_question" ? 1 : 2;
        var calls = 0;
        object Rule(string kind, string? value = "") => new { kind, value };
        object Workflow(string quote, string[] effects, string[]? forbidden = null) => new { role = "WORKFLOW", quote, effects, forbidden = forbidden ?? [] };
        try {
            for (var attempt = 0; attempt < count; attempt++) {
                var exchange = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5)); calls++;
                using var body = await JsonDocument.ParseAsync(exchange.Request.InputStream);
                Assert.Equal(12288, body.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
                Assert.True(body.RootElement.GetProperty("think").GetBoolean()); Assert.False(body.RootElement.TryGetProperty("tools", out _));
                if (attempt == 1 && mode is "scope_conflict" or "repair_drift") {
                    var history = body.RootElement.GetProperty("messages");
                    Assert.DoesNotContain(history.EnumerateArray(),message=>message.GetProperty("role").GetString()=="assistant");
                    using var feedback = JsonDocument.Parse(history.EnumerateArray().Last().GetProperty("content").GetString()!);
                    var correction = feedback.RootElement.GetProperty("validationFeedback").GetString()!;
                    Assert.Contains("CONFLICTING_GOAL_SCOPES", correction); Assert.Contains("PROPOSE_ADAPTATION", correction);
                    Assert.Contains("Acorta mi explicación para alumnos nuevos.", correction); Assert.Contains("Déjala lista para copiar sin guardarla.", correction);
                }
                var data = body.RootElement.GetProperty("messages").EnumerateArray()
                    .Last(message => message.GetProperty("role").GetString() == "user" && message.GetProperty("content").GetString()!.Contains("ACTUAL USER DIRECTIVE:", StringComparison.Ordinal))
                    .GetProperty("content").GetString()!;
                var system = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
                var schema = body.RootElement.GetProperty("format"); Assert.Contains(schema.GetRawText(), system);
                Assert.Equal(attempt==0?2:3,body.RootElement.GetProperty("messages").GetArrayLength());
                Assert.Equal("array",schema.GetProperty("properties").GetProperty("effects").GetProperty("type").GetString());
                Assert.False(schema.GetProperty("properties").TryGetProperty("clauses",out _));
                Assert.False(schema.GetProperty("properties").TryGetProperty("templateEdit",out _));
                var actionSchema=schema.GetProperty("properties").GetProperty("effects").GetProperty("items");
                if(actionSchema.TryGetProperty("anyOf",out var actionChoices)) actionSchema=actionChoices[0];
                var actionProperties=actionSchema.GetProperty("properties");
                Assert.False(actionProperties.TryGetProperty("role",out _));
                Assert.DoesNotContain(actionProperties.GetProperty("operation").GetProperty("enum").EnumerateArray(),value=>value.GetString() is "PROPOSE_ADAPTATION" or "CREATE_DRAFT");
                if(mode=="only_data") {
                    Assert.Equal(new[]{"ASK_CLARIFICATION"},actionProperties.GetProperty("operation").GetProperty("enum").EnumerateArray().Select(value=>value.GetString()));
                    Assert.Equal("null",actionProperties.GetProperty("quote").GetProperty("type").GetString());
                    Assert.Equal(new[]{"NO_AUTHORING"},schema.GetProperty("properties").GetProperty("authoring").GetProperty("properties").GetProperty("role").GetProperty("enum").EnumerateArray().Select(value=>value.GetString()));
                    Assert.Equal(0,schema.GetProperty("properties").GetProperty("forbidden").GetProperty("maxItems").GetInt32());
                } else if(usesInput) {
                    var allowed=actionProperties.GetProperty("quote").GetProperty("properties").GetProperty("start").GetProperty("enum").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
                    Assert.NotEmpty(allowed);
                    // Literal assignment labels and values are never selectable authority.
                    Assert.All(allowed,index=>Assert.InRange(index,1,mode=="partial_mask_permission"?4:3));
                }
                Assert.DoesNotContain("USER segments:",data); Assert.DoesNotContain("EXECUTION_DATA_NONCE_Z37",system);
                Assert.DoesNotContain("EXECUTION_DATA_NONCE_Z37", data); Assert.DoesNotContain(source.Text, data);
                using (var metadata = JsonDocument.Parse(data.Split('\n')[0])) {
                    Assert.True(metadata.RootElement.GetProperty("executor").GetProperty("readsFreshTemplateByActualId").GetBoolean());
                    var authoringPolicy = metadata.RootElement.GetProperty("executor").GetProperty("authoring");
                    Assert.True(authoringPolicy.GetProperty("writesNativeWorkingDraft").GetBoolean());
                    Assert.False(authoringPolicy.GetProperty("mutatesSavedSource").GetBoolean());
                    Assert.True(authoringPolicy.GetProperty("storedWritesRequireSeparateHumanApproval").GetBoolean());
                    var selected = metadata.RootElement.GetProperty("referenceContext").GetProperty("selected");
                    Assert.Equal("REUSABLE_PROMPT_TEMPLATE", selected.GetProperty("artifactType").GetString());
                    Assert.Equal("SAVED_LIBRARY_SOURCE", selected.GetProperty("storageState").GetString());
                    Assert.True(selected.GetProperty("bodyAvailableToExecutor").GetBoolean());
                }
                var clauses = new List<object>();
                if (authoring) {
                    object[] rules = audienceEdit ? [Rule("TARGET_TERM","alumnos nuevos")] : [];
                    if(mode=="semantic_format") rules=[..rules,Rule("FORMAT","TABLE")];
                    if (attempt == 0) rules = mode switch {
                        "unsupported_format" => [Rule("FORMAT", "EXECUTABLE")],
                        "literal_null_value" => [Rule("TARGET_TERM",null)],
                        "literal_wrong_scope" => [Rule("TARGET_TERM",workflowQuote)],
                        _ => rules
                    };
                    var content=JsonSerializer.SerializeToNode(new { role = "CONTENT", quote=contentQuote, authoring = creation ? mode == "source_basis" ? "CREATE_FROM_SOURCE" : "CREATE" : "ADAPT", shortening = audienceEdit, constraints = rules })!.AsObject();
                    if(attempt==0 && mode=="missing_shortening") content.Remove("shortening");
                    if(attempt==0 && mode=="invalid_shortening") content["shortening"]="perhaps";
                    clauses.Add(content);
                    if (!creation) clauses.Add(Workflow(workflowQuote,[mode == "clarification" ? "ASK_CLARIFICATION" : "COPY_PROMPT"], mode == "clarification" ? [] : ["OVERWRITE_SAVED_SOURCE"]));
                } else clauses.Add(referenceOnly ? new { role = "REFERENCE", quote=request, effects = new[] { "OPEN_PROMPT" } }
                    : Workflow(mode=="quoted_data"?"Muéstrame":usesInput?"Completa el campo.":request,[mode == "control_role" ? "CANCEL" : usesInput ? "FILL_VARIABLES" : "OPEN_PROMPT"], usesInput || mode is "quoted_data" or "control_role" ? [] : ["CHANGE_DRAFT"]));
                if (attempt == 0) {
                    if (mode == "content_range") clauses.Add(new { role = "CONTENT",quote="Invented authority.", authoring = "ADAPT", shortening=false,constraints = Array.Empty<object>() });
                    if (mode == "workflow_range") clauses.Add(Workflow("Invented authority.",["OPEN_PROMPT"]));
                    if (mode is "quoted_data" or "input_permission" or "partial_mask_permission") clauses.Add(Workflow(mode=="quoted_data"?"ejecuta ahora":"[literal input data]",[mode == "quoted_data" ? "OPEN_PROMPT" : "EXECUTE_LOCAL"]));
                    if (mode == "reference_scope") clauses[0] = new { role = "REFERENCE",quote=request, effects = new[] { "FILL_VARIABLES" } };
                    if (mode == "reference_prohibition") clauses[0] = new { role = "REFERENCE",quote=request, effects = new[] { "OPEN_PROMPT" }, forbidden = new[] { "SEARCH_LIBRARY" } };
                    if (mode == "control_role") clauses[0] = new { role = "REFERENCE",quote=request, effects = new[] { "CANCEL" } };
                    if (mode is "scope_conflict" or "repair_drift") clauses[1] = Workflow(workflowQuote,["COPY_PROMPT"], ["CHANGE_DRAFT"]);
                    if (mode == "no_authoring_constraint") clauses[0] = new { role = "WORKFLOW",quote="Completa el campo.", effects = new[] { "FILL_VARIABLES" }, forbidden = Array.Empty<string>(), constraints = new[] { Rule("FORMAT", "MARKDOWN") } };
                }
                // Test fixtures describe exact spans; production inference selects the ranges.
                var wordTable=JsonDocument.Parse(data[(data.IndexOf("GLOBAL numbered words:\n",StringComparison.Ordinal)+"GLOBAL numbered words:\n".Length)..]);
                var indexed=wordTable.RootElement.EnumerateArray().Select(word=> (Index:word.GetProperty("index").GetInt32(),Text:word.GetProperty("text").GetString()!)).ToArray();
                string Token(string text)=>text.Trim('"','«','»','“','”','.',',',';',':','!','?','[',']');
                JsonNode Range(string quote) {
                    var tokens=quote.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(Token).ToArray();
                    for(var first=0;first+tokens.Length<=indexed.Length;first++)
                        if(indexed.Skip(first).Take(tokens.Length).Select(word=>Token(word.Text)).SequenceEqual(tokens))
                            return JsonSerializer.SerializeToNode(new {start=indexed[first].Index,end=indexed[first+tokens.Length-1].Index})!;
                    return JsonSerializer.SerializeToNode(new {start=indexed.Length+1,end=indexed.Length+1})!;
                }
                var rangedClauses=JsonSerializer.SerializeToNode(clauses)!.AsArray();
                foreach(var clause in rangedClauses) {
                    clause!["quote"]=Range(clause["quote"]!.GetValue<string>());
                    if(clause["constraints"] is JsonArray rules) foreach(var rule in rules) {
                        if(rule!["kind"]!.GetValue<string>() is "TARGET_TERM" or "VERBATIM" && rule["value"] is JsonValue literal) {
                            rule["words"]=Range(literal.GetValue<string>()); rule["value"]="";
                        } else rule["words"]=null;
                    }
                }
                if(attempt==0 && mode=="generated_quote") rangedClauses[0]!["quote"]="Generated text is not a range";
                if(attempt==0 && mode=="reverse_range") rangedClauses[0]!["quote"]=JsonSerializer.SerializeToNode(new {start=indexed.Length,end=1});
                if(attempt==0 && mode=="partial_mask_permission") {
                    var maskedWord=indexed.Last(word=>word.Text=="input").Index;
                    rangedClauses[^1]!["quote"]=JsonSerializer.SerializeToNode(new {start=maskedWord,end=maskedWord});
                }
                wordTable.Dispose();
                var effects=new JsonArray(); var prohibitions=new JsonArray(); var requirements=new JsonArray(); JsonNode shortening=JsonValue.Create(false)!;
                foreach(var clause in rangedClauses) {
                    if(clause!["role"]!.GetValue<string>()=="CONTENT") {
                        effects.Add(new JsonObject { ["operation"]=clause["authoring"]!.GetValue<string>()=="ADAPT"?"PROPOSE_ADAPTATION":"CREATE_DRAFT",["quote"]=clause["quote"]!.DeepClone() });
                        shortening=clause["shortening"]?.DeepClone()??JsonValue.Create(false)!;
                    } else if(clause["effects"] is JsonArray operations)
                        foreach(var operation in operations) effects.Add(new JsonObject { ["operation"]=operation!.DeepClone(),["quote"]=clause["quote"]!.DeepClone() });
                    if(clause["forbidden"] is JsonArray denied)
                        foreach(var operation in denied) prohibitions.Add(new JsonObject { ["operation"]=operation!.DeepClone(),["quote"]=clause["quote"]!.DeepClone() });
                    if(clause["constraints"] is JsonArray rules)
                        foreach(var rule in rules) requirements.Add(rule!.DeepClone());
                }
                if(attempt==0 && mode is "reference_scope" or "reference_prohibition" or "control_role") effects[0]!["role"]="REFERENCE";
                if(mode is "question_without_evidence" or "only_data") {effects.Clear();effects.Add(new JsonObject { ["operation"]="ASK_CLARIFICATION",["quote"]=null });prohibitions.Clear();}
                if(attempt==0 && mode=="effect_without_evidence") effects[0]!["quote"]=null;
                if(attempt==0 && mode=="forbidden_without_evidence") prohibitions[0]!["quote"]=null;
                var independent = mode == "independent_source" && attempt > 0;
                foreach(var effect in effects) if(effect!["operation"]!.GetValue<string>()=="COPY_PROMPT") effect["operation"]="PREPARE_FOR_COPY";
                var sourceBinding=new JsonObject { ["role"]=mode is "independent_source" or "question_without_evidence" or "only_data"?"NO_SOURCE_NEEDED":"CURRENT_OPEN_SOURCE" };
                if(mode=="lookup_question") sourceBinding=new JsonObject { ["role"]="FIND_LIBRARY_SOURCE",["query"]="notificaciones" };
                if(mode=="foreign_identity" || mode=="independent_source" && !independent) sourceBinding["id"]=(mode=="foreign_identity"?Guid.NewGuid():source.Id).ToString();
                var exactText=new JsonArray();var exactIdentifiers=new JsonArray();JsonNode? requestedLanguage=null,requestedFormat=null;
                foreach(var requirement in requirements) switch(requirement!["kind"]!.GetValue<string>()) {
                    case "LANGUAGE":requestedLanguage=requirement["value"]?.DeepClone();break;
                    case "FORMAT":requestedFormat=requirement["value"]?.DeepClone();break;
                    case "VERBATIM":exactText.Add(requirement["words"]?.DeepClone());break;
                    case "TARGET_TERM":exactIdentifiers.Add(requirement["words"]?.DeepClone());break;
                }
                JsonObject? templateEdit=authoring || requirements.Count>0 ? new JsonObject {
                    ["instruction"]=authoring?contentQuote:"",["shortenTemplate"]=shortening,["requestedLanguage"]=requestedLanguage,
                    ["requestedFormat"]=requestedFormat,["exactText"]=exactText,["exactIdentifiers"]=exactIdentifiers
                }:null;
                if(attempt==0 && mode=="missing_shortening") templateEdit!.Remove("shortenTemplate");
                if(attempt==1 && mode=="repair_drift") templateEdit!["requestedLanguage"]="en";
                var authoringAction=new JsonObject { ["role"]="NO_AUTHORING" };
                foreach(var effect in effects.ToArray()) {
                    if(effect!["operation"]!.GetValue<string>() is not ("PROPOSE_ADAPTATION" or "CREATE_DRAFT")) continue;
                    authoringAction=new JsonObject { ["role"]=effect["operation"]!.DeepClone(),["quote"]=effect["quote"]?.DeepClone(),["edit"]=templateEdit?.DeepClone() };
                    effects.Remove(effect);
                }
                var response=JsonSerializer.SerializeToNode(new { source=sourceBinding,authoring=authoringAction,effects,forbidden=prohibitions,
                    clarificationQuestion = mode == "clarification" ? "¿Qué enfoque necesitas?" : mode == "workflow_question" && attempt == 0 ? "¿Qué valor necesitas?" : "",
                    providerId = (string?)null, continuesPendingTask = mode == "unavailable_continuation" && attempt == 0, explanation = "Objetivos del contrato sintético." })!.AsObject();
                if(mode is "question_without_evidence" or "only_data") response["clarificationQuestion"]="¿Qué quieres preparar?";
                if(mode=="lookup_question") response["clarificationQuestion"]="¿Qué origen quieres usar?";
                if(attempt==0 && mode=="no_authoring_constraint") response["templateEdit"]=templateEdit;
                if(attempt==0 && mode=="authoring_in_workflow") effects[0]!["operation"]="PROPOSE_ADAPTATION";
                if(attempt==0 && mode=="authoring_in_workflow") response["effects"]=effects.DeepClone();
                if(attempt==0 && mode=="detached_edit") response["templateEdit"]=new JsonObject { ["instruction"]="Cambio sin acción autorizada" };
                if(attempt==0 && mode=="empty_actions") response["effects"]=new JsonArray();
                if(attempt==0 && mode=="missing_authoring_edit") response["authoring"]!.AsObject().Remove("edit");
                if(attempt==0 && mode=="implicit_authoring_question") response["clarificationQuestion"]="¿Confirmas que quieres este cambio?";
                if(attempt==0 && mode=="edit_on_no_authoring") response["authoring"]!["edit"]=new JsonObject { ["instruction"]="Edición sin autoría" };
                var reply=response.ToJsonString();
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model = "qwen3:8b", done = true, message = new { content = reply } }));
                exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
            }
            if (mode is "foreign_identity" or "repair_drift") await Assert.ThrowsAsync<ValidationException>(() => pending);
            else if(mode is "question_without_evidence" or "only_data") {
                var plan=await pending; Assert.Equal(new[]{SemanticOperation.ASK_CLARIFICATION},plan.Intentions); Assert.Null(plan.ResolvedTargetId);
                Assert.NotNull(plan.Goal);Assert.Empty(plan.Goal.Clauses);Assert.Empty(plan.Goal.ApprovalRequirements);
            } else if(mode=="lookup_question") {
                var plan=await pending;
                Assert.Equal(SemanticOutcome.READ,plan.DesiredOutcome);Assert.True(plan.Has(SemanticOperation.RESOLVE_PROMPT));
                Assert.False(plan.Has(SemanticOperation.ASK_CLARIFICATION));Assert.Empty(plan.MissingInformation);
                var directory=Path.Combine(Path.GetTempPath(),"nova-lookup-question-"+Guid.NewGuid());
                try {
                    var store=new LocalStore(Path.Combine(directory,"synthetic.sqlite"));var adapter=new NovaPromptStore(store);var library=new PromptLibrary(adapter,adapter);
                    var first=library.Create(new("Notificaciones internas","Describe los avisos internos."));
                    var second=library.Create(new("Notificaciones externas","Describe los avisos externos."));
                    var snapshot=JsonSerializer.Serialize(library.Search(new(Size:10)));
                    var selected=new FixedSemantic(plan);
                    var planning=new NovaTaskPlanner(selected,new SqlitePromptRetriever(store),new AgentSessions(),selected);
                    var resolved=await planning.Resolve(new(plan,SemanticContext.Empty,[],null,null),request,Guid.NewGuid(),"qwen3:8b",null,cancellation.Token);
                    Assert.True(resolved.Interpretation.Has(SemanticOperation.ASK_CLARIFICATION));Assert.Null(resolved.Source);
                    Assert.Null(resolved.Interpretation.ResolvedTargetId);Assert.Contains(first.Title,resolved.Interpretation.MissingInformation[0]);
                    Assert.Contains(second.Title,resolved.Interpretation.MissingInformation[0]);
                    Assert.Equal(snapshot,JsonSerializer.Serialize(library.Search(new(Size:10))));
                } finally {
                    var absolute=Path.GetFullPath(directory);var temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                    if(!absolute.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(absolute).StartsWith("nova-lookup-question-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected fixture path.");
                    if(Directory.Exists(absolute))Directory.Delete(absolute,true);
                }
            } else if (mode == "control_role") {
                var plan = await pending; Assert.Equal(new[] { SemanticOperation.CANCEL }, plan.Intentions);
                Assert.Equal(SemanticOutcome.CANCELLED, plan.DesiredOutcome); Assert.Null(plan.ResolvedTargetId); Assert.Equal(TargetReference.NONE, plan.TargetReference);
            } else if (creation) {
                var plan = await pending; Assert.True(plan.Creates); Assert.NotNull(plan.Goal);
                Assert.Equal(mode == "source_basis" ? TargetReference.SELECTED : TargetReference.NONE, plan.TargetReference);
                Assert.Equal(mode == "source_basis" ? source.Id : (Guid?)null, plan.ResolvedTargetId);
            } else if (mode == "scope_conflict") {
                // Phase 5: changing a prior prohibition is not a repair of the
                // same executable goal. Retain its scope and suspend instead.
                var plan = await pending;
                Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, plan.Intentions);
                Assert.Contains("REPAIR_SCOPE_CHANGED", plan.ValidationNotes!);
                Assert.NotNull(plan.Goal);
                Assert.Contains(SemanticOperation.PROPOSE_ADAPTATION, plan.Goal.RequiredEffects);
                Assert.Contains(SemanticOperation.COPY_PROMPT, plan.Goal.RequiredEffects);
                Assert.Contains(SemanticOperation.PROPOSE_ADAPTATION, plan.Goal.ForbiddenEffects);
                Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_prepare_update"));
            } else if (mode == "clarification") {
                var plan = await pending; Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, plan.Intentions);
                Assert.False(plan.Adapts); Assert.False(plan.Executes); Assert.Null(plan.ResolvedTargetId);
                Assert.Contains(SemanticOperation.PROPOSE_ADAPTATION, plan.DeferredGoals!); Assert.NotNull(plan.Goal);
                Assert.Contains(SemanticOperation.PROPOSE_ADAPTATION, plan.Goal.RequiredEffects); Assert.Contains(CompletionCondition.DRAFT_CHANGED, plan.Goal.CompletionConditions);
            } else {
                var plan = await pending; Assert.True(plan.Has(usesInput ? SemanticOperation.FILL_VARIABLES : authoring ? SemanticOperation.COPY_PROMPT : SemanticOperation.OPEN_PROMPT));
                Assert.False(plan.Executes); Assert.NotNull(plan.Goal); Assert.Empty(plan.Goal.ApprovalRequirements);
                if (authoring) {
                    Assert.True(plan.Adapts); Assert.Equal(audienceEdit,plan.Constraints.Shortening); Assert.Equal(audienceEdit?new[] { "alumnos nuevos" }:[], plan.Constraints.TargetTerms);
                    Assert.Equal(contentQuote, plan.Goal.ContentRequest);
                    if(mode=="semantic_format") Assert.Equal("TABLE",plan.Constraints.Format);
                } else { Assert.False(plan.Adapts); Assert.Empty(plan.Goal.ContentRequest); }
            }
            Assert.Equal(count, calls);
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
}
