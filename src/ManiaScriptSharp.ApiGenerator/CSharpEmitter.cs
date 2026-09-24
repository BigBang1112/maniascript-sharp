using System.Text;

namespace ManiaScriptSharp.ApiGenerator;

/// <summary>
/// Turns a <see cref="ParsedHeader"/> into one C# source string per declared type
/// (plus a primitives file). Every emitted type is <c>partial</c> so hand-written
/// helpers in the same namespace can extend it.
/// </summary>
internal sealed class CSharpEmitter
{
    private sealed class EmittedMemberSet
    {
        public HashSet<string> FieldNames { get; } = new();
        public HashSet<string> MethodSignatures { get; } = new();
        public HashSet<string> TypeNames { get; } = new();
    }

    private readonly string _namespace;
    private readonly string _sourceFileName;
    private readonly ApiGeneratorSettings _settings;
    private readonly HashSet<string> _definedTypes;
    private readonly HashSet<(string TypeName, string MethodName)> _userImplemented;
    private readonly Dictionary<string, TypeDecl> _typesByName;
    private readonly Dictionary<string, EmittedMemberSet> _ownMembersCache = new();
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _nestedEnumNamesCache = new();
    /// <summary>(TypeName, MemberName) pairs the user already declared by hand in another partial
    /// declaration — the generated field/property with that name is skipped so the user's own
    /// member (which may have a different, more specific type) wins.</summary>
    private readonly HashSet<(string TypeName, string MemberName)> _userDefinedMembers;
    private readonly HashSet<string> _knownPrimitives = new(new[]
    {
        "Void", "Integer", "Real", "Boolean", "Text",
        "Vec2", "Vec3", "Int2", "Int3", "Ident",
    });
    // C# reserved words we need to escape if they appear as field/parameter names.
    private static readonly HashSet<string> Reserved = new(new[]
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else",
        "enum","event","explicit","extern","false","finally","fixed","float","for","foreach",
        "goto","if","implicit","in","int","interface","internal","is","lock","long",
        "namespace","new","null","object","operator","out","override","params","private",
        "protected","public","readonly","ref","return","sbyte","sealed","short","sizeof",
        "stackalloc","static","string","struct","switch","this","throw","true","try",
        "typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void",
        "volatile","while",
    });

    public CSharpEmitter(string ns, string sourceFileName, ParsedHeader header,
        ApiGeneratorSettings? settings = null,
        HashSet<(string TypeName, string MethodName)>? userImplemented = null,
        HashSet<(string TypeName, string MemberName)>? userDefinedMembers = null)
    {
        _namespace = ns;
        _sourceFileName = sourceFileName;
        _settings = settings ?? ApiGeneratorSettings.Default;
        _userImplemented = userImplemented ?? new HashSet<(string, string)>();
        _userDefinedMembers = userDefinedMembers ?? new HashSet<(string, string)>();
        _typesByName = new Dictionary<string, TypeDecl>();
        foreach (var t in header.Types)
        {
            if (_knownPrimitives.Contains(t.Name)) continue;
            if (!_typesByName.TryGetValue(t.Name, out var existing)
                || Weight(t) > Weight(existing))
                _typesByName[t.Name] = t;
        }
        _definedTypes = new HashSet<string>(header.Types.Select(t => t.Name));
        foreach (var t in header.Types)
        {
            foreach (var nt in t.NestedTypes)
                _definedTypes.Add(nt.Name);
        }
        foreach (var e in header.TopLevelEnums) _definedTypes.Add(e.Name);
        foreach (var p in _knownPrimitives) _definedTypes.Add(p);
    }

    public IEnumerable<(string FileName, string Source)> Emit(ParsedHeader header)
    {
        yield return ("__Primitives.g.cs", EmitPrimitives());

        // Collect every type name referenced anywhere — we'll emit `CNod`-style stubs for the gaps.
        var referenced = new HashSet<string>();
        void Reference(string? raw, HashSet<string>? ownEnumNames = null)
        {
            if (string.IsNullOrEmpty(raw)) return;
            var name = raw!;
            if (name.Contains("::"))
            {
                var parts = name.Split(new[] { "::" }, System.StringSplitOptions.None);
                if (_definedTypes.Contains(parts[0])) return; // qualified into a defined type
                name = parts[parts.Length - 1];
            }
            // Don't generate a stub for a nested enum that is defined on the same class:
            // within C# the nested enum is accessible unqualified inside the class body.
            if (ownEnumNames is not null && ownEnumNames.Contains(name)) return;
            referenced.Add(name);
        }
        foreach (var t in header.Types)
        {
            Reference(t.Base);
            var ownEnumNames = new HashSet<string>(t.NestedEnums.Select(e => e.Name));
            foreach (var m in t.Members)
            {
                Reference(m.ReturnType, ownEnumNames);
                Reference(m.DictKey, ownEnumNames);
                foreach (var p in m.Parameters)
                {
                    Reference(p.Type, ownEnumNames);
                    Reference(p.DictKey, ownEnumNames);
                }
            }
        }

        var emittedHints = new HashSet<string>();
        foreach (var t in _typesByName.Values)
        {
            if (!emittedHints.Add(t.Name)) continue;
            yield return ($"{t.Name}.g.cs", EmitType(t));
        }

        foreach (var e in header.TopLevelEnums)
        {
            if (!emittedHints.Add(e.Name)) continue;
            yield return ($"{e.Name}.g.cs", EmitTopLevelEnum(e));
        }

        // Emit stubs for unknown referenced types so the generated code compiles.
        foreach (var name in referenced)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (_knownPrimitives.Contains(name)) continue;
            if (_definedTypes.Contains(name)) continue;
            if (name.Contains("::")) continue; // qualified name — handled where mapped
            if (!IsValidIdentifier(name)) continue;
            if (!emittedHints.Add(name)) continue;
            yield return ($"{name}.stub.g.cs", EmitStub(name));
        }
    }

    private static int Weight(TypeDecl t) =>
        t.Members.Count * 4 + t.NestedEnums.Count * 2 + t.NestedTypes.Count * 2 + (t.Base is null ? 0 : 1);

    private string EmitPrimitives()
    {
        var sb = NewFile();
        EmitVectorPrimitive(sb, "Vec2", "float", "2-component vector (Real X, Real Y).", "X", "Y");
        EmitVectorPrimitive(sb, "Vec3", "float", "3-component vector (Real X, Real Y, Real Z).", "X", "Y", "Z");
        EmitVectorPrimitive(sb, "Int2", "int", "2-component integer vector.", "X", "Y");
        EmitVectorPrimitive(sb, "Int3", "int", "3-component integer vector.", "X", "Y", "Z");
        sb.AppendLine("/// <summary>ManiaScript Ident — opaque unique object identifier.</summary>");
        sb.AppendLine("public readonly partial struct Ident : System.IEquatable<Ident> { public static readonly Ident? NullId = null; " +
            "public override string ToString() => \"NullId\"; public bool Equals(Ident other) => true; " +
            "public override bool Equals(object? obj) => obj is Ident; public override int GetHashCode() => 0; " +
            "public static bool operator ==(Ident left, Ident right) => left.Equals(right); public static bool operator !=(Ident left, Ident right) => !left.Equals(right); }");
        sb.AppendLine();
        EndFile(sb);
        return sb.ToString();
    }

    private static void EmitVectorPrimitive(StringBuilder sb, string name, string componentType,
        string summary, params string[] components)
    {
        sb.Append("/// <summary>").Append(summary).AppendLine("</summary>");
        sb.Append("public partial struct ").Append(name).Append(" : System.IEquatable<").Append(name).AppendLine(">");
        sb.AppendLine("{");
        foreach (var component in components)
            sb.Append("    public ").Append(componentType).Append(' ').Append(component).AppendLine(";");

        sb.Append("    public ").Append(name).Append('(')
            .Append(string.Join(", ", components.Select(c => $"{componentType} {c.ToLowerInvariant()}")))
            .AppendLine(")");
        sb.AppendLine("    {");
        foreach (var component in components)
            sb.Append("        ").Append(component).Append(" = ").Append(component.ToLowerInvariant()).AppendLine(";");
        sb.AppendLine("    }");

        sb.Append("    public bool Equals(").Append(name).Append(" other) => ")
            .Append(string.Join(" && ", components.Select(c => $"{c}.Equals(other.{c})"))).AppendLine(";");
        sb.Append("    public override bool Equals(object obj) => obj is ").Append(name)
            .AppendLine(" other && Equals(other);");
        sb.AppendLine("    public override int GetHashCode()");
        sb.AppendLine("    {");
        sb.AppendLine("        unchecked");
        sb.AppendLine("        {");
        sb.AppendLine("            var hash = 17;");
        foreach (var component in components)
            sb.Append("            hash = hash * 31 + ").Append(component).AppendLine(".GetHashCode();");
        sb.AppendLine("            return hash;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.Append("    public static bool operator ==(").Append(name).Append(" left, ").Append(name)
            .AppendLine(" right) => left.Equals(right);");
        sb.Append("    public static bool operator !=(").Append(name).Append(" left, ").Append(name)
            .AppendLine(" right) => !left.Equals(right);");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private string EmitStub(string name)
    {
        var sb = NewFile();
        sb.AppendLine($"/// <summary>Stub for <c>{name}</c> referenced by the API surface but not defined in <c>{_sourceFileName}</c>.</summary>");
        sb.AppendLine($"public partial class {name} {{ }}");
        EndFile(sb);
        return sb.ToString();
    }

    private string EmitTopLevelEnum(EnumDecl e)
    {
        var sb = NewFile();
        WriteDoc(sb, e.Doc, 0);
        sb.AppendLine($"public enum {Identifier(e.Name)}");
        sb.AppendLine("{");
        foreach (var v in SanitiseEnumValues(e.Values))
            sb.AppendLine($"    {v},");
        sb.AppendLine("}");
        EndFile(sb);
        return sb.ToString();
    }

    /// <summary>
    /// The headers contain a few stray tokens that aren't valid C# identifiers — e.g.
    /// <c>(reserved)</c> or <c>XXX Null</c>. We rewrite them into safe identifiers and
    /// dedupe so each enum stays valid.
    /// </summary>
    private static IEnumerable<string> SanitiseEnumValues(IEnumerable<string> values)
    {
        var seen = new HashSet<string>();
        foreach (var raw in values)
        {
            var v = SanitiseIdentifier(raw);
            if (string.IsNullOrEmpty(v)) continue;
            var candidate = Reserved.Contains(v) ? "@" + v : v;
            // Disambiguate name collisions by appending a numeric suffix.
            var unique = candidate;
            int n = 2;
            while (!seen.Add(unique)) unique = candidate + "_" + (n++);
            yield return unique;
        }
    }

    private static string SanitiseIdentifier(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else if (c == ' ' || c == '\t') sb.Append('_');
            // skip everything else ('(', ')', etc.)
        }
        if (sb.Length == 0) return "";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private string EmitType(TypeDecl t)
    {
        var sb = NewFile();
        WriteDoc(sb, t.Doc, 0);
        if (t.IsNamespace)
        {
            var isStatic = _settings.NamespaceLibsStatic;
            sb.Append(isStatic ? "public static partial class " : "public sealed partial class ")
              .Append(Identifier(t.Name));
            if (!isStatic)
                sb.Append(" : ILib");
        }
        else
        {
            sb.Append("public partial class ").Append(Identifier(t.Name));
            bool hasColon = false;
            // Guard against the header's occasional self-referential base declarations
            // (e.g. `struct X : public X` typo).
            if (!string.IsNullOrEmpty(t.Base)
                && !string.Equals(t.Base, "CNod", System.StringComparison.Ordinal)
                && !string.Equals(t.Base, t.Name, System.StringComparison.Ordinal))
            { sb.Append(" : ").Append(Identifier(t.Base!)); hasColon = true; }
            else if (!string.IsNullOrEmpty(t.Base) && !string.Equals(t.Base, t.Name, System.StringComparison.Ordinal))
            { sb.Append(" : ").Append(Identifier(t.Base!)); hasColon = true; }

            // Append declare-mode interfaces (Local → ILocalProvider, etc.)
            foreach (var mode in t.DeclaredModes.OrderBy(m => m, StringComparer.Ordinal))
            {
                var iface = DeclaredModeInterface(mode);
                if (iface is null) continue;
                sb.Append(hasColon ? ", " : " : ").Append(iface);
                hasColon = true;
            }
        }
        sb.AppendLine();
        sb.AppendLine("{");

        var inheritedMembers = GetInheritedMembers(t);

        var nestedEnumNames = GetNestedEnumCSharpNames(t);
        var emittedEnums = new HashSet<string>();
        foreach (var e in t.NestedEnums)
        {
            if (!emittedEnums.Add(e.Name)) continue; // headers sometimes redeclare enums
            WriteDoc(sb, e.Doc, 4);
            var enumName = nestedEnumNames[e.Name];
            var enumBareName = BareIdentifier(enumName);
            var useNew = inheritedMembers.FieldNames.Contains(enumBareName)
                || inheritedMembers.TypeNames.Contains(enumBareName);
            if (enumName != Identifier(e.Name))
                sb.Append("    [global::ManiaScriptSharp.ManiaScriptName(\"")
                  .Append(e.Name.Replace("\\", "\\\\").Replace("\"", "\\\""))
                  .AppendLine("\")]");
            sb.Append("    public ");
            if (useNew) sb.Append("new ");
            sb.Append("enum ").Append(enumName).AppendLine();
            sb.AppendLine("    {");
            foreach (var v in SanitiseEnumValues(e.Values))
                sb.Append("        ").Append(v).AppendLine(",");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        var reservedMemberNames = new HashSet<string>(nestedEnumNames.Values.Select(BareIdentifier));
        reservedMemberNames.Add(t.Name);

        // Disambiguate members: methods can overload; fields cannot duplicate; if a field is
        // declared twice we keep the first to keep the generator deterministic.
        var seenFields = new HashSet<string>();
        var methodSignatures = new HashSet<string>();

        foreach (var m in t.Members)
        {
            if (m.Kind == MemberKind.Field)
            {
                if (!seenFields.Add(m.Name)) continue;
                if (_userDefinedMembers.Contains((t.Name, m.Name))) continue; // user overwrote this member by hand
                var name = DisambiguateMember(m.Name, reservedMemberNames);
                var useNew = inheritedMembers.FieldNames.Contains(BareIdentifier(name));
                EmitField(sb, t, m, name, useNew, isStatic: t.IsNamespace && _settings.NamespaceLibsStatic);
            }
            else
            {
                var sig = MethodSignature(m, t);
                if (!methodSignatures.Add(sig)) continue;
                var name = DisambiguateMember(m.Name, reservedMemberNames);
                var useNew = inheritedMembers.MethodSignatures.Contains(MethodSignature(name, m, t));
                var isPartialDecl = _userImplemented.Contains((t.Name, m.Name));
                EmitMethod(sb, t, m, name, useNew,
                    isStatic: t.IsNamespace && _settings.NamespaceLibsStatic,
                    isPartialDecl: isPartialDecl);
            }
        }

        // Explicit interface implementations for declared declare modes.
        foreach (var mode in t.DeclaredModes.OrderBy(m => m, StringComparer.Ordinal))
        {
            var iface = DeclaredModeInterface(mode);
            var prop = DeclaredModePropName(mode);
            if (iface is null || prop is null) continue;
            sb.AppendLine($"    System.Collections.Generic.Dictionary<string, System.Runtime.CompilerServices.IStrongBox> {iface}.{prop} {{ get; }} = [];");
        }

        // INetreadProvider and INetwriteProvider both extend INetworkProvider — emit the shared implementation once.
        if (t.DeclaredModes.Contains("NetworkRead") || t.DeclaredModes.Contains("NetworkWrite"))
        {
            sb.AppendLine("    System.Collections.Generic.Dictionary<string, System.Runtime.CompilerServices.IStrongBox> INetworkProvider.NetworkData { get; } = [];");
        }

        sb.AppendLine("}");
        EndFile(sb);
        return sb.ToString();
    }

    /// <summary>Maps a declare-mode name to its provider interface name, or null if unknown.</summary>
    private static string? DeclaredModeInterface(string mode) => mode switch
    {
        "Local" => "ILocalProvider",
        "Persistent" => "IPersistentProvider",
        "Metadata" => "IMetadataProvider",
        "NetworkRead" => "INetreadProvider",
        "NetworkWrite" => "INetwriteProvider",
        _ => null,
    };

    /// <summary>Returns the property name exposed by the provider interface for the given mode.</summary>
    private static string? DeclaredModePropName(string mode) => mode switch
    {
        "Local" => "Local",
        "Persistent" => "Persistent",
        "Metadata" => "Metadata",
        "NetworkRead" or "NetworkWrite" => null, // shared INetworkProvider.NetworkData emitted separately
        _ => null,
    };

    private void EmitField(StringBuilder sb, TypeDecl owner, MemberDecl m, string name, bool useNew, bool isStatic = false)
    {
        WriteDoc(sb, m.Doc, 4);
        var type = ResolveType(m, owner);
        sb.Append("    public ");
        if (isStatic) sb.Append("static ");
        if (useNew) sb.Append("new ");
        sb.Append(type).Append(' ').Append(name).AppendLine(m.IsConst ? " { get; }" : " { get; set; }");
    }

    private void EmitMethod(StringBuilder sb, TypeDecl owner, MemberDecl m, string name, bool useNew,
        bool isStatic = false, bool isPartialDecl = false)
    {
        WriteDoc(sb, m.Doc, 4);
        var ret = ResolveType(m, owner);
        sb.Append("    public ");
        if (isStatic) sb.Append("static ");
        if (isPartialDecl) sb.Append("partial ");
        if (useNew) sb.Append("new ");
        sb.Append(ret).Append(' ').Append(name).Append('(');
        var seenParams = new HashSet<string>();
        bool first = true;
        foreach (var p in m.Parameters)
        {
            if (!first) sb.Append(", ");
            first = false;
            var pn = _settings.StandardizeParamNames ? ParamIdentifier(p.Name) : Identifier(p.Name);
            var unique = pn;
            int n = 2;
            while (!seenParams.Add(unique)) unique = pn + n++;
            sb.Append(ResolveType(p.Type, p.IsArray, p.IsDictionary, p.DictKey, owner))
              .Append(' ')
              .Append(unique);
        }
        sb.Append(')');
        if (isPartialDecl) sb.AppendLine(";");
        else if (ret == "void") sb.AppendLine(" { }");
        else sb.AppendLine(" => default!;");
    }

    /// <summary>
    /// If a member's name collides with the enclosing class's own name or with a nested
    /// enum/type declared in the same class, append underscores until it doesn't.
    /// </summary>
    private static string DisambiguateMember(string raw, HashSet<string> reservedMemberNames)
    {
        var name = Identifier(raw);
        var bare = BareIdentifier(name);
        while (reservedMemberNames.Contains(bare))
        {
            bare += "_";
            name = Reserved.Contains(bare) ? "@" + bare : bare;
        }
        return name;
    }

    private static string BareIdentifier(string identifier) =>
        identifier.StartsWith("@") ? identifier.Substring(1) : identifier;

    /// <summary>Keep official field names intact when a nested enum has the same name.</summary>
    private IReadOnlyDictionary<string, string> GetNestedEnumCSharpNames(TypeDecl type)
    {
        if (_nestedEnumNamesCache.TryGetValue(type.Name, out var cached)) return cached;

        var fieldNames = new HashSet<string>(type.Members
            .Where(m => m.Kind == MemberKind.Field)
            .Select(m => BareIdentifier(Identifier(m.Name))));
        var occupied = new HashSet<string>(type.Members
            .Select(m => BareIdentifier(Identifier(m.Name))));
        occupied.UnionWith(type.NestedEnums.Select(e => BareIdentifier(Identifier(e.Name))));
        occupied.UnionWith(type.NestedTypes.Select(t => BareIdentifier(Identifier(t.Name))));
        occupied.Add(BareIdentifier(Identifier(type.Name)));

        var names = new Dictionary<string, string>();
        foreach (var e in type.NestedEnums)
        {
            if (names.ContainsKey(e.Name)) continue;
            var name = Identifier(e.Name);
            if (fieldNames.Contains(BareIdentifier(name)))
            {
                name = "E" + BareIdentifier(name);
                while (occupied.Contains(name)) name = "E" + name;
                occupied.Add(name);
            }
            names.Add(e.Name, name);
        }

        _nestedEnumNamesCache[type.Name] = names;
        return names;
    }

    private EmittedMemberSet GetInheritedMembers(TypeDecl type)
    {
        var inherited = new EmittedMemberSet();
        var seenTypes = new HashSet<string>();
        var baseName = ResolveBaseTypeName(type.Base);

        while (baseName is not null && seenTypes.Add(baseName))
        {
            if (!_typesByName.TryGetValue(baseName, out var baseType)) break;
            var own = GetOwnEmittedMembers(baseType);
            inherited.FieldNames.UnionWith(own.FieldNames);
            inherited.MethodSignatures.UnionWith(own.MethodSignatures);
            inherited.TypeNames.UnionWith(own.TypeNames);
            baseName = ResolveBaseTypeName(baseType.Base);
        }

        return inherited;
    }

    private EmittedMemberSet GetOwnEmittedMembers(TypeDecl type)
    {
        if (_ownMembersCache.TryGetValue(type.Name, out var cached))
            return cached;

        var members = new EmittedMemberSet();
        var nestedEnumNames = GetNestedEnumCSharpNames(type);
        foreach (var name in nestedEnumNames.Values)
            members.TypeNames.Add(BareIdentifier(name));
        foreach (var nt in type.NestedTypes)
            members.TypeNames.Add(BareIdentifier(Identifier(nt.Name)));
        var reservedMemberNames = new HashSet<string>(nestedEnumNames.Values.Select(BareIdentifier)) { type.Name };
        var seenFields = new HashSet<string>();
        var methodSignatures = new HashSet<string>();

        foreach (var m in type.Members)
        {
            if (m.Kind == MemberKind.Field)
            {
                if (!seenFields.Add(m.Name)) continue;
                var fieldName = DisambiguateMember(m.Name, reservedMemberNames);
                members.FieldNames.Add(BareIdentifier(fieldName));
            }
            else
            {
                var sig = MethodSignature(m, type);
                if (!methodSignatures.Add(sig)) continue;
                var methodName = DisambiguateMember(m.Name, reservedMemberNames);
                members.MethodSignatures.Add(MethodSignature(methodName, m, type));
            }
        }

        _ownMembersCache[type.Name] = members;
        return members;
    }

    private static string? ResolveBaseTypeName(string? rawBaseName)
    {
        if (string.IsNullOrWhiteSpace(rawBaseName)) return null;

        var name = rawBaseName!.Trim();
        if (name.Contains("::"))
        {
            var parts = name.Split(new[] { "::" }, System.StringSplitOptions.None);
            name = parts[parts.Length - 1];
        }
        if (name.Contains('.'))
        {
            var parts = name.Split('.');
            name = parts[parts.Length - 1];
        }

        name = SanitiseIdentifier(name);
        return string.IsNullOrEmpty(name) ? null : name;
    }

    // ────────────────────────────── type resolution ──────────────────────────────

    private string ResolveType(MemberDecl m, TypeDecl owner) =>
        ResolveType(m.ReturnType, m.IsArray, m.IsDictionary, m.DictKey, owner);

    private string ResolveType(string raw, bool isArray, bool isDict, string? dictKey, TypeDecl owner)
    {
        var mapped = MapName(raw, owner);
        if (isDict)
            return $"System.Collections.Generic.Dictionary<{MapName(dictKey ?? "Integer", owner)}, {mapped}>";
        if (isArray)
            return $"System.Collections.Generic.IList<{mapped}>";
        return mapped;
    }

    private string MapName(string raw, TypeDecl owner)
    {
        if (string.IsNullOrEmpty(raw)) return "object";
        var t = raw.Trim();
        if (t.Contains("::"))
        {
            // "CSmMode::EWeapon" → "CSmMode.EWeapon" only if CSmMode is one of our types,
            // otherwise (e.g. "NWebServicesPrestige::EPrestigeMode") drop the outer namespace
            // and treat the inner name as a free-standing type — a stub gets emitted for it.
            var parts = t.Split(new[] { "::" }, System.StringSplitOptions.None);
            if (parts.Length >= 2 && _definedTypes.Contains(parts[0]))
            {
                if (parts.Length == 2 && _typesByName.TryGetValue(parts[0], out var referencedType)
                    && GetNestedEnumCSharpNames(referencedType).TryGetValue(parts[1], out var enumName))
                    parts[1] = enumName;
                t = string.Join(".", parts);
            }
            else
                t = parts[parts.Length - 1];
        }
        else if (GetNestedEnumCSharpNames(owner).TryGetValue(t, out var enumName))
            t = enumName;
        return t switch
        {
            "Void" => "void",
            "Integer" => "int",
            "Real" => "float",
            "Boolean" => "bool",
            "Text" => "string",
            _ => Identifier(t),
        };
    }

    // ────────────────────────────── small helpers ──────────────────────────────

    /// <summary>
    /// Converts a header parameter name to a C# camelCase identifier.
    /// Strips any number of leading underscores, then lowercases the first letter.
    /// </summary>
    private static string ParamIdentifier(string raw)
    {
        // Strip leading underscores
        var s = raw.TrimStart('_');
        if (s.Length == 0) s = raw; // all underscores — keep as-is
        // Lowercase first letter
        if (char.IsUpper(s[0])) s = char.ToLowerInvariant(s[0]) + s.Substring(1);
        var sanitised = SanitiseIdentifier(s);
        if (string.IsNullOrEmpty(sanitised)) sanitised = "param";
        if (Reserved.Contains(sanitised)) return "@" + sanitised;
        return sanitised;
    }

    private static string Identifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return "_";
        // Allow dotted names through as-is (used for qualified types).
        if (s.Contains('.'))
        {
            var parts = s.Split('.');
            for (int i = 0; i < parts.Length; i++) parts[i] = Identifier(parts[i]);
            return string.Join(".", parts);
        }
        var sanitised = SanitiseIdentifier(s);
        if (string.IsNullOrEmpty(sanitised)) sanitised = "_";
        if (Reserved.Contains(sanitised)) return "@" + sanitised;
        return sanitised;
    }

    private static bool IsValidIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (!char.IsLetter(s[0]) && s[0] != '_') return false;
        for (int i = 1; i < s.Length; i++)
            if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
        return true;
    }

    private string MethodSignature(MemberDecl m, TypeDecl owner) =>
        MethodSignature(Identifier(m.Name), m, owner);

    private string MethodSignature(string methodName, MemberDecl m, TypeDecl owner)
    {
        var sb = new StringBuilder(BareIdentifier(methodName));
        sb.Append('(');
        foreach (var p in m.Parameters)
        {
            sb.Append(ResolveType(p.Type, p.IsArray, p.IsDictionary, p.DictKey, owner)).Append(',');
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static void WriteDoc(StringBuilder sb, string? doc, int indent)
    {
        if (string.IsNullOrWhiteSpace(doc)) return;
        var pad = new string(' ', indent);
        sb.Append(pad).Append("/// <summary>").Append(EscapeXml(doc!)).AppendLine("</summary>");
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private StringBuilder NewFile()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Source: {_sourceFileName}");
        sb.AppendLine("//   Tool:   ManiaScriptSharp.ApiGenerator");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine();
        sb.Append("namespace ").Append(_namespace).AppendLine(";");
        sb.AppendLine();
        return sb;
    }

    private static void EndFile(StringBuilder sb) { /* trailing newline already in place */ }
}
