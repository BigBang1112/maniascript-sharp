using System.Text;
using System.Xml.Linq;
using ManiaScriptSharp.Generator.Emission;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Incremental source generator that translates classes implementing <c>IContext</c> into
/// ManiaScript (.Script.txt) files on disk in real time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ManiaScriptGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settingsProvider = context.AnalyzerConfigOptionsProvider
            .Select((opts, _) =>
            {
                opts.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var dir);
                opts.GlobalOptions.TryGetValue("build_property.MSBuildProjectName", out var name);
                var settings = BuildSettings.FromOptions(opts.GlobalOptions);
                return (Dir: dir ?? "", Name: name ?? "Generated", Settings: settings);
            });

        var contextClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, ct) =>
                {
                    var decl = (ClassDeclarationSyntax)ctx.Node;
                    if (ctx.SemanticModel.GetDeclaredSymbol(decl, ct) is not INamedTypeSymbol symbol) return null;
                    if (!ImplementsIContext(symbol)) return null;
                    return new ContextClassInfo(decl, symbol, ctx.SemanticModel);
                })
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var combined = contextClasses.Combine(settingsProvider);

        // ── ILib<T> pipeline: generate a .Script.txt for each lib class ──────────────
        var libClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, ct) =>
                {
                    var decl = (ClassDeclarationSyntax)ctx.Node;
                    if (ctx.SemanticModel.GetDeclaredSymbol(decl, ct) is not INamedTypeSymbol symbol) return null;
                    if (!ImplementsILib(symbol)) return null;
                    return new ContextClassInfo(decl, symbol, ctx.SemanticModel);
                })
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(libClasses.Combine(settingsProvider), static (spc, tuple) =>
        {
            var (info, proj) = tuple;
            try
            {
                var emitter = new ScriptEmitter(info, spc, proj.Settings);
                var script = emitter.Emit();
                var outputPath = ResolveOutputPath(info, proj.Settings, proj.Dir, proj.Name);
                WriteScriptFile(outputPath, script, spc);

                spc.AddSource(
                    $"{info.Symbol.Name}.lib.g.cs",
                    SourceText.From(
                        $"// Generated lib ManiaScript at: {outputPath}\n// Length: {script.Length} chars\n",
                        Encoding.UTF8));
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EmissionFailed,
                    info.Declaration.Identifier.GetLocation(),
                    info.Symbol.Name, ex.Message));
            }
        });
        // ─────────────────────────────────────────────────────────────────────────────

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (info, proj) = tuple;
            try
            {
                var xmlTemplate = TryReadXmlTemplate(info);
                var effectiveInfo = xmlTemplate is not null
                    ? new ContextClassInfo(info.Declaration, info.Symbol, info.Model, isManialink: true)
                    : info;

                var emitter = new ScriptEmitter(effectiveInfo, spc, proj.Settings);
                var script = emitter.Emit();

                if (xmlTemplate is not null)
                {
                    if (!ValidateManialinkTemplate(xmlTemplate, out var xmlError))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.InvalidXmlTemplate,
                            info.Declaration.Identifier.GetLocation(),
                            info.Symbol.Name, xmlError));
                        return;
                    }
                    ValidateManialinkBindings(xmlTemplate, emitter.ManialinkBindings, spc, info);
                    var outputPath = ResolveOutputPath(info, proj.Settings, proj.Dir, proj.Name, ".xml");
                    var merged = MergeIntoManialink(xmlTemplate, script, info.Symbol.Name);
                    WriteScriptFile(outputPath, merged, spc);
                }
                else
                {
                    var outputPath = ResolveOutputPath(info, proj.Settings, proj.Dir, proj.Name);
                    WriteScriptFile(outputPath, script, spc);
                }

                spc.AddSource(
                    $"{info.Symbol.Name}.g.cs",
                    SourceText.From(
                        $"// Generated ManiaScript at: {info.Symbol.Name}\n// Length: {script.Length} chars\n",
                        Encoding.UTF8));
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EmissionFailed,
                    info.Declaration.Identifier.GetLocation(),
                    info.Symbol.Name, ex.Message));
            }
        });
    }

    private static bool ImplementsIContext(INamedTypeSymbol symbol)
    {
        foreach (var i in symbol.AllInterfaces)
            if (i.Name == "IContext" && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp")
                return true;
        return false;
    }

    private static bool ImplementsILib(INamedTypeSymbol symbol)
    {
        foreach (var i in symbol.AllInterfaces)
            if (i.IsGenericType && i.Name == "ILib"
                && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp"
                && i.TypeArguments.Length == 1)
                return true;
        return false;
    }

    private static string ResolveOutputPath(ContextClassInfo info, BuildSettings settings, string projectDir, string projectName, string extension = ".Script.txt")
    {
        var root = settings.OutputDir;
        if (!Path.IsPathRooted(root)) root = Path.Combine(projectDir, root);

        var ns = info.Symbol.ContainingNamespace?.ToDisplayString() ?? "";
        var relative = ns.Replace('.', Path.DirectorySeparatorChar);
        var fileName = info.Symbol.Name + extension;
        return string.IsNullOrEmpty(relative) ? Path.Combine(root, fileName) : Path.Combine(root, relative, fileName);
    }

    /// <summary>
    /// Looks for an XML template file alongside the declaring .cs file.
    /// Returns the file contents if found, <see langword="null"/> otherwise.
    /// </summary>
    private static string? TryReadXmlTemplate(ContextClassInfo info)
    {
        var csPath = info.Declaration.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(csPath)) return null;
        var xmlPath = Path.ChangeExtension(csPath, ".xml");
        if (!File.Exists(xmlPath)) return null;
        return File.ReadAllText(xmlPath, Encoding.UTF8);
    }

    /// <summary>
    /// Validates a Manialink XML template string. Returns <see langword="true"/> when valid;
    /// otherwise sets <paramref name="error"/> to a human-readable problem description.
    /// Checks performed:
    /// <list type="bullet">
    ///   <item>Well-formed XML (parse succeeds)</item>
    ///   <item>Root element local name is <c>manialink</c> (case-insensitive)</item>
    ///   <item><c>version</c> attribute is present and equals <c>3</c></item>
    /// </list>
    /// </summary>
    internal static bool ValidateManialinkTemplate(string xmlTemplate, out string? error)
    {
        System.Xml.Linq.XDocument doc;
        try
        {
            doc = System.Xml.Linq.XDocument.Parse(xmlTemplate);
        }
        catch (System.Xml.XmlException ex)
        {
            error = $"XML is not well-formed: {ex.Message}";
            return false;
        }

        var root = doc.Root;
        if (root is null || !root.Name.LocalName.Equals("manialink", StringComparison.OrdinalIgnoreCase))
        {
            error = "Root element must be <manialink>.";
            return false;
        }

        var version = root.Attribute("version")?.Value;
        if (version is null)
        {
            error = "<manialink> must have a 'version' attribute.";
            return false;
        }
        if (version != "3")
        {
            error = $"<manialink version=\"{version}\"> is not supported; expected version=\"3\".";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Checks that every <c>[ManialinkControl]</c> binding that does not have
    /// <c>IgnoreValidation = true</c> has a matching <c>id</c> attribute in the XML template.
    /// Reports <c>MSS006</c> for each missing id.
    /// </summary>
    private static void ValidateManialinkBindings(
        string xmlTemplate,
        IReadOnlyList<Emission.ManialinkBinding> bindings,
        SourceProductionContext spc,
        ContextClassInfo info)
    {
        foreach (var b in FindMissingManialinkBindings(xmlTemplate, bindings))
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ManialinkControlNotFound,
                b.SymbolLocation ?? info.Declaration.Identifier.GetLocation(),
                b.XmlId));
        }
    }

    /// <summary>
    /// Returns bindings whose <c>XmlId</c> has no matching element in <paramref name="xmlTemplate"/>
    /// (and don't set <c>IgnoreValidation</c>).
    /// </summary>
    internal static IReadOnlyList<Emission.ManialinkBinding> FindMissingManialinkBindings(
        string xmlTemplate,
        IReadOnlyList<Emission.ManialinkBinding> bindings)
    {
        if (bindings.Count == 0) return [];
        var doc = XDocument.Parse(xmlTemplate);
        var existingIds = new HashSet<string>(
            doc.Descendants()
               .Select(e => e.Attribute("id")?.Value)
               .Where(v => v is not null)
               .Cast<string>(),
            StringComparer.Ordinal);

        var missing = new List<Emission.ManialinkBinding>();
        foreach (var b in bindings)
        {
            if (b.IgnoreValidation) continue;
            if (!existingIds.Contains(b.XmlId))
                missing.Add(b);
        }
        return missing;
    }

    /// <summary>
    /// Injects the generated <paramref name="script"/> into the Manialink
    /// <paramref name="xmlTemplate"/> as a <c>&lt;script&gt;&lt;![CDATA[...]]&gt;&lt;/script&gt;</c>
    /// block just before the closing <c>&lt;/manialink&gt;</c> tag, and sets the
    /// <c>name</c> attribute on the root element to <paramref name="className"/>.
    /// </summary>
    internal static string MergeIntoManialink(string xmlTemplate, string script, string className)
    {
        var doc = XDocument.Parse(xmlTemplate, LoadOptions.PreserveWhitespace);

        var root = doc.Root!;

        // Set / replace name attribute.
        root.SetAttributeValue("name", className);

        // Remove any existing <script> child elements.
        var existingScripts = root.Elements()
            .Where(e => e.Name.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var s in existingScripts) s.Remove();

        // Append the new <script> element with a CDATA section.
        // XElement does not have a direct CDATA API, so we add it as an XCData node.
        root.Add(new XElement("script", new XCData("\n" + script)));

        // Serialise back to string with UTF-8, no BOM.
        using var ms = new System.IO.MemoryStream();
        var xmlSettings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = System.Xml.NewLineHandling.None,
        };
        using (var writer = System.Xml.XmlWriter.Create(ms, xmlSettings))
            doc.Save(writer);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(ms.ToArray());
    }

    private static void WriteScriptFile(string path, string contents, SourceProductionContext spc)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                if (existing == contents) return;
            }
            File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.FileWriteFailed, Location.None, path, ex.Message));
        }
    }
}

internal sealed class ContextClassInfo
{
    public ClassDeclarationSyntax Declaration { get; }
    public INamedTypeSymbol Symbol { get; }
    public SemanticModel Model { get; }

    /// <summary>The <c>T</c> type from <c>ILib&lt;T&gt;</c> when the class implements it; otherwise <see langword="null"/>.</summary>
    public ITypeSymbol? LibContextType { get; }

    /// <summary>Whether this class is a ManiaScript lib (implements <c>ILib&lt;T&gt;</c>).</summary>
    public bool IsLib => LibContextType is not null;

    /// <summary>Whether the output is a Manialink XML file (affects lib inlining vs. #Include).</summary>
    public bool IsManialink { get; }

    public ContextClassInfo(ClassDeclarationSyntax decl, INamedTypeSymbol symbol, SemanticModel model,
        bool isManialink = false)
    {
        Declaration = decl;
        Symbol = symbol;
        Model = model;
        IsManialink = isManialink;
        LibContextType = ResolveLibContextType(symbol);
    }

    private static ITypeSymbol? ResolveLibContextType(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.IsGenericType && iface.Name == "ILib"
                && iface.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp"
                && iface.TypeArguments.Length == 1)
                return iface.TypeArguments[0];
        }
        return null;
    }
}
