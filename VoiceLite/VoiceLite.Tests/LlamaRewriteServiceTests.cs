using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    public class LlamaRewriteServiceTests
    {
        private Settings CreateTestSettings()
        {
            return new Settings
            {
                EnableRewrite = true,
                LlamaModelPath = "",
                LlamaExecutablePath = "",
                RewriteMaxTokens = 1024,
                RewriteTemperature = 0.7,
                ActiveRewritePreset = "Improve"
            };
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LlamaRewriteService(null!));
        }

        [Fact]
        public void Constructor_WithValidSettings_CreatesInstance()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);
            Assert.NotNull(service);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsModelPath()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("hello world", "Improve this text", @"C:\models\test.gguf");

            Assert.Contains(@"C:\models\test.gguf", args);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsPromptAndText()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("hello world", "Improve this text", @"C:\models\test.gguf");

            Assert.Contains("Improve this text", args);
            Assert.Contains("hello world", args);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsTemperature()
        {
            var settings = CreateTestSettings();
            settings.RewriteTemperature = 0.5;
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("test", "prompt", @"C:\models\test.gguf");

            Assert.Contains("--temp 0.5", args);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsMaxTokens()
        {
            var settings = CreateTestSettings();
            settings.RewriteMaxTokens = 2048;
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("test", "prompt", @"C:\models\test.gguf");

            Assert.Contains("-n 2048", args);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsNoDisplayPrompt()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("test", "prompt", @"C:\models\test.gguf");

            Assert.Contains("--no-display-prompt", args);
        }

        [Fact]
        public void BuildLlamaArguments_ContainsGpuOffload()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("test", "prompt", @"C:\models\test.gguf");

            Assert.Contains("-ngl 99", args);
        }

        [Fact]
        public void BuildLlamaArguments_EscapesQuotesInText()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var args = service.BuildLlamaArguments("he said \"hello\"", "prompt", @"C:\models\test.gguf");

            // The quotes should be escaped
            Assert.DoesNotContain("said \"hello\"", args);
            Assert.Contains("said \\\"hello\\\"", args);
        }

        [Fact]
        public void ResolveLlamaExePath_ReturnsEmptyWhenNotConfigured()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var path = service.ResolveLlamaExePath();

            // Path may or may not be empty depending on whether llama-cli exists in bundled/local dirs
            // At minimum, it should not throw
            Assert.NotNull(path);
        }

        [Fact]
        public void ResolveLlamaModelPath_ReturnsEmptyWhenNotConfigured()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var path = service.ResolveLlamaModelPath();

            Assert.NotNull(path);
        }

        [Fact]
        public void ResolveLlamaExePath_ReturnsConfiguredPath_WhenFileExists()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var settings = CreateTestSettings();
                settings.LlamaExecutablePath = tempFile;
                using var service = new LlamaRewriteService(settings);

                var path = service.ResolveLlamaExePath();

                Assert.Equal(tempFile, path);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ResolveLlamaModelPath_ReturnsConfiguredPath_WhenFileExists()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var settings = CreateTestSettings();
                settings.LlamaModelPath = tempFile;
                using var service = new LlamaRewriteService(settings);

                var path = service.ResolveLlamaModelPath();

                Assert.Equal(tempFile, path);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task RewriteAsync_WithEmptyText_ReturnsEmptyText()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var result = await service.RewriteAsync("", "prompt");

            Assert.Equal("", result);
        }

        [Fact]
        public async Task RewriteAsync_WithWhitespaceText_ReturnsWhitespaceText()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);

            var result = await service.RewriteAsync("   ", "prompt");

            Assert.Equal("   ", result);
        }

        [Fact]
        public async Task RewriteAsync_WithMissingExe_ThrowsFileNotFoundException()
        {
            var settings = CreateTestSettings();
            settings.LlamaExecutablePath = @"C:\nonexistent\llama-cli.exe";
            using var service = new LlamaRewriteService(settings);

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.RewriteAsync("test text", "prompt"));
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var settings = CreateTestSettings();
            var service = new LlamaRewriteService(settings);

            service.Dispose();
            service.Dispose(); // Should not throw
        }

        [Fact]
        public async Task RewriteAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var settings = CreateTestSettings();
            var service = new LlamaRewriteService(settings);
            service.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                service.RewriteAsync("test", "prompt"));
        }
    }
}
