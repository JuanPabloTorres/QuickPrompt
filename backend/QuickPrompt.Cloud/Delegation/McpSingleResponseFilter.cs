using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>
/// Delivers finite, stateless MCP replies as complete JSON without re-executing the SDK handler.
/// Register after authorization and input guards. Multiple/resumable events retain their SSE representation.
/// Malformed single replies and encoded replies over 1 MiB fail with HTTP 502 before success headers are sent.
/// </summary>
public sealed class McpSingleResponseFilter : IEndpointFilter
{
    // Current account tools return at most 20 summaries or one 32,000-character template.
    // Bound the encoded response as well, including escaping, metadata and future changes.
    private const int MaximumResponseBytes = 1024 * 1024;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        if (!HttpMethods.IsPost(context.Request.Method)) return await next(invocation);

        var original = context.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        using var buffer = new ResponseBuffer();
        var staged = new StreamResponseBodyFeature(buffer);
        object? result;
        context.Features.Set<IHttpResponseBodyFeature>(staged);
        try
        {
            result = await next(invocation);
            await staged.CompleteAsync();
        }
        catch (ResponseLimitException)
        {
            return DeliveryFailure(context);
        }
        finally
        {
            // Defer StartAsync/FlushAsync as well as writes: changing Response.Body alone
            // must not send the SDK's SSE headers before the final representation is known.
            context.Features.Set(original);
        }

        if (context.Response.ContentType?.Split(';', 2)[0].Trim() == "text/event-stream"
            && context.Response.StatusCode == StatusCodes.Status200OK)
        {
            byte[]? json;
            try { json = await SingleResponse(buffer, context.RequestAborted); }
            catch (JsonException) { return DeliveryFailure(context); }
            if (json is not null)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Remove("Content-Encoding");
                context.Response.Headers.Remove("X-Accel-Buffering");
                context.Response.Headers.CacheControl = "no-store, no-transform";
                context.Response.ContentLength = json.Length;
                await original.Stream.WriteAsync(json, context.RequestAborted);
                await original.Stream.FlushAsync(context.RequestAborted);
                return result;
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original.Stream, context.RequestAborted);
        await original.Stream.FlushAsync(context.RequestAborted);
        return result;
    }

    private static async Task<byte[]?> SingleResponse(Stream buffer, CancellationToken cancellation)
    {
        buffer.Position = 0;
        SseItem<string>? single = null;
        await foreach (var item in SseParser.Create(buffer).EnumerateAsync(cancellation))
        {
            // Never discard progress, notifications, event IDs or reconnection information.
            if (single is not null) return null;
            single = item;
        }
        if (single is not { } message) throw new JsonException();
        if (message.EventType != "message" || message.EventId is not null || message.ReconnectionInterval is not null) return null;
        using var parsed = JsonDocument.Parse(message.Data);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("jsonrpc", out var version)
            || version.ValueKind != JsonValueKind.String || !version.ValueEquals("2.0")
            || !root.TryGetProperty("id", out var id) || id.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)
            || root.TryGetProperty("result", out _) == root.TryGetProperty("error", out _))
            throw new JsonException();
        // Preserve the complete SDK envelope, including metadata and both content channels.
        return Encoding.UTF8.GetBytes(message.Data);
    }

    private static IResult DeliveryFailure(HttpContext context)
    {
        context.Response.ContentLength = null;
        context.Response.Headers.Remove("Content-Encoding");
        context.Response.Headers.Remove("X-Accel-Buffering");
        return Results.Problem(statusCode: StatusCodes.Status502BadGateway,
            title: "No se pudo entregar la respuesta completa. Verifica el resultado antes de repetir la operación.");
    }

    private sealed class ResponseLimitException : IOException;

    private sealed class ResponseBuffer : MemoryStream
    {
        private void Check(int count)
        {
            if (Position + count > MaximumResponseBytes) throw new ResponseLimitException();
        }

        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Check(count); return base.WriteAsync(buffer, offset, count, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Check(buffer.Length); return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
