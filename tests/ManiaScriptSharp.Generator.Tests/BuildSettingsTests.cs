using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class BuildSettingsTests
{
    // Minimal concrete AnalyzerConfigOptions backed by a dictionary — avoids NSubstitute for this pure parsing class.
    private sealed class DictOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;
        public DictOptions(Dictionary<string, string> values) { _values = values; }
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) => _values.TryGetValue(key, out value);
    }

    private static BuildSettings FromDict(Dictionary<string, string> values) =>
        BuildSettings.FromOptions(new DictOptions(values));

    [Fact]
    public void Default_OutputDir_IsManiaScript()
    {
        Assert.Equal("ManiaScript", BuildSettings.Default.OutputDir);
    }

    [Fact]
    public void Default_UseSpaces_IsTrue()
    {
        Assert.True(BuildSettings.Default.UseSpaces);
    }

    [Fact]
    public void Default_IndentSize_IsFour()
    {
        Assert.Equal(4, BuildSettings.Default.IndentSize);
    }

    [Fact]
    public void FromOptions_NoOptions_ReturnsDefaults()
    {
        var s = FromDict([]);
        Assert.Equal("ManiaScript", s.OutputDir);
        Assert.Equal(4, s.IndentSize);
        Assert.True(s.UseSpaces);
    }

    [Fact]
    public void FromOptions_CustomOutputDir()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptOutputDir"] = "scripts" });
        Assert.Equal("scripts", s.OutputDir);
    }

    [Fact]
    public void FromOptions_WhitespaceOutputDir_FallsBackToDefault()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptOutputDir"] = "   " });
        Assert.Equal("ManiaScript", s.OutputDir);
    }

    [Fact]
    public void FromOptions_SpacesStyle_UseSpacesTrue()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentStyle"] = "spaces" });
        Assert.True(s.UseSpaces);
    }

    [Fact]
    public void FromOptions_SpacesStyle_DefaultIndentSizeIsFour()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentStyle"] = "spaces" });
        Assert.Equal(4, s.IndentSize);
    }

    [Fact]
    public void FromOptions_TabsStyle_DefaultIndentSizeIsOne()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentStyle"] = "tabs" });
        Assert.Equal(1, s.IndentSize);
        Assert.False(s.UseSpaces);
    }

    [Fact]
    public void FromOptions_SpacesCaseInsensitive()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentStyle"] = "SPACES" });
        Assert.True(s.UseSpaces);
    }

    [Fact]
    public void FromOptions_CustomIndentSize()
    {
        var s = FromDict(new()
        {
            ["build_property.ManiaScriptIndentStyle"] = "spaces",
            ["build_property.ManiaScriptIndentSize"] = "2"
        });
        Assert.Equal(2, s.IndentSize);
    }

    [Fact]
    public void FromOptions_InvalidIndentSize_UsesDefault()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentSize"] = "abc" });
        // Default style is spaces → default size is 4
        Assert.Equal(4, s.IndentSize);
    }

    [Fact]
    public void FromOptions_ZeroIndentSize_UsesDefault()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptIndentSize"] = "0" });
        Assert.Equal(4, s.IndentSize);
    }
}
