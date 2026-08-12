using Microsoft.CodeAnalysis.Diagnostics;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Generator configuration read from MSBuild properties exposed via
/// <c>CompilerVisibleProperty</c>. Consumers set these in their <c>.csproj</c>:
/// <code>
/// &lt;PropertyGroup&gt;
///   &lt;ManiaScriptOutputDir&gt;ManiaScript&lt;/ManiaScriptOutputDir&gt;
///   &lt;ManiaScriptIndentSize&gt;4&lt;/ManiaScriptIndentSize&gt;
///   &lt;ManiaScriptIndentStyle&gt;spaces&lt;/ManiaScriptIndentStyle&gt;
/// (these are also the defaults when omitted)
/// &lt;/PropertyGroup&gt;
/// </code>
/// </summary>
internal sealed class BuildSettings
{
    /// <summary>Destination folder for .Script.txt files (relative to project dir or absolute).</summary>
    public string OutputDir { get; }

    /// <summary>Number of characters per indent level (default 4 for spaces, 1 for tabs).</summary>
    public int IndentSize { get; }

    /// <summary>Whether to indent with spaces (<c>true</c>, default) or tabs (<c>false</c>).</summary>
    public bool UseSpaces { get; }

    public static readonly BuildSettings Default = new("ManiaScript", 4, true);

    private BuildSettings(string outputDir, int indentSize, bool useSpaces)
    {
        OutputDir = outputDir;
        IndentSize = indentSize;
        UseSpaces = useSpaces;
    }

    /// <summary>
    /// Reads configuration from MSBuild properties exposed through analyzer global options.
    /// </summary>
    public static BuildSettings FromOptions(AnalyzerConfigOptions globalOptions)
    {
        globalOptions.TryGetValue("build_property.ManiaScriptOutputDir", out var outputDir);
        globalOptions.TryGetValue("build_property.ManiaScriptIndentSize", out var indentSizeStr);
        globalOptions.TryGetValue("build_property.ManiaScriptIndentStyle", out var indentStyle);

        // Spaces is the default style; only an explicit "tabs" opts out.
        var useSpaces = indentStyle?.Equals("tabs", StringComparison.OrdinalIgnoreCase) != true;
        var defaultSize = useSpaces ? 4 : 1;
        var indentSize = int.TryParse(indentSizeStr, out var parsed) && parsed > 0 ? parsed : defaultSize;

        return new BuildSettings(
            string.IsNullOrWhiteSpace(outputDir) ? "ManiaScript" : outputDir!,
            indentSize,
            useSpaces);
    }
}
