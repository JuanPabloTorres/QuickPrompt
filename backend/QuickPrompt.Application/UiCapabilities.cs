using System.Text.Json;

namespace QuickPrompt.Nova.Application;

// Trusted declarations discovered once, never names supplied by a model.
public static class UiCapabilities
{
    private static JsonElement Empty => ContractSchema.Object([]);
    private static object DraftTarget => UiToolCatalog.DraftTarget;
    private static JsonElement Target => UiToolCatalog.Target;
    [UiCapability("ui_navigate_library", "Volver a la biblioteca", "Return to the actual library to search a different prompt. Never discard an open editor draft.")]
    public static JsonElement UiNavigateLibrary => Empty;

    [UiCapability("ui_open_create_prompt", "Abrir el formulario de creación", "Open the real new-prompt form, without saving. Only for a new draft requested by the user.")]
    public static JsonElement UiOpenCreatePrompt => Empty;

    [UiCapability("ui_prepare_creation", "Generar y escribir el nuevo borrador", "Generate the requested new reusable prompt with Qwen and type the validated title and body into the real open form. Arguments MUST be empty {}: do not pass title, body or text. Code invokes the generator with the original request. Never saves.")]
    public static JsonElement UiPrepareCreation => Empty;

    [UiCapability("ui_load_active_draft", "Recuperar el borrador de la conversación", "Load the authorized previous conversation draft into the real editor. No text arguments; content is bound by code to the session artifact. Use before changing, copying or running that draft.")]
    public static JsonElement UiLoadActiveDraft => Target;

    [UiCapability("ui_search_prompt", "Buscar en la biblioteca", "Search the actual visible library using a short title or topic; never a full request.")]
    public static JsonElement UiSearchPrompt => ContractSchema.Object(new() { ["query"] = ContractSchema.Text(200) }, "query");

    [UiCapability("ui_open_prompt", "Abrir el prompt seleccionado", "Click one real prompt currently visible in the library; verify its identity. A user-selected visible ID may be opened directly.")]
    public static JsonElement UiOpenPrompt => ContractSchema.Object(new() { ["promptId"] = ContractSchema.Text(36) }, "promptId");

    [UiCapability("ui_open_editor", "Abrir el editor", "Open the selected prompt's actual editor, without saving.")]
    public static JsonElement UiOpenEditor => Target;

    [UiCapability("ui_fill_variable", "Completar un campo autorizado", "Fill one visible variable with an EXACT value from the user's request or explicitly supplied values. evidence must quote that value literally.")]
    public static JsonElement UiFillVariable => ContractSchema.Object(new() { ["promptId"] = DraftTarget, ["name"] = ContractSchema.Text(80), ["value"] = ContractSchema.Text(8000), ["evidence"] = ContractSchema.Text(16000) }, "promptId", "name", "value", "evidence");

    [UiCapability("ui_prepare_update", "Preparar el cambio con Qwen en el editor", "Ask the existing Qwen editor to modify this DRAFT according to the ORIGINAL USER REQUEST held by the server. The executor fills that exact request in the editor; do not rewrite it or pass new instructions. Never saves the source.")]
    public static JsonElement UiPrepareUpdate => Target;

    [UiCapability("ui_show_compiled_prompt", "Revisar la vista previa compilada", "Inspect the deterministic compiled preview; ask for missing inputs if not ready.")]
    public static JsonElement UiShowCompiledPrompt => Target;

    [UiCapability("ui_show_template", "Revisar la plantilla con marcadores", "Show the exact reusable instruction with its input markers. Only for an explicit template copy representation; never claims compiled readiness.", RequiresTemplateCopyChoice = true)]
    public static JsonElement UiShowTemplate => Target;

    [UiCapability("ui_copy_template", "Copiar la plantilla con marcadores", "Click the template Copy control and require a fresh TEMPLATE receipt matching the current reusable instruction exactly. Only for an explicitly requested clipboard write and template representation.", Permission = "EXPLICIT_CLIPBOARD_WRITE", RequiresTemplateCopyChoice = true)]
    public static JsonElement UiCopyTemplate => Target;

    [UiCapability("ui_copy_compiled_prompt", "Copiar el texto compilado", "Click the application Copy control and require a new receipt for the exact current compiled text. Only for an explicitly requested clipboard write.", Permission = "EXPLICIT_CLIPBOARD_WRITE")]
    public static JsonElement UiCopyCompiledPrompt => Target;

    [UiCapability("ui_open_provider_picker", "Mostrar los destinos disponibles", "Open the existing provider picker for review only. No external navigation, copying or submission.")]
    public static JsonElement UiOpenProviderPicker => Target;

    [UiCapability("ui_execute_local", "Ejecutar con Qwen local", "Execute the compiled prompt with local Qwen ONLY when the user's original request explicitly asks for local execution.", Permission = "EXPLICIT_LOCAL_EXECUTION")]
    public static JsonElement UiExecuteLocal => Target;

    [UiCapability("ui_read_visible_state", "Leer el estado visible", "Read visible React controls and current state without changing them.")]
    public static JsonElement UiReadVisibleState => Empty;

    [UiCapability("ui_verify_prompt_state", "Verificar el prompt y su revisión", "Independently compare visible identity/revision/compiled values with the domain source; no mutation.")]
    public static JsonElement UiVerifyPromptState => Target;

    [UiCapability("ui_request_save_version", "Pedir revisión de la nueva versión", "Request review of this exact draft and source revision. This does not save. A human must confirm the proposal.", Permission = "REQUIRES_PROPOSAL_APPROVAL")]
    public static JsonElement UiRequestSaveVersion => Target;

    [UiCapability("ui_review_deletion", "Mostrar la revisión de eliminación", "Present the code-owned deletion review for a resolved saved record; no deletion occurs.", Permission = "REQUIRES_DELETE_REVIEW")]
    public static JsonElement UiReviewDeletion => ContractSchema.Object(new() { ["promptId"] = ContractSchema.Text(36), ["revision"] = new { type = "integer", minimum = 1 } }, "promptId", "revision");

    [UiCapability("ui_enable_deletion_confirmation", "Esperar tu confirmación de eliminación", "Enable the human confirmation control after the bound native review receipt was validated.", Permission = "REQUIRES_DELETE_REVIEW")]
    public static JsonElement UiEnableDeletionConfirmation => ContractSchema.Object(new() { ["promptId"] = ContractSchema.Text(36), ["revision"] = new { type = "integer", minimum = 1 } }, "promptId", "revision");

    [UiCapability("ui_verify_deletion", "Verificar el prompt eliminado", "Verify the already human-confirmed record is absent from the visible library; no write.")]
    public static JsonElement UiVerifyDeletion => Target;

    [UiCapability("finish", "Terminar y verificar el resultado", "Finish when every requested effect has been independently observed. A changed draft is held for human review, never silently saved.")]
    public static JsonElement Finish => Empty;

    [UiCapability("ask_clarification", "Pedir una aclaración", "Ask one concrete question when no unique source exists or required data is missing. Never select arbitrarily.")]
    public static JsonElement AskClarification => ContractSchema.Object(new() { ["question"] = ContractSchema.Text(500) }, "question");
}
