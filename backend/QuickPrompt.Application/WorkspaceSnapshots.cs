using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// The three mutable editor models share one copying algorithm. Persistence keeps
// its own archive codec; UI snapshots do not depend on a database serializer.
public static class WorkspaceSnapshots
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static Template Copy(Template value) => CopyModel(value);
    public static PromptGroup Copy(PromptGroup value) => CopyModel(value);
    public static InstructionProfile Copy(InstructionProfile value) => CopyModel(value);
    private static T CopyModel<T>(T value) where T : class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
}
