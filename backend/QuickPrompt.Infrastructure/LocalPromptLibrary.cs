using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Shared composition factory for the API and the server-rendered application.
// Hosts own registration/lifetime; this module owns the adapter construction graph.
public sealed class LocalPromptLibrary
{
    public LocalStore Store { get; }
    public NovaPromptStore PromptStore { get; }
    public PromptLibrary Prompts { get; }
    public NovaCollectionStore Collections { get; }
    public NovaLibraryArchives Archives { get; }
    public IPromptSharing Sharing { get; }
    public IPromptTransfer Transfers { get; }
    public IPromptReader Templates => PromptStore;
    public IPromptWriter TemplateWriter => PromptStore;
    public ITemplateAuthoring Authoring => Store;
    public ICompilationReader Compiled => Store;
    public ICompilationWriter Compilation => Store;
    public ILocalSettings Settings => Store;
    public IProposalLifecycle Proposals => Store;
    public ICollectionReader CollectionReader => Collections;
    public ICollectionWriter CollectionWriter => Collections;
    public IGroupAuthoring GroupAuthoring => Store;
    public IGroupMemberships Memberships => Store;
    public IInstructionCatalog Instructions => Store;
    public IInstructionWriter InstructionWriter => Store;
    public IPromptCombinations Combinations => Store;
    public IQuickAssistWorkflow Assist => Store;
    public ITransferActivityReader TransferHistory => Store;
    public ITransferActivityWriter TransferAudit => Store;
    public ILibraryDiagnostics Diagnostics => Store;
    public IPromptPanel Panel { get; }
    public IStorageFailureClassifier StorageFailures { get; } = new SqliteFailureClassifier();
    public ILegacyArchiveConverter LegacyConverter { get; } = new LegacyArchiveConverter();

    public LocalPromptLibrary(string path)
    {
        Store = new(path);
        PromptStore = new(Store);
        Prompts = new(PromptStore, PromptStore);
        Collections = new(Store, Prompts);
        Archives = new(Store);
        Sharing = new NovaPromptSharing(Store, Prompts);
        Transfers = new PromptTransfer(Prompts);
        Panel = new PromptPanel(Templates, Compilation);
    }
}
