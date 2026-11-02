using Microsoft.AspNetCore.Http.Features;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Order library-owned OAuth invalidations with job commits, without holding the fence while reading a request body.</summary>
public static class DelegationFence
{
    public static void UseDelegationFence(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/connect")) { await next(context); return; }
        if (HttpMethods.IsPost(context.Request.Method))
        {
            if (!context.Request.HasFormContentType) { context.Response.StatusCode = 400; return; }
            context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions
            { BufferBody = false, ValueCountLimit = 20, KeyLengthLimit = 80, ValueLengthLimit = 4096, MultipartBodyLengthLimit = 32768 }));
            try { await context.Request.ReadFormAsync(context.RequestAborted); }
            catch (InvalidDataException) { context.Response.StatusCode = 400; return; }
        }
        var queue = context.RequestServices.GetRequiredService<ExecutionQueue>();
        await queue.Fence(async () => { await next(context); return true; }, context.RequestAborted);
    });
}
