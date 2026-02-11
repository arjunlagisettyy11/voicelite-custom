using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class LlamaRewriteService : IDisposable
    {
        private readonly Settings settings;
        private readonly SemaphoreSlim rewriteSemaphore = new(1, 1);
        private readonly CancellationTokenSource disposalCts = new();
        private volatile bool isDisposed = false;

        private const int PROCESS_TIMEOUT_SECONDS = 120;
        private const int PROCESS_DISPOSAL_TIMEOUT_MS = 2000;

        public LlamaRewriteService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<string> RewriteAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(LlamaRewriteService));

            if (string.IsNullOrWhiteSpace(text))
                return text;

            bool semaphoreAcquired = false;
            Process? process = null;
            string? tempPromptFile = null;

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalCts.Token);
                await rewriteSemaphore.WaitAsync(linkedCts.Token);
                semaphoreAcquired = true;

                var model = settings.OllamaModel;
                if (string.IsNullOrWhiteSpace(model))
                    model = "gemma3:4b";

                var prompt = $"{systemPrompt}\n\nText to rewrite:\n{text}";

                ErrorLogger.LogWarning($"LlamaRewrite: Starting rewrite with ollama model={model}");
                var sw = Stopwatch.StartNew();

                (process, var outputBuilder, var errorBuilder, tempPromptFile) = StartOllamaProcess(model, prompt);

                bool exited = await Task.Run(() => process.WaitForExit(PROCESS_TIMEOUT_SECONDS * 1000), linkedCts.Token);
                sw.Stop();

                if (!exited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (Exception killEx) { ErrorLogger.LogWarning($"Failed to kill ollama process: {killEx.Message}"); }
                    throw new TimeoutException($"LLM rewrite timed out after {PROCESS_TIMEOUT_SECONDS} seconds.");
                }

                if (process.ExitCode != 0)
                {
                    var error = errorBuilder.ToString();
                    ErrorLogger.LogWarning($"ollama failed (exit code {process.ExitCode}) after {sw.ElapsedMilliseconds}ms: {error.Substring(0, Math.Min(error.Length, 2000))}");
                    throw new InvalidOperationException($"ollama process failed with exit code {process.ExitCode}");
                }

                var result = outputBuilder.ToString().Trim();
                ErrorLogger.LogWarning($"LlamaRewrite: Completed in {sw.ElapsedMilliseconds}ms, result length: {result.Length} chars");

                return string.IsNullOrWhiteSpace(result) ? text : result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not TimeoutException && ex is not InvalidOperationException)
            {
                ErrorLogger.LogError("LlamaRewriteService.RewriteAsync", ex);
                throw;
            }
            finally
            {
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { ErrorLogger.LogWarning($"Ollama process cleanup failed: {ex.Message}"); }

                    try
                    {
                        var disposeTask = Task.Run(() => { try { process.Dispose(); } catch { } });
                        disposeTask.Wait(PROCESS_DISPOSAL_TIMEOUT_MS);
                    }
                    catch { }
                }

                // Clean up temp prompt file
                if (tempPromptFile != null)
                {
                    try { File.Delete(tempPromptFile); }
                    catch (Exception cleanupEx) { ErrorLogger.LogWarning($"Failed to delete temp prompt file: {cleanupEx.Message}"); }
                }

                if (semaphoreAcquired)
                {
                    try { rewriteSemaphore.Release(); }
                    catch (ObjectDisposedException) { }
                    catch (SemaphoreFullException) { }
                }
            }
        }

        private (Process process, StringBuilder outputBuilder, StringBuilder errorBuilder, string tempPromptFile) StartOllamaProcess(string model, string prompt)
        {
            var ollamaPath = ResolveOllamaPath();

            // Write prompt to a temp file to avoid Ollama's readline 2048-byte buffer panic.
            // When stdin is piped directly, Ollama uses a Go readline library that panics on
            // input lines exceeding 2048 bytes. Redirecting stdin from a file via cmd.exe
            // bypasses readline entirely since Ollama detects non-interactive stdin.
            var tempPromptFile = Path.Combine(Path.GetTempPath(), $"voicelite_prompt_{Guid.NewGuid():N}.txt");
            File.WriteAllText(tempPromptFile, prompt, Encoding.UTF8);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{ollamaPath}\" run {model} < \"{tempPromptFile}\"\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder(4096);
            var errorBuilder = new StringBuilder(512);

            const int MAX_OUTPUT_SIZE = 1024 * 1024;
            const int MAX_ERROR_SIZE = 64 * 1024;

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    int newLength = outputBuilder.Length + e.Data.Length + Environment.NewLine.Length;
                    if (newLength <= MAX_OUTPUT_SIZE)
                        outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    int newLength = errorBuilder.Length + e.Data.Length + Environment.NewLine.Length;
                    if (newLength <= MAX_ERROR_SIZE)
                        errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return (process, outputBuilder, errorBuilder, tempPromptFile);
        }

        private string ResolveOllamaPath()
        {
            // Check common Ollama install locations
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                @"C:\Program Files\Ollama\ollama.exe",
                "ollama.exe" // PATH fallback
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            // Default to PATH-based resolution
            return "ollama.exe";
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            try { disposalCts.Cancel(); }
            catch (Exception ex) { ErrorLogger.LogWarning($"LlamaRewriteService disposal cancel failed: {ex.Message}"); }

            rewriteSemaphore.SafeDispose();
            disposalCts.SafeDispose();
        }
    }
}
