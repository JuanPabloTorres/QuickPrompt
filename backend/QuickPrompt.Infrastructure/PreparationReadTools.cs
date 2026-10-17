using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class PreparationReadTools(PromptLibrary library, LocalStore store) : IPreparationReadTools
{
    public void Register(PreparationTools tools)
    {
        var id = ContractSchema.Object(new() { ["id"] = ContractSchema.Text(36) }, "id");
        tools.Register<PromptIdArgs, PromptDocument>("get_prompt", ToolPermission.Read, id,
            (args, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(library.Get(args.Id)); });
        var revision = ContractSchema.Object(new() { ["id"] = ContractSchema.Text(36), ["revision"] = new { type = "integer" } }, "id", "revision");
        tools.Register<RevisionArgs, TemplateVersion>("get_prompt_revision", ToolPermission.Read, revision, (args, ct) =>
        {
            ct.ThrowIfCancellationRequested(); library.Get(args.Id);
            return Task.FromResult(store.Versions(args.Id).SingleOrDefault(v => v.Revision == args.Revision) ?? throw new MissingPromptException());
        });
        tools.Register<EmptyArgs, GroupSummary[]>("list_groups", ToolPermission.Read, ContractSchema.Object(new()),
            (_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(store.Groups().ToArray()); });
    }
    public record PromptIdArgs(Guid Id);
    public record RevisionArgs(Guid Id, int Revision);
    public record EmptyArgs;
}
