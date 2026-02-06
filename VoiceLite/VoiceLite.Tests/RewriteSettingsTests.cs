using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests
{
    public class RewriteSettingsTests
    {
        [Fact]
        public void DefaultSettings_RewriteDisabled()
        {
            var settings = new Settings();
            Assert.False(settings.EnableRewrite);
        }

        [Fact]
        public void DefaultSettings_RewriteHotkey_IsShiftX()
        {
            var settings = new Settings();
            Assert.Equal(Key.X, settings.RewriteHotkey);
            Assert.Equal(ModifierKeys.Shift, settings.RewriteHotkeyModifiers);
        }

        [Fact]
        public void DefaultSettings_HasDefaultRewritePrompts()
        {
            var settings = new Settings();
            Assert.NotNull(settings.RewritePrompts);
            Assert.True(settings.RewritePrompts.Count >= 5);
        }

        [Fact]
        public void DefaultPrompts_ContainsImprove()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.Contains(prompts, p => p.Name == "Improve");
        }

        [Fact]
        public void DefaultPrompts_ContainsFormalize()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.Contains(prompts, p => p.Name == "Formalize");
        }

        [Fact]
        public void DefaultPrompts_ContainsSimplify()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.Contains(prompts, p => p.Name == "Simplify");
        }

        [Fact]
        public void DefaultPrompts_ContainsSummarize()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.Contains(prompts, p => p.Name == "Summarize");
        }

        [Fact]
        public void DefaultPrompts_ContainsFixGrammar()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.Contains(prompts, p => p.Name == "Fix Grammar");
        }

        [Fact]
        public void DefaultPrompts_AllAreBuiltIn()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.All(prompts, p => Assert.True(p.IsBuiltIn));
        }

        [Fact]
        public void DefaultPrompts_AllHaveNonEmptySystemPrompt()
        {
            var prompts = Settings.GetDefaultRewritePrompts();
            Assert.All(prompts, p => Assert.False(string.IsNullOrWhiteSpace(p.SystemPrompt)));
        }

        [Fact]
        public void DefaultSettings_ActivePreset_IsImprove()
        {
            var settings = new Settings();
            Assert.Equal("Improve", settings.ActiveRewritePreset);
        }

        [Fact]
        public void DefaultSettings_LlamaModelPath_IsEmpty()
        {
            var settings = new Settings();
            Assert.Equal("", settings.LlamaModelPath);
        }

        [Fact]
        public void DefaultSettings_LlamaExecutablePath_IsEmpty()
        {
            var settings = new Settings();
            Assert.Equal("", settings.LlamaExecutablePath);
        }

        [Fact]
        public void DefaultSettings_RewriteMaxTokens_Is1024()
        {
            var settings = new Settings();
            Assert.Equal(1024, settings.RewriteMaxTokens);
        }

        [Fact]
        public void DefaultSettings_RewriteTemperature_Is07()
        {
            var settings = new Settings();
            Assert.Equal(0.7, settings.RewriteTemperature);
        }

        [Fact]
        public void RewriteMaxTokens_ClampsToMinimum()
        {
            var settings = new Settings();
            settings.RewriteMaxTokens = 50;
            Assert.Equal(128, settings.RewriteMaxTokens);
        }

        [Fact]
        public void RewriteMaxTokens_ClampsToMaximum()
        {
            var settings = new Settings();
            settings.RewriteMaxTokens = 10000;
            Assert.Equal(4096, settings.RewriteMaxTokens);
        }

        [Fact]
        public void RewriteTemperature_ClampsToMinimum()
        {
            var settings = new Settings();
            settings.RewriteTemperature = -1.0;
            Assert.Equal(0.0, settings.RewriteTemperature);
        }

        [Fact]
        public void RewriteTemperature_ClampsToMaximum()
        {
            var settings = new Settings();
            settings.RewriteTemperature = 5.0;
            Assert.Equal(1.5, settings.RewriteTemperature);
        }

        [Fact]
        public void SettingsValidator_PreservesRewriteSettings()
        {
            var settings = new Settings
            {
                EnableRewrite = true,
                RewriteHotkey = Key.Y,
                RewriteHotkeyModifiers = ModifierKeys.Control,
                LlamaModelPath = @"C:\models\test.gguf",
                RewriteMaxTokens = 2048,
                RewriteTemperature = 0.5,
                ActiveRewritePreset = "Formalize"
            };

            var validated = SettingsValidator.ValidateAndRepair(settings);

            Assert.True(validated.EnableRewrite);
            Assert.Equal(Key.Y, validated.RewriteHotkey);
            Assert.Equal(ModifierKeys.Control, validated.RewriteHotkeyModifiers);
            Assert.Equal(@"C:\models\test.gguf", validated.LlamaModelPath);
            Assert.Equal(2048, validated.RewriteMaxTokens);
            Assert.Equal(0.5, validated.RewriteTemperature);
            Assert.Equal("Formalize", validated.ActiveRewritePreset);
        }

        [Fact]
        public void SettingsValidator_RepairsNullRewritePrompts()
        {
            var settings = new Settings();
            settings.RewritePrompts = null!;

            var validated = SettingsValidator.ValidateAndRepair(settings);

            Assert.NotNull(validated.RewritePrompts);
            Assert.True(validated.RewritePrompts.Count >= 5);
        }

        [Fact]
        public void SettingsValidator_RepairsEmptyRewritePrompts()
        {
            var settings = new Settings();
            settings.RewritePrompts = new List<RewritePromptTemplate>();

            var validated = SettingsValidator.ValidateAndRepair(settings);

            Assert.NotNull(validated.RewritePrompts);
            Assert.True(validated.RewritePrompts.Count >= 5);
        }

        [Fact]
        public void SettingsValidator_ClampsOutOfRangeRewriteMaxTokens()
        {
            var settings = new Settings();
            settings.RewriteMaxTokens = 50; // Below minimum

            var validated = SettingsValidator.ValidateAndRepair(settings);

            Assert.Equal(128, validated.RewriteMaxTokens);
        }

        [Fact]
        public void SettingsValidator_ClampsOutOfRangeRewriteTemperature()
        {
            var settings = new Settings();
            settings.RewriteTemperature = 5.0; // Above maximum

            var validated = SettingsValidator.ValidateAndRepair(settings);

            Assert.Equal(1.5, validated.RewriteTemperature);
        }

        [Fact]
        public void RewritePromptTemplate_DefaultValues()
        {
            var template = new RewritePromptTemplate();
            Assert.Equal("", template.Name);
            Assert.Equal("", template.SystemPrompt);
            Assert.False(template.IsBuiltIn);
        }

        [Fact]
        public void Settings_RewriteSettings_SerializeAndDeserialize()
        {
            var settings = new Settings
            {
                EnableRewrite = true,
                RewriteHotkey = Key.Y,
                RewriteHotkeyModifiers = ModifierKeys.Control,
                LlamaModelPath = @"C:\models\test.gguf",
                RewriteMaxTokens = 2048,
                RewriteTemperature = 0.5,
                ActiveRewritePreset = "Simplify"
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<Settings>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized!.EnableRewrite);
            Assert.Equal(Key.Y, deserialized.RewriteHotkey);
            Assert.Equal(ModifierKeys.Control, deserialized.RewriteHotkeyModifiers);
            Assert.Equal(@"C:\models\test.gguf", deserialized.LlamaModelPath);
            Assert.Equal(2048, deserialized.RewriteMaxTokens);
            Assert.Equal(0.5, deserialized.RewriteTemperature);
            Assert.Equal("Simplify", deserialized.ActiveRewritePreset);
        }

        [Fact]
        public void Settings_RewritePrompts_SerializeAndDeserialize()
        {
            var settings = new Settings();
            settings.RewritePrompts.Add(new RewritePromptTemplate
            {
                Name = "Custom",
                SystemPrompt = "Make it funny",
                IsBuiltIn = false
            });

            var json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<Settings>(json);

            Assert.NotNull(deserialized);
            Assert.Contains(deserialized!.RewritePrompts, p => p.Name == "Custom" && p.SystemPrompt == "Make it funny" && !p.IsBuiltIn);
        }
    }
}
