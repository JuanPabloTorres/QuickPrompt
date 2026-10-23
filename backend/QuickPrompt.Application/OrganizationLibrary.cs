using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Organization capabilities extend the existing owners. Collection reads and
// protected deletion reuse ICollectionReader/ICollectionWriter, not another catalog.
public interface IGroupAuthoring
{
    PromptGroup SaveGroup(PromptGroup value);
}
public interface IGroupMemberships
{
    List<Guid> GroupsFor(string kind, Guid id);
    void AssignGroup(Guid groupId, string kind, IEnumerable<Guid> entityIds, bool member = true);
    PromptGroup CreateGroupAndAssign(PromptGroup group, string kind, Guid entityId);
}
public interface IInstructionCatalog
{
    InstructionProfile? FindInstruction(Guid id);
    CatalogPage<InstructionProfile> QueryInstructions(CatalogQuery query);
    List<InstructionChoice> InstructionChoices(string search = "");
    List<InstructionVersion> InstructionVersions(Guid id);
}
public interface IInstructionWriter
{
    InstructionProfile SaveInstruction(InstructionProfile value);
    void DeleteInstruction(Guid id, ConfirmInput input);
}
public interface IPromptCombinations
{
    List<PromptCombination> Combinations(Guid templateId);
    PromptCombination SaveCombination(PromptCombination value);
    void DeleteCombination(Guid id);
}
