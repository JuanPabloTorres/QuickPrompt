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
        CompanionRunDiagnostics? diagnostic = null;
        IDisposable? exclusive = null;
        try
        {
            var options = CompanionOptions.Parse(args);
            secrets = new(options);
            exclusive = secrets.Acquire();
            if (options.Command == "run")
            {
                diagnostic = new(options.Profile, secrets.WriteRunDiagnostic);
                diagnostic.Stage(CompanionRunPhase.Connecting);
            }
            else if (options.Command == "status" && secrets.ReadRunDiagnostic() is { } prior)
            {
                Console.WriteLine("Última ejecución local (registro histórico; no confirma que siga activa):");
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(prior, CompanionRunDiagnostics.Json));
            }
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
                else
                {
                    var runner = new CompanionRunner(connection, new AdaptiveCoreModelTransport(options.Inference), diagnostic);
                    var ending = options.StayConnected ? await runner.StayConnected(stop.Token) : await runner.Run(options.MaximumJobs, stop.Token);
                    diagnostic?.Finish(ending);
                }
            }
            return 0;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { diagnostic?.Finish(CompanionRunEnd.UserStopped); Console.WriteLine("Componente detenido por el usuario."); return 0; }
        catch (OpenIddictExceptions.ProtocolException error)
        {
            if (error.Error is "invalid_grant" or "invalid_token") secrets?.Forget();
            var code = CompanionRunDiagnostics.SafeOAuthCode(error.Error);
            diagnostic?.Finish(CompanionRunEnd.OAuthRejected, oauthCode: code);
            Console.Error.WriteLine("La autorización no está disponible. Revisa la conexión en QuickPrompt y vuelve a vincular si fue revocada.");
            Console.Error.WriteLine("Código de diagnóstico OAuth: " + code);
            var reference = System.Text.RegularExpressions.Regex.Match(error.ErrorUri ?? "", @"^https://documentation\.openiddict\.com/errors/(ID[0-9]{4})$");
            if (reference.Success) Console.Error.WriteLine("Referencia del protocolo: " + reference.Groups[1].Value);
            return 2;
        }
        catch (CompanionRequestException error)
        {
            if (error.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) secrets?.Forget();
            diagnostic?.Finish(CompanionRunEnd.HttpRejected, httpStatus: (int)error.Status);
            Console.Error.WriteLine("Código de diagnóstico HTTP: " + (int)error.Status);
            Console.Error.WriteLine("El servidor rechazó la operación. No se repetirá la instrucción automáticamente."); return 2;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or CryptographicException
            or UnauthorizedAccessException or HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        { diagnostic?.Finish(CompanionRunEnd.TransportOrConfigurationError); Console.Error.WriteLine("No se pudo completar la operación. Revisa configuración, HTTPS, servicio y que este perfil no esté abierto en otro proceso. No se mostraron credenciales."); return 2; }
        finally
        {
            // Keep this profile's ownership until its terminal receipt is published,
            // including failures; a later process must never be overwritten by this run.
            try { diagnostic?.Finish(CompanionRunEnd.UnconfirmedExit); }
            finally { exclusive?.Dispose(); Console.CancelKeyPress -= cancel; }
        }
    }
}
