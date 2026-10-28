using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Explicit organization ports over the existing transactional owner.
public sealed partial class LocalStore : IGroupAuthoring, IGroupMemberships,
    IInstructionCatalog, IInstructionWriter, IPromptCombinations;
