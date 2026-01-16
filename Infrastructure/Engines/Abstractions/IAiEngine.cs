using QuickPrompt.Engines.Descriptors;
using QuickPrompt.Engines.Execution;

namespace QuickPrompt.Engines.Abstractions
{
    public interface IAiEngine
    {
        string Name { get; }
        AiEngineDescriptor Descriptor { get; }
        // No lógica de ejecución aún
    }
}