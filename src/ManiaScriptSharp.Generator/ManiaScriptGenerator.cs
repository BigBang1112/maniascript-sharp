using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using ManiaScriptSharp.Generator.Emission;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Incremental source generator that translates <c>IContext</c> scripts and <c>ILib</c>
/// libraries into ManiaScript (.Script.txt) files on disk in real time.
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
                opts.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                var settings = BuildSettings.FromOptions(opts.GlobalOptions);
                return (Dir: dir ?? "", RootNamespace: string.IsNullOrWhiteSpace(rootNamespace) ? name ?? "" : rootNamespace!, Settings: settings);
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

        // ── XML manialink templates, tracked as proper incremental inputs ────────────
        // Reading these via raw File.IO (keyed off the .cs file's path) would bypass Roslyn's
        // incremental caching entirely, so editing only the .xml would never re-trigger the
        // generator. Sourcing them from AdditionalTextsProvider makes XML edits a real input.
        var xmlTemplates = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => (Path: t.Path, Text: t.GetText(ct)?.ToString()))
            .Collect();

        // A .razor file carries a ManiaApp context, dynamic XML markup, and optionally a nested
        // Manialink script context. Razor parses the mixed document; both contexts then use the
        // same emitter as ordinary .cs files.
        var razorTemplates = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => (Path: t.Path, Text: t.GetText(ct)?.ToString()))
            .Where(static t => t.Text is not null);
        // ─────────────────────────────────────────────────────────────────────────────

        // ── ILib pipeline: generate a .Script.txt for each lib class ─────────────────
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
                var emitter = new ScriptEmitter(info, spc, proj.Settings, proj.RootNamespace);
                var script = emitter.Emit();
                var outputPath = ResolveOutputPath(
                    info.Symbol.Name, GetNamespacePath(info.Symbol, proj.RootNamespace), proj.Settings.OutputDir, proj.Dir, ".Script.txt");
                WriteScriptFiles(info, proj.Settings, proj.Dir, proj.RootNamespace, script, spc);

                spc.AddSource(
                    GetSourceHintName(info.Symbol, ".lib.g.cs"),
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

        context.RegisterSourceOutput(combined.Combine(xmlTemplates), static (spc, tuple) =>
        {
            var ((info, proj), xmlFiles) = tuple;
            try
            {
                var xmlTemplate = TryFindXmlTemplate(info, xmlFiles);
                var effectiveInfo = xmlTemplate is not null
                    ? new ContextClassInfo(info.Declaration, info.Symbol, info.Model, isManialink: true)
                    : info;

                var emitter = new ScriptEmitter(effectiveInfo, spc, proj.Settings, proj.RootNamespace);
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
                    var merged = MergeIntoManialink(xmlTemplate, script, info.Symbol.Name);
                    WriteScriptFiles(info, proj.Settings, proj.Dir, proj.RootNamespace, merged, spc, ".xml");
                }
                else
                {
                    WriteScriptFiles(info, proj.Settings, proj.Dir, proj.RootNamespace, script, spc);
                }

                spc.AddSource(
                    GetSourceHintName(info.Symbol, ".g.cs"),
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

        context.RegisterSourceOutput(
            razorTemplates.Combine(context.CompilationProvider).Combine(settingsProvider),
            static (spc, tuple) =>
            {
                var ((razorFile, compilation), proj) = tuple;
                try
                {
                    var document = RazorManialinkProcessor.Process(razorFile.Path, razorFile.Text!);
                    var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
                    var generatedPath = razorFile.Path + ".g.cs";
                    var tree = CSharpSyntaxTree.ParseText(
                        document.CSharpSource,
                        parseOptions,
                        generatedPath,
                        Encoding.UTF8);
                    var augmentedCompilation = compilation.AddSyntaxTrees(tree);
                    var model = augmentedCompilation.GetSemanticModel(tree);
                    var outerDeclaration = tree.GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .First(c => c.Identifier.ValueText == document.ClassName);

                    if (!ValidateManialinkTemplate(document.XmlTemplate, out var xmlError))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.InvalidXmlTemplate,
                            outerDeclaration.Identifier.GetLocation(),
                            document.ClassName,
                            xmlError));
                        return;
                    }

                    var innerContexts = outerDeclaration.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Select(decl => (Declaration: decl, Symbol: model.GetDeclaredSymbol(decl) as INamedTypeSymbol))
                        .Where(static item => item.Symbol is not null && ImplementsIContext(item.Symbol))
                        .ToArray();
                    if (innerContexts.Length > 1)
                        throw new InvalidOperationException(
                            "A Razor Manialink can contain at most one nested IContext class for its <script> element.");

                    var renderedXml = document.XmlTemplate;
                    if (innerContexts.Length == 1)
                    {
                        var inner = innerContexts[0];
                        var innerInfo = new ContextClassInfo(inner.Declaration, inner.Symbol!, model, isManialink: true);
                        var innerEmitter = new ScriptEmitter(innerInfo, spc, proj.Settings, proj.RootNamespace);
                        var innerScript = innerEmitter.Emit();
                        ValidateManialinkBindings(
                            document.XmlTemplate,
                            innerEmitter.ManialinkBindings,
                            spc,
                            innerInfo);
                        renderedXml = MergeIntoManialink(document.XmlTemplate, innerScript, inner.Symbol!.Name);
                    }

                    var finalSource = RazorManialinkProcessor.AddRenderMethod(document, renderedXml);
                    var finalTree = CSharpSyntaxTree.ParseText(
                        finalSource,
                        parseOptions,
                        generatedPath,
                        Encoding.UTF8);
                    var finalCompilation = compilation.AddSyntaxTrees(finalTree);
                    var finalModel = finalCompilation.GetSemanticModel(finalTree);
                    var generatedErrors = finalModel.GetDiagnostics()
                        .Where(static d => d.Severity == DiagnosticSeverity.Error)
                        .Take(5)
                        .Select(static d => d.GetMessage())
                        .ToArray();
                    if (generatedErrors.Length > 0)
                        throw new InvalidOperationException(
                            "The C# in the Razor page did not compile: " + string.Join("; ", generatedErrors));

                    var finalDeclaration = finalTree.GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .First(c => c.Identifier.ValueText == document.ClassName);
                    var finalSymbol = (INamedTypeSymbol?)finalModel.GetDeclaredSymbol(finalDeclaration);
                    if (finalSymbol is null || !ImplementsIContext(finalSymbol))
                        throw new InvalidOperationException(
                            $"Razor page '{document.ClassName}' could not be compiled as an IContext.");

                    spc.AddSource(
                        GetSourceHintName(finalSymbol, ".razor.g.cs"),
                        SourceText.From(finalSource, Encoding.UTF8));

                    var outerInfo = new ContextClassInfo(finalDeclaration, finalSymbol, finalModel);
                    var outerEmitter = new ScriptEmitter(outerInfo, spc, proj.Settings, proj.RootNamespace);
                    var outerScript = outerEmitter.Emit();
                    WriteScriptFiles(outerInfo, proj.Settings, proj.Dir, proj.RootNamespace, outerScript, spc);

                    spc.AddSource(
                        GetSourceHintName(finalSymbol, ".razor.output.g.cs"),
                        SourceText.From(
                            $"// Generated Razor ManiaApp at: {document.ClassName}.Script.txt\n// Length: {outerScript.Length} chars\n",
                            Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.InvalidRazorTemplate,
                        Location.None,
                        Path.GetFileName(razorFile.Path),
                        ex.Message));
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
            if (i.Name == "ILib"
                && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp")
                return true;
        return false;
    }

    internal static IReadOnlyList<string> ResolveOutputPaths(
        string scriptName,
        string namespaceName,
        string rootNamespace,
        BuildSettings settings,
        string projectDir,
        string extension = ".Script.txt",
        Action<string, Exception>? onAdditionalError = null)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namespacePath = GetNamespacePath(namespaceName, rootNamespace);
        var primaryPath = ResolveOutputPath(scriptName, namespacePath, settings.OutputDir, projectDir, extension);
        paths.Add(primaryPath);
        seen.Add(primaryPath);

        foreach (var root in settings.AdditionalOutputDirs)
        {
            try
            {
                var path = ResolveOutputPath(scriptName, namespacePath, root, projectDir, extension);
                if (seen.Add(path)) paths.Add(path);
            }
            catch (Exception ex)
            {
                onAdditionalError?.Invoke(root, ex);
            }
        }
        return paths;
    }

    private static string ResolveOutputPath(
        string scriptName,
        string namespacePath,
        string root,
        string projectDir,
        string extension)
    {
        var resolvedRoot = Path.IsPathRooted(root) ? root : Path.Combine(projectDir, root);
        return Path.GetFullPath(Path.Combine(resolvedRoot, namespacePath, scriptName + extension));
    }

    private static string GetNamespacePath(INamedTypeSymbol symbol, string rootNamespace) =>
        GetNamespacePath(symbol.ContainingNamespace?.ToDisplayString() ?? "", rootNamespace);

    private static string GetSourceHintName(INamedTypeSymbol symbol, string suffix)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return (string.IsNullOrEmpty(namespaceName) ? "" : namespaceName + ".") + symbol.Name + suffix;
    }

    internal static string GetNamespacePath(string namespaceName, string rootNamespace)
    {
        const string scriptsPrefix = "ManiaScriptSharp.Scripts.";
        const string basePrefix = "ManiaScriptSharp.";
        if (namespaceName == "ManiaScriptSharp")
            return "";
        if (namespaceName.StartsWith(scriptsPrefix, StringComparison.Ordinal))
            namespaceName = namespaceName.Substring(scriptsPrefix.Length);
        else if (!string.IsNullOrEmpty(rootNamespace) && namespaceName == rootNamespace)
            return "";
        else if (!string.IsNullOrEmpty(rootNamespace) && namespaceName.StartsWith(rootNamespace + ".", StringComparison.Ordinal))
            namespaceName = namespaceName.Substring(rootNamespace.Length + 1);
        else if (namespaceName.StartsWith(basePrefix, StringComparison.Ordinal))
            namespaceName = namespaceName.Substring(basePrefix.Length);
        return namespaceName.Replace('.', Path.DirectorySeparatorChar);
    }

    private static void WriteScriptFiles(
        ContextClassInfo info,
        BuildSettings settings,
        string projectDir,
        string rootNamespace,
        string contents,
        SourceProductionContext spc,
        string extension = ".Script.txt")
    {
        var paths = ResolveOutputPaths(
            info.Symbol.Name,
            info.Symbol.ContainingNamespace?.ToDisplayString() ?? "",
            rootNamespace,
            settings,
            projectDir,
            extension,
            (root, ex) => spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.FileWriteFailed, Location.None, root, ex.Message)));
        foreach (var path in paths)
            WriteScriptFile(path, contents, spc);
    }

    /// <summary>
    /// Looks for an XML template alongside the declaring .cs file among the AdditionalFiles
    /// tracked by the incremental pipeline. Returns the file contents if found,
    /// <see langword="null"/> otherwise.
    /// </summary>
    private static string? TryFindXmlTemplate(ContextClassInfo info, ImmutableArray<(string Path, string? Text)> xmlFiles)
    {
        var csPath = info.Declaration.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(csPath)) return null;
        var xmlPath = Path.GetFullPath(Path.ChangeExtension(csPath, ".xml"));

        foreach (var (path, text) in xmlFiles)
        {
            if (text is null || string.IsNullOrEmpty(path)) continue;
            if (string.Equals(Path.GetFullPath(path), xmlPath, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
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

    /// <summary>Whether this class is a ManiaScript lib (implements <c>ILib</c> or <c>ILib&lt;T&gt;</c>).</summary>
    public bool IsLib { get; }

    /// <summary>Whether this class is a ManiaScript context script (implements <c>IContext</c>).</summary>
    public bool IsContext { get; }

    /// <summary>Whether the output is a Manialink XML file (affects lib inlining vs. #Include).</summary>
    public bool IsManialink { get; }

    public ContextClassInfo(ClassDeclarationSyntax decl, INamedTypeSymbol symbol, SemanticModel model,
        bool isManialink = false)
    {
        Declaration = decl;
        Symbol = symbol;
        Model = model;
        IsManialink = isManialink;
        IsLib = ImplementsILib(symbol);
        IsContext = ImplementsIContext(symbol);
    }

    private static bool ImplementsILib(INamedTypeSymbol symbol)
        => symbol.AllInterfaces.Any(static iface =>
            iface.Name == "ILib"
            && iface.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");

    private static bool ImplementsIContext(INamedTypeSymbol symbol)
        => symbol.AllInterfaces.Any(static iface =>
            iface.Name == "IContext"
            && iface.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");

}
