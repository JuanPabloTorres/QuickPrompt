using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>Preserve typed transport interruptions at preparation adapters without copying provider diagnostics.</summary>
public static class InferenceFailures
{
    public static PreparationToolException ToPreparation(LlmUnavailableException failure, string purpose)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var message = failure.Code switch {
            "MODEL_UNAVAILABLE" => "El modelo local no está disponible. La biblioteca sigue disponible.",
            "MODEL_TIMEOUT" => "El modelo agotó el tiempo permitido. No se repetirán automáticamente sus pasos.",
            "MODEL_OUTPUT_LIMIT" => "El modelo agotó el límite de salida sin completar la respuesta. No se repetirá automáticamente.",
            "LOCAL_DISPATCH_TIMEOUT" => "No se confirmó que el componente local recogiera la solicitud dentro del plazo. Comprueba que siga activo; no se repetirá automáticamente.",
            "COMPANION_NOT_OBSERVED" => "No hay una señal reciente del componente local. Ábrelo manualmente y espera a que consulte QuickPrompt. Las acciones ya confirmadas conservan sus recibos; no se repetirá automáticamente.",
            "QUEUE_EXPIRED" => "La solicitud caducó antes de que el equipo la recogiera.",
            "CONNECTION_REVOKED" => "La autorización del equipo fue revocada. No se ejecutarán pasos pendientes.",
            "SESSION_REVOKED" => "La sesión dejó de estar autorizada. No se ejecutarán pasos pendientes.",
            "LEASE_EXPIRED" => "Caducó la autorización de esta ejecución. No se repetirá automáticamente.",
            "HOST_STOPPED" => "El equipo detuvo la ejecución. No se repetirán sus pasos.",
            _ => "El modelo no pudo completar la solicitud. No se ejecutaron nuevos efectos."
        };
        // LlmUnavailableException already validates Code. Do not retain Message
        // or InnerException: either could carry raw diagnostics from a provider.
        return new(failure.Code, purpose, message);
    }
}
