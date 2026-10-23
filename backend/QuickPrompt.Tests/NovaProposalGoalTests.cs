using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class NovaProposalGoalTests
{
    [Theory]
    [InlineData("pass")]
    [InlineData("repair")]
    [InlineData("reject")]
    [InlineData("foreign")]
    [InlineData("missing_pipeline")]
    public async Task VisibleAdaptationsUseTheAuthorizedGenerationPipelineWithoutLegacyBypass(string mode)
    {
        var directory=Path.Combine(Path.GetTempPath(),"nova-proposal-goal-"+Guid.NewGuid());
        try {
            var store=new LocalStore(Path.Combine(directory,"synthetic.sqlite"));var adapter=new NovaPromptStore(store);var library=new PromptLibrary(adapter,adapter);
            var source=library.Create(new("Base sintética","Describe <tema> con todos los detalles. No inventes datos."));
            const string request="Reduce la redacción conservando sus restricciones.";
            var plan=SemanticPlanCompiler.Compile(new([SemanticOperation.PROPOSE_ADAPTATION],TargetReference.SELECTED,"",source.Id,"",request,
                new([SemanticOperation.EXECUTE_LOCAL,SemanticOperation.REQUEST_SAVE_VERSION,SemanticOperation.REQUEST_SAVE_COPY],true,null,null,[],[],true),SemanticOutcome.DRAFT,[],false,"",null,"Contrato controlado."));
            plan=plan with {Goal=TaskGoal.Create(request,SemanticContext.Empty with {Selected=SemanticEntity.From(source)},plan,[new(TaskClauseRole.CONTENT,request){ContentChange=request}])};
            var legacy=new NoLegacyGeneration();var generator=new ControlledGenerator(mode);
            var assistant=new NovaAssistant(adapter,legacy,goalGenerator:mode=="missing_pipeline"?null:generator);
            var input=new ProposalInput("improve","UI echo is not new authority",PromptId:source.Id,Revision:source.Revision){AuthorizedTaskPlan=plan};
            if(mode is "reject" or "foreign" or "missing_pipeline") await Assert.ThrowsAsync<ValidationException>(()=>assistant.Propose(input,default));
            else {
                var result=await assistant.Propose(input,default);
                Assert.Equal(source.Id,result.TemplateId);Assert.Equal(source.Revision,result.BaseRevision);
                Assert.Equal("Resume <tema>. No inventes datos.",result.Text);
            }
            Assert.Equal(0,legacy.Calls);
            Assert.Equal(mode=="missing_pipeline"?0:mode=="pass"?1:2,generator.Inputs.Count);
            foreach(var captured in generator.Inputs) {
                Assert.Same(plan,captured.Interpretation);Assert.Equal(request,captured.Message);
                Assert.Equal(source.Id,captured.SourcePromptId);Assert.Equal(source.Revision,captured.SourceRevision);
            }
            if(generator.Inputs.Count==2) {
                using var feedback=JsonDocument.Parse(generator.Inputs[1].ValidationFeedback!);
                Assert.Equal(new[]{"tema"},feedback.RootElement.GetProperty("requiredVariableNames").EnumerateArray().Select(value=>value.GetString()));
                Assert.Contains("No inventes datos.",feedback.RootElement.GetProperty("requiredVerbatimConstraints").EnumerateArray().Select(value=>value.GetString()));
            }
            Assert.Equal(JsonSerializer.Serialize(source),JsonSerializer.Serialize(library.Get(source.Id)));Assert.Single(store.Versions(source.Id));
        } finally {
            var absolute=Path.GetFullPath(directory);var temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!absolute.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(absolute).StartsWith("nova-proposal-goal-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected fixture path.");
            if(Directory.Exists(absolute))Directory.Delete(absolute,true);
        }
    }
    [Fact] public void HttpJsonCannotSupplyTheServerOwnedTaskPlan()
    {
        var json=JsonSerializer.Serialize(new {intent="improve",context="untrusted",authorizedTaskPlan=new {goal=new {userRequest="invented authority"}}});
        var input=JsonSerializer.Deserialize<ProposalInput>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Null(input!.AuthorizedTaskPlan);
    }
    private sealed class ControlledGenerator(string mode):IPromptGenerator
    {
        public List<PrepareInput> Inputs {get;}=[];
        public Task<PromptProposal> Generate(PrepareInput input,Understanding understanding,RetrievalCandidate[] candidates,CancellationToken ct) {
            Inputs.Add(input);var source=Assert.Single(candidates).Prompt;
            if(mode=="reject"||mode=="repair"&&Inputs.Count==1)throw new ValidationException("CONTENT_FIDELITY_MISMATCH: el borrador omite el cambio solicitado.");
            return Task.FromResult(new PromptProposal(PreparationDecision.ADAPT_EXISTING,mode=="foreign"?Guid.NewGuid():source.Id,source.Title,source.Description,
                "Resume <tema>. No inventes datos.",[],"","Propuesta de prueba controlada."));
        }
    }
    private sealed class NoLegacyGeneration:INovaInference
    {
        public int Calls;
        public Task<NovaPlan> Interpret(string message,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<AiProposal> Propose(string intent,string context,Template? source,string model,CancellationToken ct) {Calls++;throw new InvalidOperationException("Goal-mode generation must use the validated pipeline.");}
        public Task<Dictionary<string,string>> Suggest(Template source,string context,string model,CancellationToken ct)=>throw new NotSupportedException();
    }
}
