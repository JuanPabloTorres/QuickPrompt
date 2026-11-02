using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Tests;

// Controlled providers qualify app-owned structure and provenance only. Neither
// their output nor a valid body manufactures human content approval.
internal static class AuthoringReviewTestFixture
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);

    // Authoring exposes complete alternatives; other inference stages use one
    // object. Fixtures inspect field names without inventing a provider reply.
    internal static JsonElement SchemaFields(JsonElement schema) =>
        (schema.TryGetProperty("anyOf", out var branches) ? branches[0] : schema).GetProperty("properties");

    internal static PrepareInput Input(string request,PromptDocument? source=null,string[]? names=null)
    {
        var context=source is null?SemanticContext.Empty:SemanticContext.Empty with {Selected=SemanticEntity.From(source)};
        var operation=source is null?SemanticOperation.CREATE_DRAFT:SemanticOperation.PROPOSE_ADAPTATION;
        var selected=names??(source is null?TemplateCompiler.Names(request):[]);
        var plan=new LiveCommand(operation,request){InputNames=selected}.Compile(request,context);
        return new(Guid.NewGuid(),request,SourcePromptId:source?.Id,SourceRevision:source?.Revision){Interpretation=plan};
    }

    // Writer protocol tests deliberately choose adaptation. Creation now copies
    // the human contract and must never be made to call a writer by its wording.
    internal static PrepareInput WriterInput(string request, out RetrievalCandidate[] candidates, string[]? names = null)
    {
        var source = new PromptDocument(Guid.NewGuid(), "Plantilla", "Instrucción reutilizable",
            "Instrucción anterior elegida para mejorar.", 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow,
            0, null, [], new Dictionary<string, string>());
        candidates = [new(source, 1, "Fuente explícita de la adaptación controlada")];
        return Input(request, source, names ?? TemplateCompiler.Names(request));
    }

    internal static DraftProposalContract Bind(PrepareInput input,PromptDocument? source=null,AuthoringLiteralCandidate[]? literals=null)
    {
        var goal=input.Interpretation!.Goal!;var text=input.WorkingDraft?.TemplateText??source?.TemplateText??"";
        var fields=goal.InputContract??PromptInputContract.FromUserSelection(goal.UserRequest,text,input.Interpretation.Adapts,input.Interpretation.UserDefinedInputs);
        goal.SetInputContract(fields);
        var snapshot=source is null&&input.WorkingDraft is null?null:new DraftSourceSnapshot(source?.Id,source?.Revision,text,
            input.WorkingDraft?.Title??source!.Title,input.WorkingDraft?.Description??source!.Description,source?.CategoryId??0,
            source is null?null:LiveRuns.Hash(source.TemplateText),input.WorkingDraft?.Id,
            input.WorkingDraft is null?null:LiveRuns.Hash(input.WorkingDraft.TemplateText)) {SavedTemplateText=source?.TemplateText};
        var contract=new DraftProposalContract(goal.UserRequest,snapshot,fields,literalCandidates:literals);
        goal.BindAuthoringContract(contract);return contract;
    }

    internal static (DraftProposalContract Contract,PromptInput Draft) Register(PrepareInput input,string body,PromptDocument? source=null,
        string title="Artefacto",AuthoringLiteralCandidate[]? literals=null,int? literalReference=null)
    {
        var contract=Bind(input,source,literals);var draft=contract.RegisterProposal(new(title,body,"",source?.CategoryId??0),literalReference);
        return (contract,draft);
    }

    internal static PromptProposal Proposal(string body,string title="Artefacto",PromptDocument? source=null)=>
        new(source is null?PreparationDecision.CREATE_NEW:PreparationDecision.ADAPT_EXISTING,source?.Id,title,"",body,[],"","Controlled proposal; human review pending.");
    // An explicit model-fixture definition, never a production compatibility
    // adapter. Existing body oracles remain exact compact future instructions.
    internal static object Definition(string body,string title="Artefacto",string description="",int? literalReference=null)=>
        new {title,description,outcome=literalReference is null?"INSTRUCTION":"LITERAL",
            instruction=literalReference is null?new {task=body}:null,
            question=(string?)null,literalReference};
    internal static JsonElement Material(JsonElement packet)=>JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);

    // Explicit positive-provider fixtures for the new revision wire. Callers
    // choose the exact map; this never converts a provider response or removes
    // unknown/empty fields from a negative test.
    internal static object Revision(JsonElement packet, Dictionary<string, string?> exactMap) => AuthoringSyntheticReview.Revision(packet, exactMap);

    internal static object RewriteTask(JsonElement packet, string body) => AuthoringSyntheticReview.RewriteTask(packet, body);

    internal static Dictionary<string, string?> WriterFields(JsonElement packet) => Material(packet).GetProperty("fields").EnumerateArray()
        .ToDictionary(field => field.GetProperty("id").GetString()!, field => (string?)DecodeFixtureText(Material(packet), field.GetProperty("text").GetString()!));

    internal static string WriterText(JsonElement packet, string fieldId) => WriterFields(packet)[fieldId]!;

    internal static string EstablishedText(JsonElement packet, string fieldId)
    {
        var material = Material(packet);
        var text = material.GetProperty("establishedTask").EnumerateArray()
            .Single(field => field.GetProperty("id").GetString() == fieldId).GetProperty("text").GetString()!;
        return DecodeFixtureText(material, text);
    }

    private static string DecodeFixtureText(JsonElement material, string text)
    {
        foreach (var input in material.GetProperty("inputs").EnumerateArray())
            text = text.Replace(input.GetProperty("token").GetString()!, "<" + input.GetProperty("label").GetString() + ">", StringComparison.Ordinal);
        return text;
    }

    internal sealed class Transport(Func<JsonElement,string,int,object>? reply=null):IValidatedAgentModelTransport
    {
        // Calls retains the pre-discovery stage view (writer-only for authoring;
        // other ports keep their own calls). AllCalls is the actual total:
        // unconfirmed authoring now has discovery plus the writer.
        internal List<(JsonElement Packet,string Purpose,CancellationToken Token)> AllCalls {get;}=[];
        internal List<(JsonElement Packet,string Purpose,CancellationToken Token)> Calls =>
            AllCalls.Where(call => call.Purpose != "DiscoverCurrentAuthorDecision").ToList();
        internal List<(JsonElement Packet,string Purpose,CancellationToken Token)> DiscoveryCalls =>
            AllCalls.Where(call => call.Purpose == "DiscoverCurrentAuthorDecision").ToList();
        internal List<string?> Rejections {get;}=[];
        internal Exception? Failure {get;set;}
        internal Action? BeforeReply {get;set;}
        internal object? DecisionReply {get;set;}
        internal Func<JsonElement,object>? DecisionFactory {get;set;}
        public Task<JsonDocument> Send(object packet,CancellationToken ct,string purpose)
        {
            ct.ThrowIfCancellationRequested();var encoded=JsonSerializer.SerializeToElement(packet);AllCalls.Add((encoded,purpose,ct));
            if (AuthoringSyntheticReview.IsCurrentAuthorDecision(encoded))
                return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                    content = JsonSerializer.Serialize(DecisionReply ?? DecisionFactory?.Invoke(encoded)
                        ?? AuthoringSyntheticReview.CurrentAuthorDecisionReady(encoded), Json) } })));
            if(Failure is not null)throw Failure;
            var result=reply?.Invoke(encoded,purpose,Calls.Count)
                ?? throw new InvalidOperationException("Unexpected writer call: choose an explicit controlled revision reply.");
            BeforeReply?.Invoke();
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {message=new {content=JsonSerializer.Serialize(result,Json)}})));
        }
        public bool UsesThinking(string model)=>false;
        public void Observe(int stage,string content,string? error){}
        public void Reject(string? answer)=>Rejections.Add(answer);
    }

    internal sealed class ForbiddenDependencies:ILocalLlmClient,IInputValueExtractor,IDraftFidelityReviewer
    {
        private static Exception Unexpected()=>new InvalidOperationException("Production authoring must not call the retired editor, semantic reviewer, planner or an unrequested extraction.");
        public Task ReviewDraftFidelity(PrepareInput input,PromptProposal proposal,RetrievalCandidate[] candidates,CancellationToken ct,string? boundTitleLiteral=null,Action<int,string,string?>? diagnostic=null)=>throw Unexpected();
        public Task<AiProposal> Propose(string action,string context,Template? template,string model,CancellationToken ct)=>throw Unexpected();
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input,string template,string[] names,CancellationToken ct)=>throw Unexpected();
        public Task<string[]> Available(CancellationToken ct)=>throw Unexpected();
        public Task<AssistPlan> PlanAssist(string command,string model,CancellationToken ct)=>throw Unexpected();
        public Task<Dictionary<string,string>> Suggest(Template template,string context,string model,CancellationToken ct)=>throw Unexpected();
        public Task<(string Text,double ElapsedMs)> Execute(CompiledPrompt prompt,string model,bool confirmed,Func<string,Task>? progress,CancellationToken ct)=>throw Unexpected();
    }
}
