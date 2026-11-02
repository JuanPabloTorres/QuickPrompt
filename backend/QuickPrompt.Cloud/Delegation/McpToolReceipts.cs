using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Observes protocol structure without retaining tool content or changing its result, cancellation or authority.</summary>
internal static class McpToolReceipts
{
    internal static readonly object TraceKey = new();
    private const int InspectionLimit = 262144;

    internal static IMcpServerBuilder WithAccountToolReceipts(this IMcpServerBuilder builder) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellation) =>
        {
            var started = Stopwatch.GetTimestamp();
            CallToolResult result;
            try { result = await next(context, cancellation); }
            catch (OperationCanceledException) { Observe(context, null, "CANCELLED", started); throw; }
            catch { Observe(context, null, "FAULTED", started); throw; }
            Observe(context, result, "RETURNED", started);
            return result;
        }));

    // Request and response media types are classified, never reflected into logs.
    internal static string MediaTypeKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "MISSING";
        var media = value.Split(';', 2)[0].Trim();
        if (media.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return "JSON";
        return media.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase) ? "SSE" : "OTHER";
    }

    private static void Observe(RequestContext<CallToolRequestParams> context, CallToolResult? result, string outcome, long started)
    {
        try
        {
            var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("QuickPrompt.McpToolContract");
            if (logger is null || !logger.IsEnabled(LogLevel.Information)) return;
            var http = context.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
            var trace = http?.Items[TraceKey] as string ?? "";
            // Use the matched server primitive, never a request-supplied name or ID.
            var tool = context.MatchedPrimitive as McpServerTool;
            var shape = result is null ? new Shape(outcome, "NONE", null, null, "") : Inspect(result, tool);
            logger.LogInformation(new EventId(5321, "McpToolContractObserved"),
                "MCP tool contract {Tool} {Outcome} {IsError} {ContentBlocks} {Shape} {StructuredKind} {ChannelsEqual} {RequiredRootFieldsPresent} {SchemaSha256} {ElapsedMilliseconds} {McpTraceId}",
                tool?.ProtocolTool.Name ?? "UNMATCHED", outcome, result?.IsError, result?.Content?.Count ?? 0,
                shape.Status, shape.StructuredKind, shape.ChannelsEqual, shape.RequiredRootFieldsPresent, shape.SchemaSha256,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, trace);
        }
        catch (Exception)
        {
            // Inspection/logging is best effort. A missing receipt remains
            // unconfirmed; it cannot fail, replace or retry the original call.
        }
    }

    private sealed record Shape(string Status, string StructuredKind, bool? ChannelsEqual, bool? RequiredRootFieldsPresent, string SchemaSha256);

    private static Shape Inspect(CallToolResult result, McpServerTool? tool)
    {
        if (result.Content is not { Count: 1 } || result.Content[0] is not TextContentBlock text)
            return new("NON_SINGLE_TEXT", "NOT_INSPECTED", null, null, "");
        if (text.Text.Length > InspectionLimit)
            return new("INSPECTION_LIMIT", "NOT_INSPECTED", null, null, "");
        if (result.StructuredContent is null)
            return new("NO_STRUCTURED_CONTENT", "NONE", null, null, "");
        var structured = result.StructuredContent.Value;
        var schema = tool is null ? default : JsonSerializer.SerializeToElement(tool.ProtocolTool.OutputSchema);
        var hash = schema.ValueKind == JsonValueKind.Object
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema.GetRawText()))).ToLowerInvariant() : "";
        bool? requiredPresent = null;
        if (structured.ValueKind == JsonValueKind.Object && schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("required", out var required))
            requiredPresent = required.EnumerateArray().All(field => structured.TryGetProperty(field.GetString()!, out _));
        try
        {
            using var parsed = JsonDocument.Parse(text.Text, new JsonDocumentOptions { MaxDepth = 32 });
            var equal = JsonElement.DeepEquals(parsed.RootElement, structured);
            return new(equal ? "CHANNELS_MATCH" : "CHANNELS_DIFFER", structured.ValueKind.ToString(), equal, requiredPresent, hash);
        }
        catch (JsonException)
        {
            return new("TEXT_JSON_UNREADABLE", structured.ValueKind.ToString(), false, requiredPresent, hash);
        }
    }
}
