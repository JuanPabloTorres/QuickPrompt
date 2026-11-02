using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

internal enum ApertiumOperation { PortugueseAnalysis, SpanishAnalysis, PortugueseSpanish, SpanishEnglish }

/// <summary>Fixed native commands; all source content travels as base64 stdin, never shell code or arguments.</summary>
internal sealed class ApertiumFrameRunner
{
    private const string Batch = """
        set -euo pipefail
        while IFS= read -r item; do
          printf '%s' "$item" | base64 --decode | "$@" | base64 -w0
          printf '\n'
        done
        """;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string? image;
    private bool nativeVerified;
    private ApertiumFrameRunner(string? image) => this.image = image;
    public string Identity => image ?? "native-apertium-3.9.12-pinned-files";
    public static ApertiumFrameRunner Native() => new(null);
    public static ApertiumFrameRunner Docker(string image) =>
        image.StartsWith("sha256:", StringComparison.Ordinal) && image.Length == 71 && image[7..].All(Uri.IsHexDigit)
            ? new(image) : throw new ArgumentException("Usa la huella completa de la imagen lingüística ya instalada.");

    public async Task<string[]> Run(ApertiumOperation operation, string[] fields, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (image is null && !nativeVerified) VerifyNative();
        var command = operation switch {
            ApertiumOperation.PortugueseAnalysis => new[] { "/usr/bin/lt-proc", "-a", "/usr/share/apertium/apertium-es-pt/pt-es.automorf.bin" },
            ApertiumOperation.SpanishAnalysis => new[] { "/usr/bin/lt-proc", "-a", "/usr/share/apertium/apertium-eng-spa/spa-eng.automorf.bin" },
            ApertiumOperation.PortugueseSpanish => new[] { "/usr/bin/apertium", "-u", "pt-es" },
            ApertiumOperation.SpanishEnglish => new[] { "/usr/bin/apertium", "-u", "spa-eng" },
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var container = "quickprompt-linguistic-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(image is null ? "/bin/bash" : "docker") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardInputEncoding = Utf8, StandardOutputEncoding = Utf8, StandardErrorEncoding = Utf8
        };
        if (image is not null)
            foreach (var item in new[] { "run", "--rm", "--pull=never", "--name", container, "--network", "none", "--read-only",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "65534:65534", "--pids-limit", "64",
                "--memory", "256m", "--cpus", "1", "--tmpfs", "/tmp:rw,noexec,nosuid,size=32m,mode=1777",
                "-e", "LANG=C.UTF-8", "-e", "LC_ALL=C.UTF-8", "-i", "--entrypoint", "/bin/bash", image }) start.ArgumentList.Add(item);
        foreach (var item in new[] { "-c", Batch, "quickprompt-linguistic-batch" }.Concat(command)) start.ArgumentList.Add(item);
        start.Environment["LANG"] = "C.UTF-8"; start.Environment["LC_ALL"] = "C.UTF-8";
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        using var process = new Process { StartInfo = start };
        var started = false;
        Task<string>? output = null; Task<string>? error = null;
        try {
            started = process.Start();
            if (!started) throw new ValidationException("No se pudo iniciar el recurso lingüístico.");
            output = ReadBounded(process.StandardOutput, 4 * 1024 * 1024, limit.Token);
            error = ReadBounded(process.StandardError, 32768, limit.Token);
            process.StandardInput.NewLine = "\n";
            foreach (var field in fields)
                await process.StandardInput.WriteLineAsync(Convert.ToBase64String(Utf8.GetBytes(field)).AsMemory(), limit.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(limit.Token);
            var text = await output; _ = await error;
            if (process.ExitCode != 0) throw new ValidationException("El recurso lingüístico rechazó la consulta; no se repetirá.");
            var lines = text.Split('\n');
            if (lines.Length != fields.Length + 1 || lines[^1] != "")
                throw new ValidationException("El recurso lingüístico cambió los límites de los campos.");
            return lines[..^1].Select(line => Utf8.GetString(Convert.FromBase64String(line.TrimEnd('\r')))).ToArray();
        } catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) {
            throw new ValidationException("El recurso lingüístico agotó su plazo; no se repetirá.");
        } catch (Exception failure) when (failure is IOException or FormatException or System.ComponentModel.Win32Exception or DecoderFallbackException) {
            throw new ValidationException("El recurso lingüístico no está disponible o devolvió datos inválidos.");
        } finally {
            limit.Cancel();
            if (started && !process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
            foreach (var read in new[] { output, error }.OfType<Task<string>>())
                try { await read; } catch (Exception failure) when (failure is OperationCanceledException or IOException or DecoderFallbackException) { }
            if (image is not null) await Cleanup(container);
        }
    }

    private void VerifyNative()
    {
        foreach (var (path, expected) in new[] {
            ("/usr/share/apertium/apertium-eng-spa/spa-eng.automorf.bin", "9c273f2e22c6c87e5b2c35ddc97c02e954eff01dfa355b2f9324a1bcd0b9d364"),
            ("/usr/share/apertium/apertium-es-pt/pt-es.automorf.bin", "fc6d6370bcc58043af75fe15787104f3cab8d187153752596f2a6a7fca7959ea"),
            ("/usr/bin/lt-proc", "e80ae89a37f7a08d966c2c7d6fda0c97f3bc97b501716b85c4957bca7f42978b"),
            ("/usr/bin/apertium", "79d6ec404120dc43af0c655867feeb5e64f614694e4f234e2d104d114f479528")
        }) {
            if (!File.Exists(path)) throw new ValidationException("Falta el recurso lingüístico instalado y verificado.");
            using var file = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La huella del recurso lingüístico no coincide.");
        }
        nativeVerified = true;
    }
    private static async Task<string> ReadBounded(StreamReader reader, int maximum, CancellationToken cancellation)
    {
        var result = new StringBuilder(); var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellation)) > 0) {
            if (result.Length + read > maximum) throw new IOException("Salida lingüística excesiva.");
            result.Append(buffer, 0, read);
        }
        return result.ToString();
    }
    private static async Task Cleanup(string container)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var item in new[] { "rm", "--force", container }) start.ArgumentList.Add(item);
        try {
            using var process = Process.Start(start);
            if (process is null) return;
            var output = process.StandardOutput.ReadToEndAsync(limit.Token);
            var error = process.StandardError.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token); await output; await error;
        } catch (Exception failure) when (failure is OperationCanceledException or System.ComponentModel.Win32Exception or IOException) {
            // Only the named disposable tool container is targeted; never widen cleanup to another workload.
        }
    }
}
