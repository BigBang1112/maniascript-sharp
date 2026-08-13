using Microsoft.CodeAnalysis.Diagnostics;

namespace ManiaScriptSharp.ApiGenerator;

/// <summary>
/// Generator configuration read from MSBuild properties exposed via
/// <c>CompilerVisibleProperty</c>. Consumers set these in their <c>.csproj</c>:
/// <code>
/// &lt;PropertyGroup&gt;
///   &lt;ManiaScriptApiNamespaceLibsStatic&gt;true&lt;/ManiaScriptApiNamespaceLibsStatic&gt;
///   &lt;ManiaScriptApiStandardizeParamNames&gt;true&lt;/ManiaScriptApiStandardizeParamNames&gt;
/// &lt;/PropertyGroup&gt;
/// </code>
/// </summary>
internal sealed class ApiGeneratorSettings
{
    /// <summary>
    /// When <c>true</c> (default), namespace libraries such as <c>MathLib</c> and
    /// <c>TextLib</c> are emitted as <c>public static partial class</c> with
    /// <c>static</c> members. When <c>false</c>, they are emitted as
    /// <c>public partial class</c> with <c>virtual</c> instance members so that
    /// user code can subclass and override individual methods.
    /// </summary>
    public bool NamespaceLibsStatic { get; }

    /// <summary>
    /// When <c>true</c> (default), method parameter names are converted to C# camelCase
    /// (leading underscores stripped, first letter lowercased). When <c>false</c>,
    /// the original names from the header are kept verbatim.
    /// </summary>
    public bool StandardizeParamNames { get; }

    public static readonly ApiGeneratorSettings Default = new(namespaceLibsStatic: true, standardizeParamNames: true);

    /// <summary>Creates a settings instance directly — for use in tests.</summary>
    internal static ApiGeneratorSettings ForTesting(bool namespaceLibsStatic = true, bool standardizeParamNames = true) =>
        new(namespaceLibsStatic, standardizeParamNames);

    private ApiGeneratorSettings(bool namespaceLibsStatic, bool standardizeParamNames)
    {
        NamespaceLibsStatic = namespaceLibsStatic;
        StandardizeParamNames = standardizeParamNames;
    }

    public static ApiGeneratorSettings From(AnalyzerConfigOptions globalOptions)
    {
        globalOptions.TryGetValue("build_property.ManiaScriptApiNamespaceLibsStatic", out var staticStr);
        globalOptions.TryGetValue("build_property.ManiaScriptApiStandardizeParamNames", out var standardizeStr);

        var namespaceLibsStatic = string.Equals(staticStr, "true", StringComparison.OrdinalIgnoreCase);
        var standardizeParamNames = string.Equals(standardizeStr, "true", StringComparison.OrdinalIgnoreCase);

        return new ApiGeneratorSettings(namespaceLibsStatic, standardizeParamNames);
    }
}
