using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class TaskGoalSourceTransportTests
{
    [Theory]
    [InlineData(false,"pass")]
    [InlineData(true,"pass")]
    [InlineData(false,"legacy_profile")]
    [InlineData(true,"legacy_profile")]
    [InlineData(false,"missing_purpose")]
    [InlineData(false,"lost_constraint")]
    [InlineData(false,"workflow")]
    [InlineData(false,"invented_evidence")]
    [InlineData(false,"unanchored_workflow")]
    [InlineData(false,"translated_title")]
    [InlineData(false,"unanchored_title")]
    [InlineData(false,"repaired_review")]
    [InlineData(false,"unanchored_issue")]
    [InlineData(false,"repaired_scope")]
    [InlineData(false,"repaired_workflow_scope")]
    [InlineData(false,"bound_literal_title")]
    [InlineData(false,"unanchored_authoring")]
    [InlineData(false,"echoed_authoring")]
    [InlineData(false,"unanchored_authoring_title")]
    [InlineData(false,"opaque_identifiers")]
    [InlineData(false,"missing_role_range")]
    [InlineData(false,"overlapping_role_range")]
    [InlineData(false,"unicode_title")]
    [InlineData(false,"repaired_authoring_ranges")]
    public async Task SourceBasedCreationReceivesCompleteSourceAndCannotBypassFidelityReview(bool sessionDraft,string outcome)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", taskGoals: true);
        var body = string.Concat(Enumerable.Repeat("Contexto sintético. ", 170)) + "TAIL_SOURCE_NONCE_A1. No inventes datos.";
        var description = new string('d', 650) + "DESC_TAIL_Q2";
        var source = new PromptDocument(Guid.NewGuid(), "Base", description, body, 0, false, false, 2, DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());
        var request = "Crea otra instrucción basada en la anterior para explicar un proceso."+(outcome is "translated_title" or "bound_literal_title"?" Titúlala Nombre literal.":outcome=="unicode_title"?" Titúlala Órbita 🛰️.":outcome=="echoed_authoring"?" No guardes.":"");
        var context = sessionDraft ? SemanticContext.Empty with { ActiveDraft = body } : SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], sessionDraft ? TargetReference.ACTIVE_DRAFT : TargetReference.SELECTED,
            "", sessionDraft ? null : source.Id, "", request, new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear desde una base autorizada."));
        if(outcome!="legacy_profile")plan = plan with { Goal = TaskGoal.Create(request, context, plan, [new(TaskClauseRole.CONTENT, request)]) };
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan,ApprovedPreferences=sessionDraft?new("es","MARKDOWN"):null,
            WorkingDraft = sessionDraft ? new(Guid.NewGuid(), PreparationState.READY, PreparationIntent.CREATE_PROMPT, PreparationDecision.CREATE_NEW, source.Title, description,
                body, body, [], [], [], "", "Base de sesión.", [], null, null, 0, false, DateTime.UtcNow) : null };
        var pending = inference.Generate(input, plan.Preparation(), sessionDraft ? [] : [new(source, 10, "Base real")], cancellation.Token);
        try {
            var exchange = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream);
            Assert.Equal(outcome == "legacy_profile", packet.RootElement.GetProperty("think").GetBoolean());
            using var material = JsonDocument.Parse(packet.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!);
            var definitionOnly = outcome == "legacy_profile";
            var requestUnits=definitionOnly ? [] : material.RootElement.GetProperty("requestUnits").EnumerateArray().ToArray();
            var lastUnit=requestUnits.Length-1;
            int Unit(string text)=>Array.FindIndex(requestUnits,unit=>unit.GetProperty("text").GetString()==text);
            var outsideStart=outcome is "bound_literal_title" or "unicode_title"?Unit("Titúlala"):outcome=="echoed_authoring"?Unit("No"):-1;
            var bodyRanges=new[]{new {start=0,end=outsideStart>=0?outsideStart-1:outcome is "missing_role_range" or "repaired_authoring_ranges"?lastUnit-1:lastUnit}};
            var outsideRanges=outcome=="unanchored_authoring"?new[]{new {start=-1,end=0}}:outcome=="overlapping_role_range"?new[]{new {start=0,end=0}}:outsideStart>=0?new[]{new {start=outsideStart,end=lastUnit}}:Array.Empty<object>();
            var titleRange=outcome=="bound_literal_title"?new {start=Unit("Nombre"),end=Unit("literal")}:outcome=="unicode_title"?new {start=Unit("Órbita"),end=Unit("🛰️")}:outcome=="unanchored_authoring_title"?new {start=0,end=lastUnit+1}:null;
            var actual = definitionOnly
                ? material.RootElement.GetProperty(sessionDraft ? "workingDefinition" : "sourceDefinition")
                : sessionDraft ? material.RootElement.GetProperty("workingInstruction") : material.RootElement.GetProperty("candidates")[0];
            Assert.Equal(body, actual.GetProperty("templateText").GetString());
            if (definitionOnly) {
                Assert.Equal(request, material.RootElement.GetProperty("originalAuthoringRequest").GetString());
                Assert.Equal(new[] { "title", "description", "templateText" }, packet.RootElement.GetProperty("format").GetProperty("properties").EnumerateObject().Select(property => property.Name));
                Assert.False(packet.RootElement.TryGetProperty("tools", out _));
                Assert.False(material.RootElement.TryGetProperty("values", out _));
            } else Assert.False(actual.GetProperty("truncated").GetBoolean());
            if (!sessionDraft) Assert.Equal(description, actual.GetProperty("description").GetString());
            var generated=outcome is "missing_purpose" or "missing_role_range" or "overlapping_role_range"?"Explica un tema. No inventes datos.":outcome=="lost_constraint"?"Explica el proceso.":outcome is "workflow" or "repaired_workflow_scope"?"Explica el proceso. No inventes datos. Abre la biblioteca y guarda el registro.":outcome=="echoed_authoring"?"Explica el proceso. No inventes datos. No guardes.":outcome=="opaque_identifiers"?"Read <factura>. Add its data to <hoja>. Do not invent data.":"Explica el proceso. No inventes datos.";
            object generatedReply = definitionOnly ? new { title = "Nuevo", description = "", templateText = generated } : new {
                decision = "CREATE_NEW", sourceId = (Guid?)null, title = "Nuevo", description = "", templateText = generated,
                authoringEvidence=new {bodyRanges,outsideRanges,titleRange},
                values = Array.Empty<object>(), requiredInputs = Array.Empty<string>(), clarification = "", reason = "Nueva instrucción basada en el material autorizado."
            };
            var reply = JsonSerializer.Serialize(new { model = "qwen3:8b", done = true, message = new { content = JsonSerializer.Serialize(generatedReply) } });
            var bytes = System.Text.Encoding.UTF8.GetBytes(reply); exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
            await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
            if(outcome is "missing_role_range" or "overlapping_role_range" or "repaired_authoring_ranges" or "unanchored_authoring" or "unanchored_authoring_title") {
                var repair=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
                using var repairPacket=await JsonDocument.ParseAsync(repair.Request.InputStream);
                Assert.False(repairPacket.RootElement.GetProperty("think").GetBoolean());
                Assert.False(repairPacket.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("templateText",out _));
                using var repairData=JsonDocument.Parse(repairPacket.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request,repairData.RootElement.GetProperty("request").GetString());
                var repairedBody=outcome=="repaired_authoring_ranges"?new[]{new {start=0,end=lastUnit}}:bodyRanges;
                var repairReply=JsonSerializer.Serialize(new {model="qwen3:8b",done=true,message=new {content=JsonSerializer.Serialize(new {
                    bodyRanges=repairedBody,outsideRanges,titleRange
                })}});
                var repairBytes=System.Text.Encoding.UTF8.GetBytes(repairReply);repair.Response.ContentLength64=repairBytes.Length;repair.Response.ContentType="application/json";
                await repair.Response.OutputStream.WriteAsync(repairBytes);repair.Response.Close();
            }
            if(outcome is "unanchored_authoring" or "echoed_authoring" or "unanchored_authoring_title") {
                await Assert.ThrowsAsync<ValidationException>(()=>pending); return;
            }
            if(outcome is "missing_role_range" or "overlapping_role_range") {
                var failure=await Assert.ThrowsAsync<ValidationException>(()=>pending);
                Assert.Equal(true,failure.Data["NovaStageRepairExhausted"]);
                Assert.False(failure.Data.Contains("NovaFullContextFallback"));
                Assert.Contains("No se aplicó el borrador",failure.Message);
                Assert.DoesNotContain("unidad",failure.Message);
                return;
            }
            var reviewAttempts=outcome is "unanchored_workflow" or "unanchored_title" or "repaired_review" or "unanchored_issue" or "invented_evidence" or "repaired_scope" or "repaired_workflow_scope" ? 2 : 1;
            for(var reviewAttempt=0;reviewAttempt<reviewAttempts;reviewAttempt++) {
            var reviewExchange=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var reviewPacket=await JsonDocument.ParseAsync(reviewExchange.Request.InputStream);
            var responseProperties=reviewPacket.RootElement.GetProperty("format").GetProperty("properties");
            var metadataOnly=responseProperties.EnumerateObject().Count()==1&&responseProperties.TryGetProperty("requestedTitleQuote",out _);
            Assert.Equal(!metadataOnly,reviewPacket.RootElement.GetProperty("think").GetBoolean());Assert.False(reviewPacket.RootElement.TryGetProperty("tools",out _));
            using var reviewData=JsonDocument.Parse(reviewPacket.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(request,reviewData.RootElement.GetProperty("authorizedUserRequest").GetString());
            if(!metadataOnly) {
            Assert.Equal(body,reviewData.RootElement.GetProperty("sourceInstruction").GetString());
            Assert.Equal(generated,reviewData.RootElement.GetProperty("proposedInstruction").GetString());
            }
            if(outcome is not ("bound_literal_title" or "unicode_title")) Assert.Equal(source.Title,reviewData.RootElement.GetProperty("sourceTitle").GetString());
            if(outcome=="opaque_identifiers") {
                Assert.Equal(new[]{"factura","hoja"},reviewData.RootElement.GetProperty("opaqueInputIdentifiers").EnumerateArray().Select(value=>value.GetString()));
                var sample=reviewData.RootElement.GetProperty("bodyProseForLanguageCheck");
                Assert.False(sample.GetProperty("truncated").GetBoolean());
                Assert.DoesNotContain("factura",sample.GetProperty("text").GetString());
                Assert.DoesNotContain("hoja",sample.GetProperty("text").GetString());
                Assert.Contains("Read",sample.GetProperty("text").GetString());
            }
            if(outcome is "bound_literal_title" or "unicode_title") {
                Assert.False(reviewData.RootElement.TryGetProperty("proposedTitle",out _));
                Assert.True(reviewData.RootElement.GetProperty("metadataAlreadyValidated").GetProperty("literalTitleMatched").GetBoolean());
            }
            Assert.False(reviewData.RootElement.TryGetProperty("validationFeedback",out _));
            Assert.False(reviewData.RootElement.TryGetProperty("applicationEffects",out _));
            if (!metadataOnly) Assert.Equal(plan.Intentions.Order(), reviewData.RootElement.GetProperty("currentApplicationOperations").EnumerateArray()
                .Select(operation => Enum.Parse<SemanticOperation>(operation.GetProperty("operation").GetString()!)).Order());
            if(metadataOnly) {
                Assert.True(reviewAttempt>0);
                Assert.Equal(new[]{"authorizedUserRequest","sourceTitle"},reviewData.RootElement.EnumerateObject().Select(property=>property.Name));
            }
            else Assert.Equal(reviewAttempt>0,reviewPacket.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!.Contains("REVIEW_CONTRACT_REPAIR:"));
            if(sessionDraft) {
                Assert.Equal("es",reviewData.RootElement.GetProperty("approvedContentPreferences").GetProperty("language").GetString());
                Assert.Equal("MARKDOWN",reviewData.RootElement.GetProperty("approvedContentPreferences").GetProperty("format").GetString());
            }
            var issue=outcome switch {
                "missing_purpose" or "missing_role_range" or "overlapping_role_range"=>new {authority="USER_REQUEST",problem="El borrador omite el propósito solicitado.",evidence="explicar un proceso"},
                "lost_constraint"=>new {authority="SOURCE_INSTRUCTION",problem="Falta una restricción del origen.",evidence="No inventes datos."},
                "workflow"=>new {authority="USER_REQUEST",problem="El borrador incluye acciones de la biblioteca.",evidence=request},
                "unanchored_issue"=>new {authority="USER_REQUEST",problem="Un diagnóstico no es un requisito del usuario.",evidence="validationFeedback text"},
                _=>new {authority="INVENTED_AUTHORITY_NONCE",problem="Requisito ajeno al usuario y al origen.",evidence="INVENTED"}
            };
            var declaration=JsonSerializer.SerializeToElement(new {
                verdict=outcome is "missing_purpose" or "lost_constraint" or "workflow" or "invented_evidence" or "unanchored_issue" or "missing_role_range" or "overlapping_role_range"?"REVISE":"ACCEPT",
                instructionScope=outcome=="workflow"||outcome=="repaired_workflow_scope"&&reviewAttempt>0?"MIXED_ACTORS":"FUTURE_TASK_ONLY",
                applicationWorkflowQuotes=outcome is "workflow" or "repaired_workflow_scope"?new[]{"Abre la biblioteca y guarda el registro."}:outcome=="unanchored_workflow"?new[]{"Texto ausente del cuerpo."}:outcome=="repaired_scope"&&reviewAttempt==0?new[]{"Explica el proceso."}:Array.Empty<string>(),
                requestedTitleQuote=outcome=="translated_title"?"Nombre literal":outcome=="unanchored_title"?"Nombre ausente":outcome=="repaired_review"&&reviewAttempt==0?source.Title:null,
                issues=outcome is "pass" or "legacy_profile" or "unanchored_workflow" or "translated_title" or "unanchored_title" or "repaired_review" or "repaired_scope" or "repaired_workflow_scope" or "bound_literal_title" or "opaque_identifiers" or "unicode_title" or "repaired_authoring_ranges"?Array.Empty<object>():new object[]{issue}
            });
            // The reviewer repairs only the invalid independent group. A fixture must obey
            // that actual schema rather than return a stale full review or wait until timeout.
            var scopedReply=declaration.EnumerateObject().Where(property=>responseProperties.TryGetProperty(property.Name,out _))
                .ToDictionary(property=>property.Name,property=>property.Value.Clone());
            var reviewReply=JsonSerializer.Serialize(new {model="qwen3:8b",done=true,message=new {content=JsonSerializer.Serialize(scopedReply)}});
            var reviewBytes=System.Text.Encoding.UTF8.GetBytes(reviewReply);reviewExchange.Response.ContentType="application/json";reviewExchange.Response.ContentLength64=reviewBytes.Length;
            await reviewExchange.Response.OutputStream.WriteAsync(reviewBytes);reviewExchange.Response.Close();
            }
            if(outcome is "pass" or "legacy_profile" or "repaired_review" or "repaired_scope" or "bound_literal_title" or "opaque_identifiers" or "unicode_title" or "repaired_authoring_ranges") {
                var result=await pending;Assert.Equal(PreparationDecision.CREATE_NEW,result.Decision);
                if(outcome=="unicode_title")Assert.Equal("Órbita 🛰️",result.Title);
            }
            else {
                var failure=await Assert.ThrowsAsync<ValidationException>(()=>pending);
                if(outcome is "unanchored_workflow" or "unanchored_title" or "unanchored_issue") {
                    Assert.Equal("CONTENT_FIDELITY_REVIEW_INVALID",failure.Data["NovaValidationCode"]);
                    Assert.Equal(true,failure.Data["NovaStageRepairExhausted"]);
                }
                else if(outcome!="invented_evidence")Assert.Equal("CONTENT_FIDELITY_MISMATCH",failure.Data["NovaValidationCode"]);
                Assert.DoesNotContain("CONTENT_FIDELITY",failure.Message);
            }
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
}
