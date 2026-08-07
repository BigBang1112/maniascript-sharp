using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.ApiGenerator;

/// <summary>
/// Incremental source generator that turns any <c>doc.h</c> file passed as
/// <c>AdditionalFiles</c> into a full .NET API surface.
///
/// <para>
/// The generator always emits into the <c>ManiaScriptSharp</c> namespace.
/// </para>
///
/// <para>
/// The generator is format-agnostic — see <see cref="HeaderParser"/> for how the
/// two distinct header dialects (ManiaPlanet vs. Trackmania) are reconciled.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ApiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var headerFiles = context.AdditionalTextsProvider
            .Where(static at =>
            {
                var name = Path.GetFileName(at.Path);
                return name.StartsWith("doc", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".h", StringComparison.OrdinalIgnoreCase);
            })
            .Select(static (at, ct) => (
                Path: at.Path,
                FileName: Path.GetFileName(at.Path),
                Text: at.GetText(ct)?.ToString() ?? ""));

        var settings = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) => ApiGeneratorSettings.From(opts.GlobalOptions));

        // Collect (TypeName, MethodName) from user-written partial method implementations.
        // These are partial methods WITH a body — the user is providing the implementation.
        var userPartials = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m &&
                    m.Modifiers.Any(t => t.IsKind(SyntaxKind.PartialKeyword)) &&
                    (m.Body != null || m.ExpressionBody != null),
                transform: static (ctx, _) =>
                {
                    var method = (MethodDeclarationSyntax)ctx.Node;
                    var typeName = (method.Parent as TypeDeclarationSyntax)?.Identifier.Text ?? "";
                    return (TypeName: typeName, MethodName: method.Identifier.Text);
                })
            .Collect();

        // Collect (TypeName, MemberName) from user-written fields/properties declared by hand
        // in another partial declaration of the type (not generated) — the matching generated
        // field/property is skipped so the user's own member (which may use a more specific
        // type, e.g. Dictionary<Ident, CUILayer> instead of CUILayer[]) wins.
        var userMembers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    (node is PropertyDeclarationSyntax || node is FieldDeclarationSyntax)
                    && !node.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase),
                transform: static (ctx, _) =>
                {
                    var typeName = (ctx.Node.Parent as TypeDeclarationSyntax)?.Identifier.Text ?? "";
                    var memberName = ctx.Node switch
                    {
                        PropertyDeclarationSyntax p => p.Identifier.Text,
                        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                        _ => "",
                    };
                    return (TypeName: typeName, MemberName: memberName);
                })
            .Where(static x => x.TypeName.Length > 0 && x.MemberName.Length > 0)
            .Collect();

        var combined = headerFiles.Combine(settings).Combine(userPartials).Combine(userMembers);

        // ── .Script.txt pipeline ──────────────────────────────────────────────
        var scriptFiles = context.AdditionalTextsProvider
            .Where(static at => at.Path.EndsWith(".Script.txt", StringComparison.OrdinalIgnoreCase))
            .Select(static (at, ct) => (
                Path: at.Path,
                Text: at.GetText(ct)?.ToString() ?? ""));

        // Extract all type names defined in doc.h files so we can skip script
        // functions that reference types not present in the generated C# API.
        var knownApiTypes = headerFiles
            .Select(static (file, _) => ExtractDocTypeNames(file.Text))
            .Collect();

        // Pre-compute (path, text, fileName, rawClassName, ns, sanitisedSegs) for every script.
        var scriptInfos = scriptFiles.Select(static (file, _) =>
        {
            var fileName = System.IO.Path.GetFileName(file.Path);
            var (rawClassName, ns, sanitisedSegs) = ComputeScriptNames(file.Path);
            return (file.Path, file.Text, fileName, rawClassName, ns, sanitisedSegs);
        });

        // Collect everything so we can detect namespace-vs-class naming conflicts
        // before emitting (e.g. class UI in one ns conflicts with a sub-namespace).
        var allScriptInfos = scriptInfos.Collect().Combine(settings).Combine(knownApiTypes);

        context.RegisterSourceOutput(allScriptInfos, static (spc, outer) =>
        {
            var ((infos, generatorSettings), knownTypeSets) = outer;

            // Union all type names from all doc.h files
            var knownTypes = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var set in knownTypeSets)
                knownTypes.UnionWith(set);

            // Build the full set of namespaces that will be emitted so we can detect
            // cases where a class name collides with a sub-namespace at the same level.
            var allNamespaces = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var info in infos)
                allNamespaces.Add(info.ns);

            // Pre-compute all final FQNs (with conflict renames applied) so include
            // fields can be validated before emission (avoids CS0234 / CS0118).
            // Only include scripts that will actually produce output (have public functions).
            var knownScriptFqns = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var info in infos)
            {
                if (string.IsNullOrEmpty(info.rawClassName)) continue;
                try
                {
                    var p = new ScriptParser(info.Text).Parse();
                    if (p.Functions.Count == 0 && p.Structs.Count == 0) continue;
                }
                catch { continue; }
                var cn = info.rawClassName;
                if (allNamespaces.Contains(info.ns + "." + cn)) cn = cn + "Lib";
                knownScriptFqns.Add(info.ns + "." + cn);
            }

            var emitted = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            foreach (var (_, text, fileName, rawClassName, ns, sanitisedSegs) in infos)
            {
                if (string.IsNullOrEmpty(rawClassName)) continue;

                // Rename the class if its name would clash with a sub-namespace.
                // E.g. "UI" in ManiaScriptSharp.TrackMania conflicts with the
                // namespace ManiaScriptSharp.TrackMania.UI → rename to "UILib".
                var className = rawClassName;
                if (allNamespaces.Contains(ns + "." + className))
                    className = className + "Lib";

                var hintPrefix = sanitisedSegs.Length > 0
                    ? string.Join(".", sanitisedSegs) + "."
                    : "";
                var hintName = $"Script.{hintPrefix}{className}.g.cs";

                if (!emitted.Add(hintName)) continue; // last-resort dedup

                try
                {
                    var parsed = new ScriptParser(text).Parse();
                    if (parsed.Functions.Count == 0 && parsed.Structs.Count == 0) continue;

                    var emitter = new ScriptApiEmitter(ns, fileName, className, parsed, generatorSettings, knownTypes, knownScriptFqns);
                    var source = emitter.Emit();
                    spc.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "MSSA002",
                            "Script API generation failed",
                            "Failed to process {0}: {1}",
                            "ManiaScriptSharp",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        Location.None,
                        fileName, ex.Message));
                }
            }
        });

        // ── doc.h pipeline ────────────────────────────────────────────────────
        context.RegisterSourceOutput(combined, static (spc, quad) =>
        {
            var (((file, generatorSettings), userImplemented), userMemberList) = quad;
            const string ns = "ManiaScriptSharp";

            var userSet = new System.Collections.Generic.HashSet<(string, string)>();
            foreach (var (typeName, methodName) in userImplemented)
                userSet.Add((typeName, methodName));

            var userMemberSet = new System.Collections.Generic.HashSet<(string, string)>();
            foreach (var (typeName, memberName) in userMemberList)
                userMemberSet.Add((typeName, memberName));

            try
            {
                var parsed = new HeaderParser(file.Text).Parse();
                var emitter = new CSharpEmitter(ns, file.FileName, parsed, generatorSettings, userSet, userMemberSet);
                var prefix = SanitisePrefix(Path.GetFileNameWithoutExtension(file.FileName));
                var added = new System.Collections.Generic.HashSet<string>();
                foreach (var (name, src) in emitter.Emit(parsed))
                {
                    var hint = $"{prefix}.{name}";
                    if (!added.Add(hint)) continue; // last-resort dedup
                    spc.AddSource(hint, SourceText.From(src, Encoding.UTF8));
                }
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "MSSA001",
                        "API generation failed",
                        "Failed to process {0}: {1}",
                        "ManiaScriptSharp",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    file.FileName, ex.Message));
            }
        });
    }

    private static string SanitisePrefix(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }

    /// <summary>
    /// Scans the text of a doc.h file and returns the set of all C/E-prefixed
    /// type names defined in it (e.g. CSmMode, EWeapon). These are the types
    /// that will be present in the generated ManiaScriptSharp C# API, and any
    /// script function referencing an unknown type outside this set is skipped.
    /// </summary>
    private static System.Collections.Generic.HashSet<string> ExtractDocTypeNames(string docHText)
    {
        var result = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        // struct CXxx : ... or struct EXxx (enums are 'struct' in the header too)
        var i = 0;
        while (i < docHText.Length)
        {
            // Look for "struct " or "enum "
            if (i + 7 < docHText.Length &&
                docHText[i] == 's' && docHText.Substring(i, 7) == "struct ")
            {
                i += 7;
            }
            else if (i + 5 < docHText.Length &&
                docHText[i] == 'e' && docHText.Substring(i, 5) == "enum ")
            {
                i += 5;
            }
            else { i++; continue; }

            // Skip whitespace
            while (i < docHText.Length && docHText[i] == ' ') i++;

            // Read identifier
            if (i < docHText.Length &&
                (docHText[i] == 'C' || docHText[i] == 'E') &&
                i + 1 < docHText.Length && char.IsUpper(docHText[i + 1]))
            {
                var start = i;
                while (i < docHText.Length && (char.IsLetterOrDigit(docHText[i]) || docHText[i] == '_'))
                    i++;
                result.Add(docHText.Substring(start, i - start));
            }
        }
        return result;
    }

    /// <summary>
    /// Given an AdditionalFile path for a .Script.txt file, derive the C# class name,
    /// namespace, and sanitised path segments used for hint-name generation.
    /// </summary>
    private static (string ClassName, string Namespace, string[] SanitisedSegs) ComputeScriptNames(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);

        // Class name = filename without ".Script.txt"
        var className = fileName;
        if (className.EndsWith(".Script.txt", StringComparison.OrdinalIgnoreCase))
            className = className.Substring(0, className.Length - ".Script.txt".Length);
        className = SanitisePrefix(className);

        // Normalise path separators and make path relative to "Scripts/"
        var relPath = filePath.Replace('\\', '/');
        var scriptsIdx = relPath.IndexOf("/Scripts/", StringComparison.OrdinalIgnoreCase);
        if (scriptsIdx >= 0)
            relPath = relPath.Substring(scriptsIdx + "/Scripts/".Length);
        else
            relPath = fileName;

        // Use the full directory path as namespace segments (preserve Libs, Nadeo, etc.)
        var slashIdx = relPath.LastIndexOf('/');
        var dirPart = slashIdx >= 0 ? relPath.Substring(0, slashIdx) : "";
        var dirSegments = dirPart.Length > 0
            ? dirPart.Split('/')
            : System.Array.Empty<string>();

        var sanitisedSegs = dirSegments
            .Select(SanitisePrefix)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        var ns = sanitisedSegs.Length > 0
            ? "ManiaScriptSharp.Scripts." + string.Join(".", sanitisedSegs)
            : "ManiaScriptSharp.Scripts";

        return (className, ns, sanitisedSegs);
    }
}
