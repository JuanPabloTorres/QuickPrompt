using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Adapter only: no second rule set, transaction, cache or membership authority.
public sealed class NovaCollectionStore(LocalStore store, IPromptCatalog prompts) : ICollectionReader, ICollectionWriter
{
    public GroupSummary[] Groups() => store.Groups().ToArray();
    public Guid[] Memberships(Guid promptId)
    {
        prompts.Get(promptId);
        return store.GroupsFor("template", promptId).ToArray();
    }
    public PromptGroup Create(GroupInput input) => store.SaveGroup(new() { Name = input.Name });
    public PromptGroup Rename(Guid id, GroupInput input) => store.NovaRenameGroup(id, input);
    public void Delete(Guid id, ConfirmInput input) => store.NovaDeleteGroup(id, input);
    public void Assign(Guid promptId, MembershipInput input) => store.NovaAssign(promptId, input);
}
