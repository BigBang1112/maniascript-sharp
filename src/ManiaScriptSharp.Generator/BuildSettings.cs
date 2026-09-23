using Microsoft.CodeAnalysis.Diagnostics;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Generator configuration read from MSBuild properties exposed via
/// <c>CompilerVisibleProperty</c>. Consumers set these in their <c>.csproj</c>:
/// <code>
/// &lt;PropertyGroup&gt;
///   &lt;ManiaScriptOutputDir&gt;ManiaScript&lt;/ManiaScriptOutputDir&gt;
///   &lt;!-- Put machine-local ManiaScriptAdditionalOutputDirs in the ignored .csproj.user file. --&gt;
///   &lt;ManiaScriptIndentSize&gt;4&lt;/ManiaScriptIndentSize&gt;
///   &lt;ManiaScriptIndentStyle&gt;spaces&lt;/ManiaScriptIndentStyle&gt;
///   &lt;ManiaScriptVersion&gt;1&lt;/ManiaScriptVersion&gt;
/// (these are also the defaults when omitted)
/// &lt;/PropertyGroup&gt;
/// </code>
/// </summary>
internal sealed class BuildSettings
{
    /// <summary>Destination folder for .Script.txt files (relative to project dir or absolute).</summary>
    public string OutputDir { get; }

    /// <summary>Additional destination folders that mirror files written to <see cref="OutputDir"/>.</summary>
    public IReadOnlyList<string> AdditionalOutputDirs { get; }

    /// <summary>Number of characters per indent level (default 4 for spaces, 1 for tabs).</summary>
    public int IndentSize { get; }

    /// <summary>Whether to indent with spaces (<c>true</c>, default) or tabs (<c>false</c>).</summary>
    public bool UseSpaces { get; }

    /// <summary>Target ManiaScript language version. Version 1 is the default.</summary>
    public int ManiaScriptVersion { get; }

    public static readonly BuildSettings Default = new("ManiaScript", [], 4, true, 1);
    internal static readonly BuildSettings Version2 = new("ManiaScript", [], 4, true, 2);

    private BuildSettings(
        string outputDir,
        IReadOnlyList<string> additionalOutputDirs,
        int indentSize,
        bool useSpaces,
        int maniaScriptVersion)
    {
        OutputDir = outputDir;
        AdditionalOutputDirs = additionalOutputDirs;
        IndentSize = indentSize;
        UseSpaces = useSpaces;
        ManiaScriptVersion = maniaScriptVersion;
    }

    /// <summary>
    /// Reads configuration from MSBuild properties exposed through analyzer global options.
    /// </summary>
    public static BuildSettings FromOptions(AnalyzerConfigOptions globalOptions)
    {
        globalOptions.TryGetValue("build_property.ManiaScriptOutputDir", out var outputDir);
        globalOptions.TryGetValue("build_property.ManiaScriptAdditionalOutputDirs", out var additionalOutputDirs);
        globalOptions.TryGetValue("build_property.ManiaScriptIndentSize", out var indentSizeStr);
        globalOptions.TryGetValue("build_property.ManiaScriptIndentStyle", out var indentStyle);
        globalOptions.TryGetValue("build_property.ManiaScriptVersion", out var maniaScriptVersionStr);

        // Spaces is the default style; only an explicit "tabs" opts out.
        var useSpaces = indentStyle?.Equals("tabs", StringComparison.OrdinalIgnoreCase) != true;
        var defaultSize = useSpaces ? 4 : 1;
        var indentSize = int.TryParse(indentSizeStr, out var parsed) && parsed > 0 ? parsed : defaultSize;
        var maniaScriptVersion = int.TryParse(maniaScriptVersionStr, out var parsedVersion) && parsedVersion == 2
            ? 2
            : 1;
        var mirrors = (additionalOutputDirs ?? "")
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => path.Trim())
            .Where(static path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BuildSettings(
            string.IsNullOrWhiteSpace(outputDir) ? "ManiaScript" : outputDir!,
            mirrors,
            indentSize,
            useSpaces,
            maniaScriptVersion);
    }
}
