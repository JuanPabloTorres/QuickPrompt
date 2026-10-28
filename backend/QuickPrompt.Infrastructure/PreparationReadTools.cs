using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class PreparationReadTools(IPromptCatalog library, ICollectionReader collections) : IPreparationReadTools
{
    public void Register(PreparationTools tools) => tools.RegisterAttributed(this, ReflectedPreparationToolFactory.Instance);

    [PreparationTool("get_prompt", ToolPermission.Read, "Read a saved prompt by its actual identity without modifying it.")]
    public Task<PromptDocument> GetPrompt(PromptIdArgs args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(library.Get(args.Id));
    }

    [PreparationTool("get_prompt_revision", ToolPermission.Read, "Read an existing revision of a real prompt without modifying it.")]
    public Task<TemplateVersion> GetRevision(RevisionArgs args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(library.Versions(args.Id).SingleOrDefault(v => v.Revision == args.Revision) ?? throw new MissingPromptException());
    }

    [PreparationTool("list_groups", ToolPermission.Read, "List the groups currently stored in the library.")]
    public Task<GroupSummary[]> ListGroups(EmptyArgs args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(collections.Groups());
    }
    public record PromptIdArgs(Guid Id);
    public record RevisionArgs(Guid Id, int Revision);
    public record EmptyArgs;
}
