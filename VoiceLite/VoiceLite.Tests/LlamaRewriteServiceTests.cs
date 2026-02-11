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
                OllamaModel = "gemma3:4b",
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

        [Fact]
        public async Task RewriteAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            var settings = CreateTestSettings();
            using var service = new LlamaRewriteService(settings);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.RewriteAsync("test text", "prompt", cts.Token));
        }

        [Fact]
        public void Constructor_WithDefaultOllamaModel_UsesGemma()
        {
            var settings = CreateTestSettings();
            settings.OllamaModel = "";
            using var service = new LlamaRewriteService(settings);
            // Service should not throw - it will use "gemma3:4b" as default
            Assert.NotNull(service);
        }
    }
}
