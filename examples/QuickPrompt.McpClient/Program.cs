using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using QuickPrompt.Nova.Mcp;

// A visible, deterministic MCP client for learning and diagnosis; no AI API or secret is needed.
// Usage: <API port> <tools|list_collections|search_prompts|get_prompt|create_prompt|compile_prompt> [arguments.json]
if (args.Length is < 2 or > 3 || !int.TryParse(args[0], out var port) || port is < 1024 or > 65535
    || args[1] is not ("tools" or "list_collections" or "search_prompts" or "get_prompt" or "create_prompt" or "compile_prompt")
    || (args[1] != "tools" && args.Length != 3))
{
    Console.Error.WriteLine("Uso: dotnet run --project examples/QuickPrompt.McpClient -- <puerto API> <tools|nombre_herramienta> [argumentos.json]");
    return 2;
}
try
{
    Dictionary<string, object?> input = [];
    if (args.Length == 3)
    {
        var file = new FileInfo(args[2]);
        if (!file.Exists || file.Length > 192 * 1024) throw new InvalidOperationException("Archivo JSON inexistente o demasiado grande.");
        input = JsonSerializer.Deserialize<Dictionary<string, object?>>(await File.ReadAllTextAsync(file.FullName))
            ?? throw new InvalidOperationException("Se requiere un objeto JSON.");
    }
    await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
    {
        Command = "dotnet", Arguments = [typeof(QuickPromptMcpHost).Assembly.Location],
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["QuickPromptMcp__Transport"] = "stdio", ["QuickPromptMcp__ApiBaseUrl"] = $"http://127.0.0.1:{port}",
            ["QuickPromptMcp__AllowCreate"] = (args[1] == "create_prompt").ToString()
        }
    }));
    var json = new JsonSerializerOptions { WriteIndented = true };
    if (args[1] == "tools")
    {
        Console.WriteLine(JsonSerializer.Serialize((await client.ListToolsAsync()).Select(tool => new { tool.Name, tool.Description }), json));
        return 0;
    }
    var result = await client.CallToolAsync(args[1], input);
    Console.WriteLine(result.StructuredContent is { } content ? JsonSerializer.Serialize(content, json)
        : string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
    return result.IsError == true ? 1 : 0;
}
catch (Exception error) when (error is JsonException or IOException or InvalidOperationException)
{
    Console.Error.WriteLine("No se completó la llamada MCP. Revisa la API, el archivo JSON y los argumentos. No se reintentó automáticamente.");
    return 1;
}
