using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// Synthetic component diagnostic. It exercises the real generator, not UI or persistence.
public static class GenerationDiagnostic
{
    private record PlanningCase(string Id,string Message,Guid SelectedId);
    private record PlanningDataset(bool SyntheticOnly,PlanningCase[] Cases);
    public static async Task Plan(string[] args)
    {
        if(args.Length!=3)throw new ArgumentException("Pass synthetic SQLite, case dataset and report path.");
        using var lease=new FileStream(Path.Combine(Path.GetTempPath(),"QuickPrompt.Nova.SemanticModel.lease"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var dataset=JsonSerializer.Deserialize<PlanningDataset>(File.ReadAllText(args[1]),json)!;
        if(!dataset.SyntheticOnly)throw new ArgumentException("Synthetic fixtures required.");
        var store=new LocalStore(args[0]);var adapter=new NovaPromptStore(store);var library=new PromptLibrary(adapter,adapter);
        var rows=new List<object>();
        foreach(var item in dataset.Cases) {
            var stages=new List<object>();var sessions=new AgentSessions();var sessionId=Guid.NewGuid();
            sessions.Put(new(sessionId,library.Get(item.SelectedId),null,[],DateTime.UtcNow));
            using var inference=new PreparationInference("http://127.0.0.1:11434",reuseValidatedInference:false);
            void Report(object? result=null)=>File.WriteAllText(args[2],JsonSerializer.Serialize(new {scope="PLANNING_COMPONENT_ONLY",rows,current=new {item.Id,item.Message,stages,result}},json));
            inference.SemanticDiagnostic=(stage,content,error)=>{stages.Add(new {stage,content,error});Report();};
            var planning=new NovaTaskPlanner(inference,new SqlitePromptRetriever(store),sessions,inference);
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try {
                var resolution=await planning.Interpret(item.Message,sessionId,null,null,"qwen3:8b",null,timeout.Token);
                resolution=await planning.Resolve(resolution,item.Message,sessionId,"qwen3:8b",null,timeout.Token);
                rows.Add(new {item.Id,item.Message,stages,plan=resolution.Interpretation,sourceId=resolution.Source?.Id,
                    scopedValues=resolution.Interpretation.InputScope?.Values(item.Message,resolution.Source?.VariableNames??[]),error=(string?)null});
            } catch(Exception failure){rows.Add(new {item.Id,item.Message,stages,error=failure.Message});}
            Report();Console.WriteLine($"{item.Id}: DIAGNOSTIC_COMPLETE");
        }
    }
    private record Case(string Id, string Message, LiveCommand Command, string? Source, string? SourceTitle = null,
        string? ReviewOnlyBody=null,string[]? ReviewRequirements=null,string? ExpectedReview=null);
    private record Dataset(bool SyntheticOnly, Case[] Cases);
    public static async Task Run(string[] args)
    {
        if (args.Length != 2 && !(args.Length==3&&args[2] is "--direct-review" or "--constraints-only"))
            throw new ArgumentException("Pass a synthetic generation dataset, output path, and optional --direct-review or --constraints-only.");
        var constraintsOnly=args.Length==3&&args[2]=="--constraints-only";
        var model=Environment.GetEnvironmentVariable("NOVA_QA_MODEL")??"qwen3:8b";
        var endpoint=Environment.GetEnvironmentVariable("NOVA_QA_OLLAMA")??"http://127.0.0.1:11434";
        OllamaClient.ValidateEndpoint(endpoint);
        if(!OllamaClient.Models.Contains(model))throw new ArgumentException("The diagnostic requires an existing supported local model.");
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var data = JsonSerializer.Deserialize<Dataset>(File.ReadAllText(args[0]), json)!;
        if (!data.SyntheticOnly) throw new ArgumentException("Synthetic data required.");
        var rows = new List<object>();
        var failedReviewExpectations=false;
        using var inference = new PreparationInference(endpoint, reuseValidatedInference: false,semanticThinking:args.Length==2||constraintsOnly);
        foreach (var item in data.Cases) {
            if(item.ExpectedReview is not null && (item.ReviewOnlyBody is null || item.ExpectedReview is not ("ACCEPT" or "REJECT")))
                throw new ArgumentException("An expected review requires a controlled body and ACCEPT/REJECT.");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var stages = new List<object>();
            inference.SemanticDiagnostic = (stage, content, _) => {
                stages.Add(new { stage, contract = JsonSerializer.Deserialize<JsonElement>(content) });
                File.WriteAllText(args[1], JsonSerializer.Serialize(new { scope="GENERATION_COMPONENT_ONLY",rows,current=new {item.Id,stages}},json));
            };
            var source = item.Source is null ? null : new PromptDocument(Guid.NewGuid(), item.SourceTitle ?? "Base sintética", "", item.Source, 0, false, false, 1,
                DateTime.UnixEpoch, DateTime.UnixEpoch, 0, null, TemplateCompiler.Names(item.Source), new Dictionary<string, string>());
            var plan = item.Command.Compile(item.Message, source is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(source) });
            PromptProposal? proposal = null; string? error = null; string? errorCode = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try {
                var input=new PrepareInput(Guid.NewGuid(),item.Message,Model:model,SourcePromptId:source?.Id,SourceRevision:source?.Revision){Interpretation=plan};
                RetrievalCandidate[] candidates=source is null?[]:[new(source,10,"Origen sintético")];
                if(constraintsOnly) {
                    var constraints=await inference.ExtractContent(item.Message,SemanticContext.Empty,model,timeout.Token);
                    stages.Add(new {stage=180,contract=JsonSerializer.SerializeToElement(constraints)});
                }
                else if(item.ReviewOnlyBody is null) proposal=await inference.Generate(input,plan.Preparation(),candidates,timeout.Token);
                else {
                    // QA-only controlled candidate: execute the unchanged private production
                    // review, without allowing this fixture to enter UI or persistence.
                    var candidate=new PromptProposal(PreparationDecision.CREATE_NEW,null,"Borrador sintético","",item.ReviewOnlyBody,[],"","Revisión controlada.");
                    var method=typeof(PreparationInference).GetMethod("ReviewDraftFidelity",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
                    // Fixture-selected requirements are observations, never the user's authority.
                    await (Task)method.Invoke(inference,[input,candidate,candidates,timeout.Token,null])!;
                    proposal=candidate;
                }
            }
            catch (Exception failure) {
                var actual=failure is System.Reflection.TargetInvocationException {InnerException:not null} wrapped?wrapped.InnerException!:failure;
                error=actual.Message;
                errorCode=actual is PreparationToolException tool?tool.Code
                    :actual.Data["NovaValidationCode"] as string??(actual is OperationCanceledException?"CANCELLED_OR_TIMEOUT":actual.GetType().Name);
            }
            var reviewOutcome=item.ReviewOnlyBody is null?null:proposal is not null?"REVIEW_ACCEPTED"
                :errorCode=="CONTENT_FIDELITY_MISMATCH"?"CONTENT_REJECTED":"REVIEW_INCONCLUSIVE";
            bool? reviewConforms=item.ExpectedReview is null?null:(reviewOutcome is "REVIEW_ACCEPTED" or "CONTENT_REJECTED")
                && (proposal is not null)==(item.ExpectedReview=="ACCEPT");
            if(reviewConforms==false)failedReviewExpectations=true;
            rows.Add(new { item.Id, item.Message,model,inferenceEndpoint=endpoint,modelIdentityVerified=false,
                caseMode=constraintsOnly?"CONTENT_INTERPRETATION_ONLY":item.ReviewOnlyBody is null?"GENERATION_AND_REVIEW":"CONTROLLED_BODY_REVIEW",
                elapsedMs=timer.ElapsedMilliseconds,stages,proposal,error,errorCode,reviewOutcome,item.ExpectedReview,reviewConforms });
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { scope = "GENERATION_COMPONENT_ONLY", rows }, json));
            Console.WriteLine($"{item.Id}: {(constraintsOnly?(error is null?"INTERPRETED_ONLY":"FAILED"):item.ReviewOnlyBody is null?(proposal is null?"FAILED":"GENERATED_REQUIRES_REVIEW"):reviewOutcome)}");
        }
        if(failedReviewExpectations)Environment.ExitCode=2;
    }
}
