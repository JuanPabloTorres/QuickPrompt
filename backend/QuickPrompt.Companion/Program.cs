using System.Net;
using System.Security.Cryptography;
using OpenIddict.Abstractions;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Companion;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += cancel;
        CompanionSecrets? secrets = null;
        try
        {
            var options = CompanionOptions.Parse(args);
            secrets = new(options);
            using var exclusive = secrets.Acquire();
            await using var connection = new CompanionConnection(options, secrets);
            if (options.Command == "pair")
            {
                await connection.Pair((uri, code) => { Console.WriteLine("Abre QuickPrompt para revisar y decidir:"); Console.WriteLine(uri.AbsoluteUri); Console.WriteLine("Código de este equipo: " + code); }, stop.Token);
                Console.WriteLine("Vinculación guardada de forma privada. Puedes iniciar run.");
            }
            else
            {
                await connection.Connect(stop.Token);
                if (options.Command == "disconnect") { await connection.Disconnect(stop.Token); Console.WriteLine("Equipo desconectado y credencial local retirada."); }
                else if (options.Command == "status") Console.WriteLine("Autorización vigente para Qwen3:8b. Esta consulta no ejecutó el modelo.");
                else await new CompanionRunner(connection, new AdaptiveCoreModelTransport(options.Inference)).Run(options.MaximumJobs, stop.Token);
            }
            return 0;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { Console.WriteLine("Componente detenido por el usuario."); return 0; }
        catch (OpenIddictExceptions.ProtocolException error)
        {
            if (error.Error is "invalid_grant" or "invalid_token") secrets?.Forget();
            var code = error.Error is "invalid_request" or "invalid_scope" or "invalid_client" or "server_error" or "unauthorized_client"
                or "invalid_target" or "access_denied" or "invalid_grant" or "temporarily_unavailable" ? error.Error : "protocol_error";
            Console.Error.WriteLine("La autorización no está disponible. Revisa la conexión en QuickPrompt y vuelve a vincular si fue revocada.");
            Console.Error.WriteLine("Código de diagnóstico OAuth: " + code);
            var reference = System.Text.RegularExpressions.Regex.Match(error.ErrorUri ?? "", @"^https://documentation\.openiddict\.com/errors/(ID[0-9]{4})$");
            if (reference.Success) Console.Error.WriteLine("Referencia del protocolo: " + reference.Groups[1].Value);
            return 2;
        }
        catch (CompanionRequestException error)
        {
            if (error.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) secrets?.Forget();
            Console.Error.WriteLine("El servidor rechazó la operación. No se repetirá la instrucción automáticamente."); return 2;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or CryptographicException
            or UnauthorizedAccessException or HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        { Console.Error.WriteLine("No se pudo completar la operación. Revisa configuración, HTTPS, servicio y que este perfil no esté abierto en otro proceso. No se mostraron credenciales."); return 2; }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
