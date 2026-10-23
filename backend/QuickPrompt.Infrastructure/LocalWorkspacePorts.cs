using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Interface segregation over the existing transactional owner. No second state,
// codecs, authorization implementation or one-class-per-method forwarding layer.
public sealed partial class LocalStore : ITemplateAuthoring, ICompilationReader,
    ICompilationWriter, ILocalSettings, IProposalLifecycle;
