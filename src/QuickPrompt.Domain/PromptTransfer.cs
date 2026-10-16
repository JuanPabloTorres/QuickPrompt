using System.Text.Json.Serialization;

namespace QuickPrompt.Local;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderId { LocalOllama, ChatGptWeb, ClaudeWeb }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferAction { PrepareOnly, PrepareAndSend }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferState { Ready, ExtensionMissing, ProviderTabOpening, WaitingForEditor, Prepared, SubmissionRequested, SubmittedUI, NeedsManualPaste, LoginRequired, BlockedByProvider, Timeout, Failed, Cancelled }

public sealed record PromptTransferRequest(int Version, Guid RequestId, ProviderId Provider, string CompiledPrompt,
    TransferAction Action, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, bool ExplicitUserAction,
    Guid? TemplateId = null, Guid? RevisionId = null)
{
    public const int MaxCharacters = 20_000;
    public void Validate(DateTimeOffset now)
    {
        if(ValidationCode(now) is {} code)throw new ValidationException(code switch {
            "invalid-prompt"=>"Confirma la acción; texto requerido, máximo 20 000 caracteres / Confirm action; text required, max 20,000 characters.",
            "expired-request"=>"Transferencia caducada / Transfer expired.",
            "invalid-metadata"=>"Identificador opcional inválido / Invalid optional identifier.",
            _=>"Transferencia inválida / Invalid transfer."});
    }
    public string? ValidationCode(DateTimeOffset now)
    {
        if(Version != 1 || RequestId == Guid.Empty || !Enum.IsDefined(Provider) || Provider == ProviderId.LocalOllama || !Enum.IsDefined(Action))
            return "invalid-request";
        if(!ExplicitUserAction || string.IsNullOrWhiteSpace(CompiledPrompt) || CompiledPrompt.Length > MaxCharacters)
            return "invalid-prompt";
        if(CreatedAtUtc > now.AddSeconds(5) || ExpiresAtUtc <= now || ExpiresAtUtc <= CreatedAtUtc || ExpiresAtUtc-CreatedAtUtc > TimeSpan.FromMinutes(2) || now-CreatedAtUtc > TimeSpan.FromMinutes(2))
            return "expired-request";
        if(TemplateId==Guid.Empty || RevisionId==Guid.Empty)return "invalid-metadata";
        return null;
    }
}
public sealed record PromptTransferResult(Guid RequestId, TransferState State, string Code, double DurationMs = 0, string AdapterVersion = "1.0.0");
