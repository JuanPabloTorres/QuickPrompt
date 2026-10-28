using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore : IQuickAssistWorkflow, ITransferActivityReader,
    ITransferActivityWriter, ILibraryDiagnostics;
