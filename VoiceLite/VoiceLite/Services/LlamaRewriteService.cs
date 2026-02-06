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
        private readonly string baseDir;
        private readonly SemaphoreSlim rewriteSemaphore = new(1, 1);
        private readonly CancellationTokenSource disposalCts = new();
        private volatile bool isDisposed = false;

        private const int PROCESS_TIMEOUT_SECONDS = 120;
        private const int PROCESS_DISPOSAL_TIMEOUT_MS = 2000;

        public LlamaRewriteService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task<string> RewriteAsync(string text, string systemPrompt, CancellationToken cancellationToken = default)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(LlamaRewriteService));

            if (string.IsNullOrWhiteSpace(text))
                return text;

            var llamaExePath = ResolveLlamaExePath();
            if (string.IsNullOrEmpty(llamaExePath) || !File.Exists(llamaExePath))
                throw new FileNotFoundException("llama-cli executable not found. Please configure the path in Settings.");

            var modelPath = ResolveLlamaModelPath();
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                throw new FileNotFoundException("LLM model file not found. Please configure the model path in Settings.");

            bool semaphoreAcquired = false;
            Process? process = null;

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalCts.Token);
                await rewriteSemaphore.WaitAsync(linkedCts.Token);
                semaphoreAcquired = true;

                var arguments = BuildLlamaArguments(text, systemPrompt, modelPath);

                ErrorLogger.LogWarning($"LlamaRewrite: Starting rewrite with model={Path.GetFileName(modelPath)}");

                (process, var outputBuilder, var errorBuilder) = ExecuteLlamaProcess(llamaExePath, arguments);

                bool exited = await Task.Run(() => process.WaitForExit(PROCESS_TIMEOUT_SECONDS * 1000), linkedCts.Token);

                if (!exited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killEx)
                    {
                        ErrorLogger.LogWarning($"Failed to kill llama-cli process: {killEx.Message}");
                    }
                    throw new TimeoutException($"LLM rewrite timed out after {PROCESS_TIMEOUT_SECONDS} seconds.");
                }

                if (process.ExitCode != 0)
                {
                    var error = errorBuilder.ToString();
                    ErrorLogger.LogWarning($"llama-cli failed with exit code {process.ExitCode}: {error.Substring(0, Math.Min(error.Length, 500))}");
                    throw new InvalidOperationException($"llama-cli process failed with exit code {process.ExitCode}");
                }

                var result = outputBuilder.ToString().Trim();
                ErrorLogger.LogWarning($"LlamaRewrite: Completed, result length: {result.Length} chars");

                return string.IsNullOrWhiteSpace(result) ? text : result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not TimeoutException && ex is not FileNotFoundException && ex is not InvalidOperationException)
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
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { ErrorLogger.LogWarning($"Llama process cleanup failed: {ex.Message}"); }

                    try
                    {
                        var disposeTask = Task.Run(() => { try { process.Dispose(); } catch { } });
                        disposeTask.Wait(PROCESS_DISPOSAL_TIMEOUT_MS);
                    }
                    catch { }
                }

                if (semaphoreAcquired)
                {
                    try { rewriteSemaphore.Release(); }
                    catch (ObjectDisposedException) { }
                    catch (SemaphoreFullException) { }
                }
            }
        }

        public string ResolveLlamaExePath()
        {
            // Priority 1: User-configured path
            if (!string.IsNullOrEmpty(settings.LlamaExecutablePath) && File.Exists(settings.LlamaExecutablePath))
                return settings.LlamaExecutablePath;

            // Priority 2: Bundled with application
            var bundledPath = Path.Combine(baseDir, "llama", "llama-cli.exe");
            if (File.Exists(bundledPath))
                return bundledPath;

            // Priority 3: User-downloaded in LocalApplicationData
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite", "llama", "llama-cli.exe");
            if (File.Exists(localDataPath))
                return localDataPath;

            return "";
        }

        public string ResolveLlamaModelPath()
        {
            // Priority 1: User-configured path
            if (!string.IsNullOrEmpty(settings.LlamaModelPath) && File.Exists(settings.LlamaModelPath))
                return settings.LlamaModelPath;

            // Priority 2: Bundled with application
            var llamaDir = Path.Combine(baseDir, "llama");
            if (Directory.Exists(llamaDir))
            {
                var ggufFiles = Directory.GetFiles(llamaDir, "*.gguf");
                if (ggufFiles.Length > 0)
                    return ggufFiles[0];
            }

            // Priority 3: User-downloaded in LocalApplicationData
            var localDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite", "llama", "models");
            if (Directory.Exists(localDataDir))
            {
                var ggufFiles = Directory.GetFiles(localDataDir, "*.gguf");
                if (ggufFiles.Length > 0)
                    return ggufFiles[0];
            }

            return "";
        }

        public string BuildLlamaArguments(string text, string systemPrompt, string modelPath)
        {
            // Sanitize text to prevent command injection via the prompt
            var safeText = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var safePrompt = systemPrompt.Replace("\\", "\\\\").Replace("\"", "\\\"");

            var fullPrompt = $"{safePrompt}\\n\\nText to rewrite:\\n{safeText}";

            var temperature = settings.RewriteTemperature;
            var maxTokens = settings.RewriteMaxTokens;

            return $"-m \"{modelPath}\" " +
                   $"-p \"{fullPrompt}\" " +
                   $"--temp {temperature:F1} " +
                   $"-n {maxTokens} " +
                   "--no-display-prompt " +
                   "-ngl 99";
        }

        private (Process process, StringBuilder outputBuilder, StringBuilder errorBuilder) ExecuteLlamaProcess(string exePath, string arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
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

            try
            {
                process.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to set llama-cli process priority: {ex.Message}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return (process, outputBuilder, errorBuilder);
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
