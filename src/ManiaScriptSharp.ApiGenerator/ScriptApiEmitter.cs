using System.Linq;
using System.Text;

namespace ManiaScriptSharp.ApiGenerator;

/// <summary>
/// Generates a C# partial class stub from a <see cref="ParsedScript"/>,
/// following the same conventions as <see cref="CSharpEmitter"/> for
/// namespace libraries (TextLib, MathLib, …).
/// </summary>
internal sealed class ScriptApiEmitter
{
    private readonly string _namespace;
    private readonly string _sourceRelativePath;
    private readonly string _className;
    private readonly ParsedScript _script;
    private readonly ApiGeneratorSettings _settings;
    private readonly HashSet<string>? _knownTypes;
    private readonly System.Collections.Generic.HashSet<string>? _knownScriptFqns;
    private readonly System.Collections.Generic.HashSet<string> _localStructNames;

    // C# reserved words that need @-escaping
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else",
        "enum","event","explicit","extern","false","finally","fixed","float","for",
        "foreach","goto","if","implicit","in","int","interface","internal","is","lock",
        "long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed","short",
        "sizeof","stackalloc","static","string","struct","switch","this","throw","true",
        "try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual",
        "void","volatile","while",
    };

    public ScriptApiEmitter(
        string ns,
        string sourceRelativePath,
        string className,
        ParsedScript script,
        ApiGeneratorSettings settings,
        HashSet<string>? knownTypes = null,
        System.Collections.Generic.HashSet<string>? knownScriptFqns = null)
    {
        _namespace = ns;
        _sourceRelativePath = sourceRelativePath;
        _className = className;
        _script = script;
        _settings = settings;
        _knownTypes = knownTypes;
        _knownScriptFqns = knownScriptFqns;
        _localStructNames = new System.Collections.Generic.HashSet<string>(
            script.Structs.Select(s => s.Name), StringComparer.Ordinal);
    }

    // ── public entry ──────────────────────────────────────────────────────────

    /// <returns>The generated C# source text.</returns>
    public string Emit()
    {
        var sb = NewFile();

        WriteClassDoc(sb);

        var isStatic = _settings.NamespaceLibsStatic;
        var modifier = isStatic ? "public static partial class " : "public partial class ";

        // Resolve base class from #Extends path, if any and the type is known.
        string? baseType = null;
        if (_script.Extends is not null)
            baseType = ResolveExtendsType(_script.Extends);

        string iface;
        if (isStatic)
            iface = "";
        else if (baseType is not null)
            iface = $" : {baseType}, ILib";
        else
            iface = " : ILib";

        sb.Append(modifier).Append(EscapeIdentifier(_className)).AppendLine(iface);
        sb.AppendLine("{");

        // Emit #Include directives as fields (picked up by DirectivesEmitter).
        var emittedAliases = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var inc in _script.Includes)
        {
            var libType = IncludePathToTypeName(inc.Path);
            if (libType is null) continue;
            // Validate: the referenced type must be in the known-script FQNs set.
            // This avoids CS0234/CS0118 for includes referencing missing or
            // namespace-conflicting types.
            if (_knownScriptFqns != null)
            {
                var fqn = libType.StartsWith("global::", StringComparison.Ordinal)
                    ? libType.Substring("global::".Length)
                    : libType;
                if (!_knownScriptFqns.Contains(fqn)) continue;
            }
            var alias = EscapeIdentifier(inc.Alias);
            if (!emittedAliases.Add(alias)) continue; // deduplicate
            // Avoid CS0542: field name must not equal the enclosing class name.
            if (alias == _className) alias += "Lib";
            sb.Append("    public ").Append(libType).Append(' ')
              .Append(alias).AppendLine(";");
        }

        // Emit #Const declarations as public C# constants.
        // Pre-compute function names to avoid CS0102 collisions.
        var functionNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var func in _script.Functions)
            functionNames.Add(EscapeIdentifier(func.Name));

        bool firstConst = true;
        var emittedConsts = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var con in _script.Consts)
        {
            if (!TryMapConstValue(con.RawValue, out var csType, out var csValue)) continue;
            var constName = EscapeIdentifier(con.Name);
            if (!emittedConsts.Add(constName)) continue; // deduplicate same-name consts
            if (functionNames.Contains(constName)) continue; // avoid CS0102 with same-named method
            if (firstConst && _script.Includes.Count > 0) sb.AppendLine();
            firstConst = false;
            sb.Append("    public const ").Append(csType).Append(' ')
              .Append(constName).Append(" = ").Append(csValue).AppendLine(";");
        }

        bool first = _script.Includes.Count == 0 && firstConst;

        // Emit #Struct declarations as nested C# structs.
        var emittedStructNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var s in _script.Structs)
        {
            var structName = EscapeIdentifier(s.Name);
            if (!emittedStructNames.Add(structName)) continue; // deduplicate
            var checkpoint = sb.Length;
            if (!first) sb.AppendLine();
            try
            {
                EmitStruct(sb, s);
                first = false;
            }
            catch
            {
                // Roll back and remove from localStructNames so functions using it are also skipped.
                sb.Length = checkpoint;
                _localStructNames.Remove(s.Name);
            }
        }

        // Emit label blocks as virtual void methods (only for non-static classes)
        var emittedLabels = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        if (!isStatic)
        {
            foreach (var label in _script.Labels)
            {
                var labelName = EscapeIdentifier(label.Name);
                if (!emittedLabels.Add(labelName)) continue; // deduplicate
                if (functionNames.Contains(labelName)) continue; // already emitted as a function
                if (emittedAliases.Contains(labelName)) continue; // collides with an #Include field
                if (labelName == _className) continue; // CS0542: method name == class name
                if (!first) sb.AppendLine();
                first = false;
                sb.Append("    public virtual void ").Append(labelName).AppendLine("() { }");
            }
        }

        foreach (var func in _script.Functions)
        {
            var checkpoint = sb.Length;
            if (!first) sb.AppendLine();
            try
            {
                EmitMethod(sb, func, isStatic);
                first = false;
            }
            catch
            {
                // Roll back anything written for this function and skip it
                sb.Length = checkpoint;
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── file header ───────────────────────────────────────────────────────────

    private StringBuilder NewFile()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Source: {_sourceRelativePath}");
        sb.AppendLine("//   Tool:   ManiaScriptSharp.ApiGenerator");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine();
        // Sub-namespaced files need access to the root namespace types (Vec3, Ident, CXxx …)
        if (!_namespace.Equals("ManiaScriptSharp", StringComparison.Ordinal))
            sb.AppendLine("using ManiaScriptSharp;");
        sb.AppendLine();
        sb.Append("namespace ").Append(_namespace).AppendLine(";");
        sb.AppendLine();
        return sb;
    }

    // ── class XML doc comment ─────────────────────────────────────────────────

    private void WriteClassDoc(StringBuilder sb)
    {
        var doc = _script.ScriptDoc;
        if (doc?.Summary is null) return;

        var summary = EscapeXml(doc.Summary);
        if (string.IsNullOrWhiteSpace(summary)) return;

        sb.AppendLine($"/// <summary>{summary}</summary>");
    }

    // ── method ────────────────────────────────────────────────────────────────

    private void EmitMethod(StringBuilder sb, ScriptFunction func, bool isStatic)
    {
        WriteMethodDoc(sb, func);

        var ret = MapType(func.ReturnType);
        var memberModifier = isStatic ? "public static " : "public ";

        // A method name that equals the enclosing class name is illegal in C#
        var methodName = EscapeIdentifier(func.Name);
        if (methodName == _className) methodName += "_";

        sb.Append("    ").Append(memberModifier).Append(ret).Append(' ')
          .Append(methodName).Append('(');

        for (int i = 0; i < func.Parameters.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var p = func.Parameters[i];
            sb.Append(MapType(p.Type)).Append(' ').Append(ParamIdentifier(p.Name));
        }

        sb.Append(')');
        if (ret == "void") sb.AppendLine(" { }");
        else sb.AppendLine(" => default!;");
    }

    // ── struct ────────────────────────────────────────────────────────────────

    private void EmitStruct(StringBuilder sb, ScriptStruct s)
    {
        sb.Append("    public struct ").AppendLine(EscapeIdentifier(s.Name));
        sb.AppendLine("    {");
        foreach (var f in s.Fields)
        {
            string mappedType;
            try { mappedType = MapStructFieldType(f.Type); }
            catch { continue; } // skip fields with unresolvable types
            sb.Append("        public ").Append(mappedType).Append(' ')
              .Append(EscapeIdentifier(f.Name)).AppendLine(";");
        }
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Maps a ManiaScript type used in a struct field to C#.
    /// Unlike <see cref="MapSimpleType"/>, this allows local struct types (e.g.
    /// <c>K_Xxx</c>) because they are defined as sibling nested structs in the
    /// same class.
    /// </summary>
    private string MapStructFieldType(string msType)
    {
        if (string.IsNullOrEmpty(msType)) return "object";

        if (msType.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = msType.Substring(0, msType.Length - 2);
            return MapStructFieldSimpleType(elem) + "[]";
        }

        var bracket = msType.IndexOf('[');
        if (bracket > 0 && msType.EndsWith("]", StringComparison.Ordinal))
        {
            var valueType = msType.Substring(0, bracket);
            var keyType = msType.Substring(bracket + 1, msType.Length - bracket - 2);
            return $"global::System.Collections.Generic.Dictionary<{MapStructFieldSimpleType(keyType)}, {MapStructFieldSimpleType(valueType)}>";
        }

        return MapStructFieldSimpleType(msType);
    }

    private string MapStructFieldSimpleType(string t)
    {
        if (t.Contains("::"))
        {
            var parts = t.Split(["::"], StringSplitOptions.None);
            t = parts[0] + "." + parts[parts.Length - 1];
        }

        t = t switch
        {
            "Void" => "void",
            "Integer" => "int",
            "Real" => "float",
            "Boolean" => "bool",
            "Text" => "string",
            _ => t,
        };

        // Local struct types (K_Xxx, SXxx) are valid if defined in this script.
        if ((t.Length > 2 && t[0] == 'K' && t[1] == '_') ||
            (t.Length > 1 && t[0] == 'S' && char.IsUpper(t[1])))
        {
            if (_localStructNames.Contains(t)) return t;
            throw new InvalidOperationException($"Cross-script struct type '{t}'");
        }

        // Unknown C/E-prefixed API types in struct fields are unresolvable.
        if (_knownTypes != null && t.Length > 1 &&
            (t[0] == 'C' || t[0] == 'E') && char.IsUpper(t[1]) &&
            !_knownTypes.Contains(t))
            throw new InvalidOperationException($"Unknown type '{t}'");

        // Alias-qualified references (e.g. Structs.SFoo) are unresolvable.
        if (t.Contains('.') && !t.StartsWith("System.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unresolvable qualified type '{t}'");

        return t;
    }

    // ── method XML doc comment ────────────────────────────────────────────────

    private void WriteMethodDoc(StringBuilder sb, ScriptFunction func)
    {
        var doc = func.Doc;
        if (doc is null) return;

        if (doc.Summary is not null)
        {
            var summary = EscapeXml(doc.Summary);
            if (!string.IsNullOrWhiteSpace(summary))
                sb.AppendLine($"    /// <summary>{summary}</summary>");
        }

        foreach (var (name, desc) in doc.Params)
        {
            var paramCsName = ParamIdentifier(name);
            if (desc.Length > 0)
                sb.AppendLine($"    /// <param name=\"{EscapeXml(paramCsName)}\">{EscapeXml(desc)}</param>");
            else
                sb.AppendLine($"    /// <param name=\"{EscapeXml(paramCsName)}\" />");
        }

        if (doc.Returns is not null)
        {
            var ret = EscapeXml(doc.Returns);
            if (!string.IsNullOrWhiteSpace(ret))
                sb.AppendLine($"    /// <returns>{ret}</returns>");
        }
    }

    // ── type mapping ──────────────────────────────────────────────────────────

    private string MapType(string msType)
    {
        if (string.IsNullOrEmpty(msType)) return "object";

        // Array suffix: T[]
        if (msType.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = msType.Substring(0, msType.Length - 2);
            return MapSimpleType(elem) + "[]";
        }

        // Associative array: Value[Key]
        var bracket = msType.IndexOf('[');
        if (bracket > 0 && msType.EndsWith("]", StringComparison.Ordinal))
        {
            var valueType = msType.Substring(0, bracket);
            var keyType = msType.Substring(bracket + 1, msType.Length - bracket - 2);
            return $"global::System.Collections.Generic.Dictionary<{MapSimpleType(keyType)}, {MapSimpleType(valueType)}>";
        }

        return MapSimpleType(msType);
    }

    private string MapSimpleType(string t)
    {
        // Qualified ManiaScript name: A::B  (e.g. CSmMode::EWeapon, CMlControl::AlignHorizontal)
        // Always emit as OuterType.InnerType.
        if (t.Contains("::"))
        {
            var parts = t.Split(["::"], StringSplitOptions.None);
            t = parts[0] + "." + parts[parts.Length - 1];
        }

        t = t switch
        {
            "Void" => "void",
            "Integer" => "int",
            "Real" => "float",
            "Boolean" => "bool",
            "Text" => "string",
            _ => t,
        };

        // If a known-types set was provided, verify any C/E-prefixed type is in it.
        // Unknown types mean the function can't compile → throw so the caller skips it.
        if (_knownTypes != null && t.Length > 1 &&
            (t[0] == 'C' || t[0] == 'E') && char.IsUpper(t[1]) &&
            !_knownTypes.Contains(t))
        {
            throw new InvalidOperationException($"Unknown type '{t}'");
        }

        // ManiaScript struct types (K_Xxx and SXxx conventions) are local to the
        // script. If the struct is defined in this script's #Struct declarations,
        // it is now emitted as a nested C# struct and can be referenced directly.
        // Otherwise, skip any function that references an unknown struct type.
        // K_Xxx: t[0]=='K' && t[1]=='_'
        // SXxx:  t[0]=='S' && t[1] is uppercase letter
        if ((t.Length > 2 && t[0] == 'K' && t[1] == '_') ||
            (t.Length > 1 && t[0] == 'S' && char.IsUpper(t[1])))
        {
            if (_localStructNames.Contains(t)) return t;
            throw new InvalidOperationException($"ManiaScript struct type '{t}'");
        }

        // Alias-qualified type references like "Structs.SSettingModel" (where
        // "Structs" is an #Include alias, not a real C# type) are unresolvable.
        if (t.Contains('.') && !t.StartsWith("System.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unresolvable qualified type '{t}'");

        return t;
    }

    // ── include path → C# type ────────────────────────────────────────────────

    /// <summary>
    /// Resolves a <c>#Extends</c> path to a fully-qualified C# type name that
    /// is known to be emitted (i.e. present in <see cref="_knownScriptFqns"/>).
    /// Returns <c>null</c> if the type cannot be resolved or isn't available.
    /// </summary>
    private string? ResolveExtendsType(string path)
    {
        var fqn = IncludePathToFqn(path);
        if (fqn is null) return null;

        // If a known-FQN set was given, verify the base class is actually emitted.
        if (_knownScriptFqns != null && !_knownScriptFqns.Contains(fqn))
            return null;

        // Apply the same namespace-vs-class rename that the generator uses for UILib etc.
        // We can detect this by checking if the FQN namespace contains the class name
        // as a sub-namespace.  The simplest proxy: check if the FQN appears in knownScriptFqns
        // with "Lib" appended (the rename is transparent from here since it's already in fqns).
        return $"global::{fqn}";
    }

    /// <summary>
    /// Converts a ManiaScript include/extends path to a fully-qualified C# type name string
    /// (without the <c>global::</c> prefix).
    /// </summary>
    private static string? IncludePathToFqn(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string className;
        string nsPath;

        if (path.EndsWith(".Script.txt", StringComparison.OrdinalIgnoreCase))
        {
            var slash = path.LastIndexOf('/');
            var file = slash >= 0 ? path.Substring(slash + 1) : path;
            className = file.Substring(0, file.Length - ".Script.txt".Length);

            var dir = slash >= 0 ? path.Substring(0, slash) : "";
            nsPath = dir.Length > 0
                ? "ManiaScriptSharp.Scripts." + dir.Replace('/', '.')
                : "ManiaScriptSharp.Scripts";
        }
        else
        {
            className = path;
            nsPath = "ManiaScriptSharp";
        }

        var sb = new StringBuilder(className.Length);
        foreach (var c in className)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        className = sb.Length > 0 ? sb.ToString() : "_";

        return $"{nsPath}.{className}";
    }

    /// <summary>
    /// Converts a ManiaScript include path to a fully-qualified C# type name (with <c>global::</c>)
    /// that can be used as a field type in the generated stub.
    /// Returns <c>null</c> for paths that can't be resolved to a type name.
    /// </summary>
    private static string? IncludePathToTypeName(string path)
    {
        var fqn = IncludePathToFqn(path);
        return fqn is null ? null : $"global::{fqn}";
    }

    // ── const value helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Attempts to map a raw ManiaScript const value to a C# type and literal.
    /// Returns <c>false</c> for complex values that cannot be a C# const.
    /// </summary>
    private static bool TryMapConstValue(string raw, out string csType, out string csValue)
    {
        csType = "";
        csValue = "";

        if (string.IsNullOrEmpty(raw)) return false;

        // String literal: starts and ends with " but NOT a triple-quoted ManiaScript string
        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"'
            && !(raw.Length >= 3 && raw[1] == '"'))
        {
            csType = "string";
            csValue = raw; // already a valid C# string literal
            return true;
        }

        // Boolean: ManiaScript True / False
        if (raw.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            csType = "bool";
            csValue = "true";
            return true;
        }
        if (raw.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            csType = "bool";
            csValue = "false";
            return true;
        }

        // Integer: optional leading minus, all digits
        if (System.Text.RegularExpressions.Regex.IsMatch(raw, @"^-?\d+$"))
        {
            csType = "int";
            csValue = raw;
            return true;
        }

        // Real/Double: optional minus, digits with at least one dot
        if (System.Text.RegularExpressions.Regex.IsMatch(raw, @"^-?\d*\.\d+$|^-?\d+\.\d*$"))
        {
            csType = "double";
            // C# requires digits on both sides of the dot (e.g. 95. → 95.0)
            csValue = raw.EndsWith(".") ? raw + "0" : raw;
            return true;
        }

        return false;
    }

    // ── identifier helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Converts a ManiaScript parameter name to C# camelCase (strips leading
    /// underscores, lowercases the first letter) when
    /// <see cref="ApiGeneratorSettings.StandardizeParamNames"/> is enabled.
    /// </summary>
    private string ParamIdentifier(string raw)
    {
        if (!_settings.StandardizeParamNames)
            return EscapeIdentifier(raw);

        var s = raw.TrimStart('_');
        if (s.Length == 0) s = raw;
        if (char.IsUpper(s[0])) s = char.ToLowerInvariant(s[0]) + s.Substring(1);

        // Replace any remaining non-alphanumeric-or-underscore chars
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        s = sb.Length > 0 ? sb.ToString() : "param";

        return Reserved.Contains(s) ? "@" + s : s;
    }

    private static string EscapeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_";
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        var result = sb.ToString();
        if (result.Length > 0 && char.IsDigit(result[0])) result = "_" + result;
        if (Reserved.Contains(result)) return "@" + result;
        return result;
    }

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
