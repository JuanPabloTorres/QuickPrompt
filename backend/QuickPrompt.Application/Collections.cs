using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Consumer-oriented ports. Reads cannot acquire mutation methods merely to list
// collections; transactional validation remains with the existing store owner.
public interface ICollectionReader
{
    GroupSummary[] Groups();
    Guid[] Memberships(Guid promptId);
}

public interface ICollectionWriter
{
    PromptGroup Create(GroupInput input);
    PromptGroup Rename(Guid id, GroupInput input);
    void Delete(Guid id, ConfirmInput input);
    void Assign(Guid promptId, MembershipInput input);
}
