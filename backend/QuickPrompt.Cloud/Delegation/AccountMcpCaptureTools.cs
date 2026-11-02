using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using QuickPrompt.Accounts.Libraries;

namespace QuickPrompt.Cloud.Delegation;

[McpServerToolType, Authorize(Policy = AccountMcpModule.CreatePolicy)]
public sealed class AccountMcpCaptureTools(AccountMcpAccess access)
{
    [McpServerTool(Name = "create_prompt", ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Save a NEW reusable prompt only when the user asks. Owner and destination library come from OAuth, never tool arguments. Does not edit/delete existing prompts, accept invitations or execute AI. Preserve exact template text, Unicode, newlines and <variable> markers. Title follows library validation/trim. Reuse the SAME operationId, title and templateText after uncertain delivery. Different content or destination with that ID is a conflict. A replay returns the initial saved version; get_prompt reads its current revision. A storage receipt does not certify semantic correctness.")]
    public Task<AccountCaptureReceipt> Create([Description("Nonempty UUID for this requested creation; preserve it across retries.")] Guid operationId,
        [Description("Prompt title, at most 160 characters.")] string title,
        [Description("Exact reusable instruction with its variable markers, at most 32000 characters.")] string templateText,
        CancellationToken cancellationToken = default) => access.Capture(new(operationId, title, templateText), cancellationToken);
}
