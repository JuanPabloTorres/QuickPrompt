using System.Net;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptInputContractTests
{
    private const string Request="Crea un prompt para una propuesta. Variables: proyecto, propósito, tipo.";
    private static string[] Fields=>["proyecto","propósito","tipo"];
    private static PromptInputContract Contract(string request=Request,string source="",bool updating=false)
        =>PromptInputContract.FromUserSelection(request,source,updating,Fields);
    private static SemanticInterpretation Plan()=>new LiveCommand(SemanticOperation.CREATE_DRAFT,Request){InputNames=Fields}.Compile(Request,SemanticContext.Empty);

    [Fact]
    public async Task SelectedFieldsExistBeforeReviewAndAreMissingValuesRatherThanAFalseReady()
    {
        var plan=Plan();var reviewer=new Reviewer();
        var generator=new PromptDraftGenerator(new Author(),new Editor(),new Extractor(),reviewer);
        var input=new PrepareInput(Guid.NewGuid(),Request){Interpretation=plan};
        var proposal=await generator.GenerateAfterSyntheticReview(input,plan.Preparation(),[],default);
        var draft=new DeterministicPromptEvaluator().Evaluate(input,plan.Preparation(),proposal,[],[]);
        Assert.Equal(Fields,draft.Variables.Select(v=>v.Name));
        Assert.Equal(PreparationState.NEEDS_INFORMATION,draft.State);
        Assert.Equal(3,draft.MissingVariables.Length);Assert.Empty(draft.Values);Assert.Null(draft.CompiledText);
        Assert.Null(reviewer.Body);Assert.True(plan.Goal!.HasAuthoringStructure(draft.TemplateText));Assert.False(plan.Goal.HasHumanContentApproval(draft.TemplateText));
        var contract=plan.Goal!.InputContract;
        await generator.GenerateAfterSyntheticReview(input,plan.Preparation(),[],default);
        Assert.Same(contract,plan.Goal.InputContract);
    }

    [Fact]
    public async Task UndefinedInputsAskForNamesWithoutCallingTheModelOrInventingValues()
    {
        var plan=new LiveCommand(SemanticOperation.CREATE_DRAFT,Request).Compile(Request,SemanticContext.Empty);
        var author=new Author();var reviewer=new Reviewer();
        var proposal=await new PromptDraftGenerator(author,new Editor(),new Extractor(),reviewer)
            .GenerateAfterSyntheticReview(new(Guid.NewGuid(),Request){Interpretation=plan},plan.Preparation(),[],default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION,proposal.Decision);
        Assert.Equal("INPUT_FIELDS_NEED_CONFIRMATION",proposal.Reason);
        Assert.Contains("nombres",proposal.Clarification.ToLowerInvariant());
        Assert.Empty(proposal.TemplateText);Assert.Empty(proposal.Values);Assert.Equal(0,author.Calls);Assert.Null(reviewer.Body);
    }

    [Fact]
    public async Task UnrequestedSlotsCannotReachAnAcceptingReviewer()
    {
        var plan=Plan();var review=new Reviewer();
        var generator=new PromptDraftGenerator(new Author("Usa <intruso>."),new Editor(),new Extractor(),review);
        var failure=await Assert.ThrowsAsync<ValidationException>(()=>generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(),Request){Interpretation=plan},plan.Preparation(),[],default));
        Assert.Contains("Campos no autorizados",failure.Message);Assert.Contains("intruso",failure.Message);
        Assert.Null(review.Body);
    }

    [Fact]
    public void FieldsAreNotDerivedFromTheGeneratedTextOrItsEmptyMetadata()
    {
        var plan=Plan();plan.Goal!.SetInputContract(Contract());
        var draft=new DeterministicPromptEvaluator().Evaluate(new(Guid.NewGuid(),Request),plan.Preparation(),
            new(PreparationDecision.CREATE_NEW,null,"Tarea","","Redacta la propuesta.",[],"","Propuesta.",[]),[],[]);
        Assert.Contains("REQUESTED_INPUTS_MISSING_OR_EXTRA",PromptPreparationPipeline.Validation(draft,new(null,null,[]),plan.Goal).Errors);
        Assert.Contains("proyecto",PromptPreparationPipeline.CorrectionFeedback(["REQUESTED_INPUTS_MISSING_OR_EXTRA"],new(null,null,[]),"",requiredInputs:plan.Goal.InputContract!.Names));
    }

    [Fact]
    public void AddingFieldsCanPreserveTheExactSourceAndItsRestrictions()
    {
        const string source="Explica <tema>. No inventes datos.";
        var contract=Contract(source:source,updating:true);
        var result=contract.Bind(source,source);
        Assert.StartsWith(source,result);Assert.Equal(new[]{"tema","proyecto","propósito","tipo"},TemplateCompiler.Names(result));
        Assert.Throws<ValidationException>(()=>contract.Bind(source,"Prepara <intruso>."));
        Assert.Throws<ValidationException>(()=>contract.ValidateBasis(Request,source+" Otra revisión."));
    }

    [Fact]
    public void ExplicitlyEmptySelectionPreservesExistingFieldsAndForbidsNewOnes()
    {
        const string source="Explica <tema>.";
        var preserve=PromptInputContract.FromUserSelection("Mejora el texto.",source,true,[]);
        Assert.Equal(PromptInputPolicy.PRESERVE,preserve.Policy);
        Assert.Throws<ValidationException>(()=>preserve.Bind(source,"Explica <tema> a <persona>."));
        var empty=PromptInputContract.FromUserSelection("Responde LISTO.","",false,[]);
        Assert.Equal("Responde LISTO.",empty.Bind("","Responde LISTO."));
        Assert.Throws<ValidationException>(()=>empty.Bind("","Responde <saludo>."));
        Assert.Equal(PromptInputPolicy.CLARIFY,PromptInputContract.FromUserSelection("Responde LISTO.","",false,null).Policy);
    }

    [Fact]
    public void UserControlsAreAnIndependentImmutableAuthorityNotModelProse()
    {
        string[] selected=["audiencia","restricciones"];
        var contract=PromptInputContract.FromUserSelection("Agrega los campos indicados en el formulario.","Explica <tema>.",true,selected);
        selected[0]="intruso";
        Assert.Equal(new[]{"tema","audiencia","restricciones"},contract.Names);
        Assert.All(contract.Inputs,input=>Assert.Equal("USER_FIELD_CONTROL",input.Evidence));
        Assert.Throws<ValidationException>(()=>PromptInputContract.FromUserSelection(Request,"",false,["tipo","tipo"]));
        Assert.Throws<ValidationException>(()=>PromptInputContract.FromUserSelection(Request,"",false,[" tipo "]));
        Assert.Throws<ValidationException>(()=>PromptInputContract.FromUserSelection(Request,"",false,["<tipo>"]));
    }

    [Fact]
    public void OnlyAuthoringCommandsCanDeclareInputNames()
    {
        var command=new LiveCommand(SemanticOperation.SEARCH_PROMPT,"buscar"){InputNames=["tipo"]};
        Assert.Throws<ValidationException>(()=>command.Validate(new(Guid.NewGuid(),Guid.NewGuid(),"buscar",Command:command)));
    }

    [Fact]
    public async Task ExistingEditorAcceptsAuthorizedAdditionsWithItsRealBindingAndValidation()
    {
        const string source="Explica <tema>.";
        using var http=new HttpClient(new EditorReply());
        var result=await new OllamaClient(http).Propose("improve",Request,new(){Title="Origen",Template=source},"qwen3:8b",default,false,false,
            new TemplateEditInterpretation("Prepara una propuesta."){InputContract=Contract(source:source,updating:true)});
        Assert.Equal(new[]{"tema","proyecto","propósito","tipo"},TemplateCompiler.Names(result.Text));
    }

    private sealed class Author(string body="Redacta una propuesta detallada.") : IValidatedAgentModelTransport
    {
        public int Calls; public int DiscoveryCalls;
        public Task<JsonDocument> Send(object packet,CancellationToken ct,string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            var encoded = JsonSerializer.SerializeToElement(packet);
            object reply;
            if (AuthoringSyntheticReview.IsCurrentAuthorDecision(encoded)) {
                DiscoveryCalls++;
                reply = AuthoringSyntheticReview.CurrentAuthorDecisionReady();
            } else reply = AuthoringReviewTestFixture.Definition(body, "Propuesta");
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {message=new {
                content=JsonSerializer.Serialize(reply,AuthoringReviewTestFixture.Json)}})));
        }
        public bool UsesThinking(string model)=>false;
        public void Observe(int stage,string content,string? error){}
        public void Reject(string? answer){}
    }
    private sealed class Reviewer : IDraftFidelityReviewer
    {
        public string? Body;
        public Task ReviewDraftFidelity(PrepareInput input,PromptProposal proposal,RetrievalCandidate[] candidates,CancellationToken ct,string? boundTitleLiteral=null,Action<int,string,string?>? diagnostic=null)
        {Body=proposal.TemplateText;throw new InvalidOperationException("Production proposal must not invoke a retired reviewer.");}
    }
    private sealed class Extractor : IInputValueExtractor
    { public Task<InputValueExtraction> ExtractInputs(PrepareInput input,string template,string[] names,CancellationToken ct)=>throw new InvalidOperationException("Do not fill a future template's inputs."); }
    private sealed class Editor : ILocalLlmClient
    {
        public Task<string[]> Available(CancellationToken ct)=>throw new NotSupportedException();
        public Task<AiProposal> Propose(string action,string context,Template? template,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<Dictionary<string,string>> Suggest(Template template,string context,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<(string Text,double ElapsedMs)> Execute(CompiledPrompt prompt,string model,bool confirmed,Func<string,Task>? progress,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class EditorReply : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            using var packet=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.False(packet.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("variablesPreserved",out _));
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new {done=true,message=new {content=JsonSerializer.Serialize(new {
                text="Prepara una propuesta para <tema>.",reasons=new[]{"Añadir campos pedidos."},changes=new[]{"Nueva propuesta."},warnings=Array.Empty<string>()
            })}}),Encoding.UTF8,"application/json")};
        }
    }
}
