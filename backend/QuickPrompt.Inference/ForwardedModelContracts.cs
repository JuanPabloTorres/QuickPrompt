using System.Runtime.CompilerServices;
using QuickPrompt.Web.Infrastructure;

// Preserve compiled consumers of the inference assembly after the pure port moves
// to Application. Provider execution and SDK ownership remain in this module.
[assembly: TypeForwardedTo(typeof(IAgentModelTransport))]
[assembly: TypeForwardedTo(typeof(IValidatedAgentModelTransport))]
