using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// Real Qwen judges controlled synthetic candidates through the production generation
// boundary. This component diagnostic is neither a task-success nor a holdout campaign.
public static class FidelityEvaluation
{
    private record Case(string Id,string Source,string Request,string Candidate,bool Accept);
    private record Dataset(string Version,bool SyntheticOnly,bool ComponentDiagnosticOnly,Case[] Cases);
    public static async Task Run(string[] args)
    {
        if(args.Length!=2 && !(args.Length==3&&args[2] is "--production-profile" or "--direct-review"))
            throw new ArgumentException("Pass a synthetic fidelity dataset, report path, and optional --production-profile or --direct-review.");
        var samplingExperiment=args.Length==2;
        var thinking=args.Length!=3||args[2]!="--direct-review";
        using var lease=new FileStream(Path.Combine(Path.GetTempPath(),"QuickPrompt.Nova.SemanticModel.lease"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var bytes=File.ReadAllBytes(args[0]);var data=JsonSerializer.Deserialize<Dataset>(bytes,json)!;
        if(!data.SyntheticOnly||!data.ComponentDiagnosticOnly||data.Cases.Length==0)throw new ArgumentException("Synthetic component diagnostic required.");
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(5)};
        var before=JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("http://127.0.0.1:11434/api/ps"));
        using var tags=JsonDocument.Parse(await client.GetStringAsync("http://127.0.0.1:11434/api/tags"));
        var digest=tags.RootElement.GetProperty("models").EnumerateArray().Single(model=>model.GetProperty("name").GetString()=="qwen3:8b").GetProperty("digest").GetString();
        var rows=new List<object>();var correct=0;var evaluated=0;
        foreach(var scenario in data.Cases) {
            var editor=new ControlledEditor(scenario.Candidate);
            using var inference=new PreparationInference("http://127.0.0.1:11434",localEditor:editor,taskGoals:true,semanticThinking:thinking,qwenThinkingSampling:samplingExperiment);
            var source=new PromptDocument(Guid.Parse("40000000-0000-0000-0000-000000000001"),"Base sintética","",scenario.Source,0,false,false,1,DateTime.UnixEpoch,DateTime.UnixEpoch,0,null,TemplateCompiler.Names(scenario.Source),new Dictionary<string,string>());
            var plan=SemanticPlanCompiler.Compile(new([SemanticOperation.PROPOSE_ADAPTATION],TargetReference.SELECTED,"",source.Id,"",scenario.Request,
                new([SemanticOperation.EXECUTE_LOCAL,SemanticOperation.REQUEST_SAVE_VERSION,SemanticOperation.REQUEST_SAVE_COPY],true,null,null,[],[],false),SemanticOutcome.DRAFT,[],false,"",null,"Contrato de componente controlado; no evalúa interpretación de intención."));
            plan=plan with {Goal=TaskGoal.Create(scenario.Request,SemanticContext.Empty with {Selected=SemanticEntity.From(source)},plan,[new(TaskClauseRole.CONTENT,scenario.Request){ContentChange=scenario.Request}])};
            JsonElement? review=null;
            inference.SemanticDiagnostic=(stage,content,_)=> {if(stage==190)try{review=JsonSerializer.Deserialize<JsonElement>(content);}catch(JsonException){}};
            var input=new PrepareInput(Guid.NewGuid(),scenario.Request,SourcePromptId:source.Id,SourceRevision:source.Revision){Interpretation=plan};
            var timer=Stopwatch.StartNew();var accepted=false;string? failure=null;string? failureCode=null;var validVerdict=true;
            try {
                using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(90));
                await inference.Generate(input,plan.Preparation(),[new(source,10,"Origen sintético controlado")],cancellation.Token);accepted=true;
            } catch(ValidationException error) {failure=error.Message;failureCode=error.Data["NovaValidationCode"] as string;validVerdict=failureCode=="CONTENT_FIDELITY_MISMATCH";}
              catch(Exception error) {failure=error.GetType().Name;validVerdict=false;}
            var matches=validVerdict&&accepted==scenario.Accept;evaluated++;if(matches)correct++;
            rows.Add(new {scenario.Id,expectedAccept=scenario.Accept,accepted,validVerdict,correct=matches,elapsedMs=timer.ElapsedMilliseconds,failure,failureCode,review});
            Console.WriteLine($"{scenario.Id} {(matches?"PASS":"FAIL")} {timer.ElapsedMilliseconds}ms");Report(false);
        }
        Report(true);
        void Report(bool complete) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            File.WriteAllText(args[1],JsonSerializer.Serialize(new {status=complete?"DIAGNOSTIC_COMPLETE":"RUNNING",acceptanceComplete=false,
                data.Version,component="PRODUCTION_DRAFT_FIDELITY_REVIEW_WITH_CONTROLLED_GENERATOR",count=evaluated,correct,agreement=correct/(double)Math.Max(1,evaluated),
                datasetSha256=Convert.ToHexString(SHA256.HashData(bytes)),model="qwen3:8b",modelDigest=digest,semanticThinking=thinking,qwenThinkingSampling=samplingExperiment,
                resourcesBefore=before,rows,limits=new[]{"Development component diagnostic, not independent validation or holdout.","Intent, UI, persistence and latency A/B are NOT_TESTED here.","Controlled generator; only the semantic reviewer uses real Qwen. No model thinking is recorded."}},json));
        }
    }
    private sealed class ControlledEditor(string text):ILocalLlmClient
    {
        public Task<AiProposal> Propose(string action,string context,Template? template,string model,CancellationToken ct)=>Task.FromResult(new AiProposal(Guid.NewGuid(),template!.Id,template.Revision,text,[],[],[],TemplateCompiler.Names(text),model));
        public Task<string[]> Available(CancellationToken ct)=>throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<Dictionary<string,string>> Suggest(Template template,string context,string model,CancellationToken ct)=>throw new NotSupportedException();
        public Task<(string Text,double ElapsedMs)> Execute(CompiledPrompt prompt,string model,bool confirmed,Func<string,Task>? progress,CancellationToken ct)=>throw new NotSupportedException();
    }
}
