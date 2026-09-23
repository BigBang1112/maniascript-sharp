using System.Collections.Generic;
using System.IO;
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
        Assert.Empty(BuildSettings.Default.AdditionalOutputDirs);
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
    public void Default_ManiaScriptVersion_IsOne()
    {
        Assert.Equal(1, BuildSettings.Default.ManiaScriptVersion);
    }

    [Fact]
    public void FromOptions_NoOptions_ReturnsDefaults()
    {
        var s = FromDict([]);
        Assert.Equal("ManiaScript", s.OutputDir);
        Assert.Equal(4, s.IndentSize);
        Assert.True(s.UseSpaces);
        Assert.Equal(1, s.ManiaScriptVersion);
    }

    [Fact]
    public void FromOptions_ManiaScriptVersionTwo_EnablesVersionTwo()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptVersion"] = "2" });

        Assert.Equal(2, s.ManiaScriptVersion);
    }

    [Fact]
    public void FromOptions_UnsupportedManiaScriptVersion_FallsBackToVersionOne()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptVersion"] = "3" });

        Assert.Equal(1, s.ManiaScriptVersion);
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
    public void FromOptions_AdditionalOutputDirs_AreOrderedTrimmedAndDeduplicated()
    {
        var s = FromDict(new()
        {
            ["build_property.ManiaScriptAdditionalOutputDirs"] = " ../Server/Scripts ; ;../Client/Scripts;../server/scripts "
        });

        Assert.Equal(["../Server/Scripts", "../Client/Scripts"], s.AdditionalOutputDirs);
    }

    [Fact]
    public void FromOptions_WhitespaceAdditionalOutputDirs_IsEmpty()
    {
        var s = FromDict(new() { ["build_property.ManiaScriptAdditionalOutputDirs"] = " ;  ; " });

        Assert.Empty(s.AdditionalOutputDirs);
    }

    [Fact]
    public void ResolveOutputPaths_PrimaryComesFirstAndMirrorsUseSameFileName()
    {
        var settings = FromDict(new()
        {
            ["build_property.ManiaScriptOutputDir"] = "Primary",
            ["build_property.ManiaScriptAdditionalOutputDirs"] = "../Mirror;Primary"
        });
        var projectDir = Path.Combine(Path.GetTempPath(), "ManiaScriptSharp", "Project");

        var paths = ManiaScriptGenerator.ResolveOutputPaths("MyMode", "MyProject.Modes.TrackMania", "MyProject", settings, projectDir);

        Assert.Equal(2, paths.Count);
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "Primary", "Modes", "TrackMania", "MyMode.Script.txt")), paths[0]);
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "../Mirror", "Modes", "TrackMania", "MyMode.Script.txt")), paths[1]);
    }

    [Fact]
    public void ResolveOutputPaths_UsesRequestedExtensionForEveryDestination()
    {
        var settings = FromDict(new()
        {
            ["build_property.ManiaScriptAdditionalOutputDirs"] = "Mirror"
        });

        var paths = ManiaScriptGenerator.ResolveOutputPaths("MyManialink", "MyProject.UI.Pages", "MyProject", settings, Path.GetTempPath(), ".xml");

        Assert.All(paths, path => Assert.EndsWith(Path.Combine("UI", "Pages", "MyManialink.xml"), path));
    }

    [Fact]
    public void ResolveOutputPaths_InvalidMirrorReportsErrorAndKeepsPrimary()
    {
        var settings = FromDict(new()
        {
            ["build_property.ManiaScriptOutputDir"] = "Primary",
            ["build_property.ManiaScriptAdditionalOutputDirs"] = "\0invalid"
        });
        var errors = new List<(string Path, System.Exception Exception)>();

        var paths = ManiaScriptGenerator.ResolveOutputPaths(
            "MyMode",
            "",
            "MyProject",
            settings,
            Path.GetTempPath(),
            onAdditionalError: (path, exception) => errors.Add((path, exception)));

        Assert.Single(paths);
        Assert.EndsWith(Path.Combine("Primary", "MyMode.Script.txt"), paths[0]);
        Assert.Single(errors);
        Assert.Equal("\0invalid", errors[0].Path);
    }

    [Theory]
    [InlineData("", "MyLib.Script.txt")]
    [InlineData("ManiaScriptSharp", "MyLib.Script.txt")]
    [InlineData("ManiaScriptSharp.Scripts", "Scripts/MyLib.Script.txt")]
    [InlineData("ManiaScriptSharp.Scripts.Libs.Nadeo", "Libs/Nadeo/MyLib.Script.txt")]
    [InlineData("ManiaScriptSharp.Libs.Nadeo", "Libs/Nadeo/MyLib.Script.txt")]
    public void ResolveOutputPaths_UsesSameNamespacePathAsIncludes(string namespaceName, string relativePath)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "ManiaScriptSharp", "Project");

        var paths = ManiaScriptGenerator.ResolveOutputPaths("MyLib", namespaceName, "MyProject", BuildSettings.Default, projectDir);

        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "ManiaScript", relativePath.Replace('/', Path.DirectorySeparatorChar))), paths[0]);
    }

    [Theory]
    [InlineData("MyProject", "MyMode.Script.txt")]
    [InlineData("MyProject.Libs.Components", "Libs/Components/MyMode.Script.txt")]
    [InlineData("MyProjectExtra.Libs", "MyProjectExtra/Libs/MyMode.Script.txt")]
    [InlineData("Company.MyProject.Libs", "Company/MyProject/Libs/MyMode.Script.txt")]
    public void ResolveOutputPaths_OmitsOnlyMatchingProjectRootNamespace(string namespaceName, string relativePath)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "ManiaScriptSharp", "Project");

        var paths = ManiaScriptGenerator.ResolveOutputPaths("MyMode", namespaceName, "MyProject", BuildSettings.Default, projectDir);

        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "ManiaScript", relativePath.Replace('/', Path.DirectorySeparatorChar))), paths[0]);
    }

    [Fact]
    public void ResolveOutputPaths_OmitsDottedRootNamespace()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "ManiaScriptSharp", "Project");

        var paths = ManiaScriptGenerator.ResolveOutputPaths(
            "MyMode", "Company.MyProject.Modes", "Company.MyProject", BuildSettings.Default, projectDir);

        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "ManiaScript", "Modes", "MyMode.Script.txt")), paths[0]);
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
