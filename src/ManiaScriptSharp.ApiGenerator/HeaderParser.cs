using System.Text;

namespace ManiaScriptSharp.ApiGenerator;

/// <summary>
/// Parses Nadeo's C++-flavoured ManiaScript API headers (<c>doc_ManiaPlanet.h</c>,
/// <c>doc_Trackmania.h</c>). The two files use slightly different conventions:
///
/// <list type="bullet">
///   <item>ManiaPlanet: <c>struct C... : public C...</c>; fields like <c>const Type Name;</c>;
///         arrays as <c>Type[]</c>; no access labels.</item>
///   <item>Trackmania: <c>class C... : public C...</c> with explicit <c>public :</c>;
///         fields like <c>Type * const Name;</c> or <c>Type const Name;</c>;
///         arrays as <c>Array&lt;Type* const&gt;</c>;
///         dictionaries as <c>AssociativeArray&lt;K, V&gt;</c>.</item>
/// </list>
///
/// One parser handles both — modifiers (<c>const</c>, <c>*</c>, <c>public :</c>) are
/// simply normalised away.
/// </summary>
internal sealed class HeaderParser
{
    private readonly string _src;
    private int _p;
    private string? _pendingDoc;
    private HashSet<string>? _pendingModes;

    public HeaderParser(string source)
    {
        _src = source;
        _p = 0;
    }

    public ParsedHeader Parse()
    {
        var result = new ParsedHeader();
        while (_p < _src.Length)
        {
            SkipTrivia();
            if (_p >= _src.Length) break;

            if (Match("template"))
            {
                // template <...> struct/class { ... };  — skip wholesale
                SkipTemplate();
                _pendingDoc = null; _pendingModes = null;
                continue;
            }
            if (Match("struct") || Match("class"))
            {
                var decl = ParseTypeDecl();
                if (decl is not null) result.Types.Add(decl);
                _pendingDoc = null; _pendingModes = null;
                continue;
            }
            if (Match("namespace"))
            {
                var decl = ParseNamespaceDecl();
                if (decl is not null) result.Types.Add(decl);
                _pendingDoc = null; _pendingModes = null;
                continue;
            }
            if (Match("enum"))
            {
                var en = ParseEnum();
                if (en is not null) result.TopLevelEnums.Add(en);
                _pendingDoc = null; _pendingModes = null;
                continue;
            }
            // Unknown top-level token â€” skip a char and try again.
            _p++;
        }
        return result;
    }

    // ────────────────────────────── trivia ──────────────────────────────

    private void SkipTrivia()
    {
        while (_p < _src.Length)
        {
            char c = _src[_p];
            if (char.IsWhiteSpace(c)) { _p++; continue; }
            if (c == '/' && _p + 1 < _src.Length)
            {
                if (_src[_p + 1] == '/')
                {
                    while (_p < _src.Length && _src[_p] != '\n') _p++;
                    continue;
                }
                if (_src[_p + 1] == '*')
                {
                    bool isDoc = _p + 2 < _src.Length && _src[_p + 2] == '!';
                    _p += 2;
                    var start = _p;
                    while (_p + 1 < _src.Length && !(_src[_p] == '*' && _src[_p + 1] == '/')) _p++;
                    if (isDoc)
                    {
                        var rawDoc = _src.Substring(start + 1, _p - start - 1).Trim();
                        _pendingDoc = CleanDoc(rawDoc);
                        _pendingModes = ParseDeclaredModes(rawDoc);
                    }
                    _p += 2; // consume */
                    continue;
                }
            }
            break;
        }
    }

    private static string CleanDoc(string raw)
    {
        // Strip leading '* ' from each line and \brief tags. Best-effort cleanup.
        var sb = new StringBuilder();
        foreach (var lineRaw in raw.Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.TrimStart();
            if (line.StartsWith("*")) line = line.Substring(1).TrimStart();
            if (line.StartsWith("\\brief")) line = line.Substring(6).TrimStart();
            if (line.Length == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts mode names listed after "Supported declare modes :" in a raw doc comment.
    /// Handles lines like <c>* - Local</c> and <c>* - Persistent</c>.
    /// </summary>
    private static HashSet<string> ParseDeclaredModes(string rawDoc)
    {
        var modes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inModes = false;
        foreach (var lineRaw in rawDoc.Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.TrimStart();
            if (line.StartsWith("*")) line = line.Substring(1).TrimStart();
            if (line.StartsWith("Supported declare modes", System.StringComparison.OrdinalIgnoreCase))
            {
                // Inline format (ManiaPlanet): "Supported declare modes : Local  Persistent"
                var colon = line.IndexOf(':');
                if (colon >= 0)
                {
                    var rest = line.Substring(colon + 1).Trim();
                    if (rest.Length > 0)
                    {
                        foreach (var token in rest.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
                            modes.Add(token);
                        // Inline format — no need to enter bullet-list mode.
                        inModes = false;
                        continue;
                    }
                }
                // No inline modes — expect bullet lines on subsequent lines (Trackmania).
                inModes = true;
                continue;
            }
            if (inModes)
            {
                if (line.StartsWith("- "))
                {
                    var mode = line.Substring(2).Trim();
                    var spaceIdx = mode.IndexOf(' ');
                    if (spaceIdx >= 0) mode = mode.Substring(0, spaceIdx);
                    if (mode.Length > 0) modes.Add(mode);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    inModes = false; // end of mode list
                }
            }
        }
        return modes;
    }

    private bool Match(string keyword)
    {
        if (_p + keyword.Length > _src.Length) return false;
        for (int i = 0; i < keyword.Length; i++)
            if (_src[_p + i] != keyword[i]) return false;
        int end = _p + keyword.Length;
        if (end < _src.Length && (char.IsLetterOrDigit(_src[end]) || _src[end] == '_')) return false;
        _p = end;
        return true;
    }

    private bool Peek(char c) => _p < _src.Length && _src[_p] == c;

    private void Expect(char c)
    {
        if (_p < _src.Length && _src[_p] == c) { _p++; return; }
        // tolerate: just don't advance
    }

    private string ReadIdent()
    {
        SkipTrivia();
        var start = _p;
        while (_p < _src.Length && (char.IsLetterOrDigit(_src[_p]) || _src[_p] == '_' || _src[_p] == ':'))
            _p++;
        return _src.Substring(start, _p - start);
    }

    private void SkipTemplate()
    {
        // we already consumed "template" — now skip <...> then the declaration body.
        SkipTrivia();
        if (Peek('<')) SkipBracketed('<', '>');
        SkipTrivia();
        // Consume the following struct/class declaration (or anything until matching };)
        // The simplest robust thing: skip until the next top-level };
        int depth = 0;
        while (_p < _src.Length)
        {
            char c = _src[_p];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            else if (c == ';' && depth <= 0) { _p++; return; }
            _p++;
        }
    }

    private void SkipBracketed(char open, char close)
    {
        if (!Peek(open)) return;
        int depth = 0;
        while (_p < _src.Length)
        {
            char c = _src[_p++];
            if (c == open) depth++;
            else if (c == close) { depth--; if (depth == 0) return; }
        }
    }

    // ────────────────────────────── namespace decl ──────────────────────────────

    private TypeDecl? ParseNamespaceDecl()
    {
        var doc = _pendingDoc;
        var modes = _pendingModes;
        SkipTrivia();
        var name = ReadIdent();
        if (string.IsNullOrEmpty(name)) return null;

        SkipTrivia();
        if (!Peek('{'))
        {
            SkipToSemicolon();
            return null;
        }
        _p++; // consume {

        var decl = new TypeDecl(name, null, doc, isNamespace: true, declaredModes: modes);
        ParseClassBody(decl);

        SkipTrivia();
        if (Peek(';')) _p++;
        return decl;
    }

    // ────────────────────────────── type decl ──────────────────────────────

    private TypeDecl? ParseTypeDecl()
    {
        var doc = _pendingDoc;
        var modes = _pendingModes;
        SkipTrivia();
        var name = ReadIdent();
        if (string.IsNullOrEmpty(name)) return null;

        string? baseName = null;
        SkipTrivia();
        if (Peek(':'))
        {
            _p++;
            SkipTrivia();
            // optional "public"
            Match("public");
            SkipTrivia();
            baseName = ReadIdent();
            // Drop trailing '*' or 'const' if any
            baseName = baseName.Replace("*", "").Trim();
            SkipTrivia();
        }

        if (!Peek('{'))
        {
            // forward decl / something exotic — skip to ;
            SkipToSemicolon();
            return null;
        }
        _p++; // consume {

        var decl = new TypeDecl(name, baseName, doc, declaredModes: modes);
        ParseClassBody(decl);

        SkipTrivia();
        if (Peek(';')) _p++;
        return decl;
    }

    private void SkipToSemicolon()
    {
        while (_p < _src.Length && _src[_p] != ';') _p++;
        if (_p < _src.Length) _p++;
    }

    private void ParseClassBody(TypeDecl decl)
    {
        while (_p < _src.Length)
        {
            SkipTrivia();
            if (_p >= _src.Length) return;
            if (Peek('}')) { _p++; return; }

            // Access labels: "public :", "private :", "protected :"
            if (Match("public") || Match("private") || Match("protected"))
            {
                SkipTrivia();
                if (Peek(':')) _p++;
                _pendingDoc = null; _pendingModes = null;
                continue;
            }

            if (Match("enum"))
            {
                var en = ParseEnum();
                if (en is not null) decl.NestedEnums.Add(en);
                _pendingDoc = null; _pendingModes = null;
                continue;
            }

            // Nested struct/class (rare but possible) — skip whole declaration to ;
            if (Match("struct") || Match("class"))
            {
                var nested = ParseTypeDecl();
                if (nested is not null) decl.NestedTypes.Add(nested);
                _pendingDoc = null; _pendingModes = null;
                continue;
            }

            // Otherwise: a member. Read until ; while tracking parens/angle/braces.
            var memberDoc = _pendingDoc;
            _pendingDoc = null; _pendingModes = null;
            var memberText = ReadMemberStatement();
            if (string.IsNullOrWhiteSpace(memberText)) continue;
            var member = ParseMember(memberText, memberDoc);
            if (member is not null) decl.Members.Add(member);
        }
    }

    private string ReadMemberStatement()
    {
        var sb = new StringBuilder();
        int paren = 0, angle = 0, brace = 0;
        while (_p < _src.Length)
        {
            char c = _src[_p];
            // intercept doc/block comments inside a member (rare but defensive)
            if (c == '/' && _p + 1 < _src.Length && (_src[_p + 1] == '/' || _src[_p + 1] == '*'))
            {
                SkipTrivia();
                sb.Append(' ');
                continue;
            }
            if (c == '(') paren++;
            else if (c == ')') paren--;
            else if (c == '<') angle++;
            else if (c == '>') { if (angle > 0) angle--; }
            else if (c == '{') brace++;
            else if (c == '}')
            {
                if (brace == 0) return sb.ToString();
                brace--;
            }
            else if (c == ';' && paren <= 0 && brace <= 0)
            {
                _p++;
                return sb.ToString();
            }
            sb.Append(c);
            _p++;
        }
        return sb.ToString();
    }

    private static MemberDecl? ParseMember(string text, string? doc)
    {
        // Normalise whitespace and strip C++ modifiers we don't care about.
        text = text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0) return null;

        // Detect method by presence of '(' at top level.
        int paren = text.IndexOf('(');
        if (paren >= 0)
            return ParseMethod(text, paren, doc);
        return ParseField(text, doc);
    }

    private static MemberDecl? ParseField(string text, string? doc)
    {
        // e.g. "const  Integer Now"  or  "Integer  const Now"  or  "Array<CFoo* const > Bar"
        //   or "CMlPage * const  Page"  or  "const Real Pi = 3.14159"
        // Strip "= value" suffix before splitting so name is extracted correctly.
        int depth0 = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '<' || ch == '[') depth0++;
            else if (ch == '>' || ch == ']') depth0--;
            else if (ch == '=' && depth0 == 0) { text = text.Substring(0, i).TrimEnd(); break; }
        }
        var (type, name) = SplitTypeAndName(text);
        if (string.IsNullOrEmpty(name)) return null;
        var (mapped, isArray, isDict, key) = MapType(type);
        return new MemberDecl
        {
            Kind = MemberKind.Field,
            Name = name,
            Doc = doc,
            ReturnType = mapped,
            IsArray = isArray,
            IsDictionary = isDict,
            DictKey = key,
            IsConst = ContainsWord(type, "const"),
        };
    }

    private static MemberDecl? ParseMethod(string text, int parenIdx, string? doc)
    {
        var header = text.Substring(0, parenIdx).Trim();
        var rest = text.Substring(parenIdx + 1);
        int close = FindMatching(rest, 0, '(', ')');
        var argsText = close >= 0 ? rest.Substring(0, close) : rest;

        var (retType, name) = SplitTypeAndName(header);
        if (string.IsNullOrEmpty(name)) return null;
        var (mappedRet, retArr, retDict, retKey) = MapType(retType);

        var paramList = new List<ParamDecl>();
        foreach (var raw in SplitArgs(argsText))
        {
            var arg = raw.Trim();
            if (arg.Length == 0) continue;
            var (t, n) = SplitTypeAndName(arg);
            if (string.IsNullOrEmpty(n)) continue;
            var (mt, arr, dict, key) = MapType(t);
            paramList.Add(new ParamDecl(mt, n, arr, dict, key));
        }

        return new MemberDecl
        {
            Kind = MemberKind.Method,
            Name = name,
            Doc = doc,
            ReturnType = mappedRet,
            IsArray = retArr,
            IsDictionary = retDict,
            DictKey = retKey,
            Parameters = paramList,
        };
    }

    private static IEnumerable<string> SplitArgs(string args)
    {
        int depth = 0;
        int start = 0;
        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '<' || c == '(' || c == '[') depth++;
            else if (c == '>' || c == ')' || c == ']') depth--;
            else if (c == ',' && depth <= 0)
            {
                yield return args.Substring(start, i - start);
                start = i + 1;
            }
        }
        if (start < args.Length) yield return args.Substring(start);
    }

    private static int FindMatching(string s, int from, char open, char close)
    {
        int depth = 1;
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == open) depth++;
            else if (s[i] == close) { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    /// <summary>
    /// Splits a declarator like <c>"const  CMlPage * const  Page"</c> or
    /// <c>"Array&lt;CFoo* const &gt; PendingEvents"</c> into (type, name).
    /// </summary>
    private static (string Type, string Name) SplitTypeAndName(string text)
    {
        text = text.Trim();
        // Find the last whitespace at top angle-bracket depth → boundary between type and name.
        int depth = 0;
        int split = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (depth == 0 && (c == ' ' || c == '\t'))
            {
                // remember the last whitespace; the name is the trailing identifier (with possible '[]')
                split = i;
            }
        }
        if (split < 0) return (text, "");

        var name = text.Substring(split + 1).Trim();
        var type = text.Substring(0, split).Trim();

        // Trim trailing '[]' that some headers put on the name side ("Type Name[]").
        if (name.EndsWith("[]"))
        {
            type = "Array<" + type + ">";
            name = name.Substring(0, name.Length - 2);
        }
        // Strip trailing default value, if present (rare).
        int eq = name.IndexOf('=');
        if (eq >= 0) name = name.Substring(0, eq).Trim();
        return (type, name);
    }

    /// <summary>
    /// Strips C++ noise (<c>const</c>, <c>*</c>) and unwraps <c>Array&lt;...&gt;</c> /
    /// <c>AssociativeArray&lt;...&gt;</c> wrappers, returning the inner element type and flags
    /// describing the collection shape.
    /// </summary>
    private static (string Type, bool IsArray, bool IsDict, string? DictKey) MapType(string raw)
    {
        var t = raw.Replace("*", " ").Trim();
        t = StripWord(t, "const");
        t = CollapseSpaces(t);

        // ManiaPlanet style: "Type[]"
        if (t.EndsWith("[]"))
            return (CleanInner(t.Substring(0, t.Length - 2).Trim()), true, false, null);

        // ManiaPlanet dict/array notation: "ValueType[KeyType]" or "ValueType[Void]"
        int bracketOpen = t.IndexOf('[');
        if (bracketOpen > 0 && t.EndsWith("]"))
        {
            var valueType = CleanInner(t.Substring(0, bracketOpen).Trim());
            var keyType = t.Substring(bracketOpen + 1, t.Length - bracketOpen - 2).Trim();
            if (keyType == "Void" || keyType == "Integer")
                return (valueType, true, false, null);   // integer-indexed = array
            return (valueType, false, true, CleanInner(keyType));  // dictionary
        }

        // Trackmania: Array<Inner>
        if (t.StartsWith("Array<") && t.EndsWith(">"))
        {
            var inner = t.Substring(6, t.Length - 7).Trim();
            return (CleanInner(StripWord(inner.Replace("*", " "), "const").Trim()), true, false, null);
        }
        // Trackmania: AssociativeArray<K, V>
        if (t.StartsWith("AssociativeArray<") && t.EndsWith(">"))
        {
            var inside = t.Substring(17, t.Length - 18);
            // split top-level comma
            var parts = new List<string>(SplitArgs(inside));
            if (parts.Count == 2)
            {
                var k = CleanInner(StripWord(parts[0].Replace("*", " "), "const").Trim());
                var v = CleanInner(StripWord(parts[1].Replace("*", " "), "const").Trim());
                return (v, false, true, k);
            }
        }

        return (CleanInner(t), false, false, null);
    }

    private static string CleanInner(string t)
    {
        t = t.Replace("*", " ");
        t = StripWord(t, "const");
        t = CollapseSpaces(t).Trim();
        if (t.Length == 0) return "Void";
        // If anything still has whitespace, the header was malformed (e.g. `Real Faces Ratio`
        // as a single parameter). Keep just the first token after stripping modifiers.
        int sp = t.IndexOf(' ');
        if (sp >= 0) t = t.Substring(0, sp);
        return t;
    }

    private static string StripWord(string s, string word)
    {
        // remove standalone occurrences of `word`
        int idx;
        while ((idx = s.IndexOf(word)) >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetterOrDigit(s[idx - 1]);
            int after = idx + word.Length;
            bool rightOk = after >= s.Length || !char.IsLetterOrDigit(s[after]);
            if (!leftOk || !rightOk) break;
            s = s.Remove(idx, word.Length);
        }
        return s;
    }

    /// <summary>Returns true if <paramref name="word"/> occurs as a standalone token in <paramref name="s"/>.</summary>
    private static bool ContainsWord(string s, string word)
    {
        int idx = 0;
        while ((idx = s.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetterOrDigit(s[idx - 1]);
            int after = idx + word.Length;
            bool rightOk = after >= s.Length || !char.IsLetterOrDigit(s[after]);
            if (leftOk && rightOk) return true;
            idx = after;
        }
        return false;
    }

    private static string CollapseSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool sp = false;
        foreach (var c in s)
        {
            if (c == ' ' || c == '\t')
            {
                if (!sp) sb.Append(' ');
                sp = true;
            }
            else { sb.Append(c); sp = false; }
        }
        return sb.ToString().Trim();
    }

    // ────────────────────────────── enums ──────────────────────────────

    private EnumDecl? ParseEnum()
    {
        var doc = _pendingDoc;
        SkipTrivia();
        var name = ReadIdent();
        if (string.IsNullOrEmpty(name)) return null;
        SkipTrivia();
        if (!Peek('{')) { SkipToSemicolon(); return null; }
        _p++;
        var values = new List<string>();
        var sb = new StringBuilder();
        while (_p < _src.Length && !Peek('}'))
        {
            SkipTrivia();
            if (Peek('}')) break;
            sb.Clear();
            while (_p < _src.Length && _src[_p] != ',' && _src[_p] != '}')
            {
                sb.Append(_src[_p++]);
            }
            var v = sb.ToString().Trim();
            if (v.Length > 0)
            {
                // strip "= value" if present
                int eq = v.IndexOf('=');
                if (eq >= 0) v = v.Substring(0, eq).Trim();
                values.Add(v);
            }
            if (_p < _src.Length && _src[_p] == ',') _p++;
        }
        if (Peek('}')) _p++;
        SkipTrivia();
        if (Peek(';')) _p++;
        return new EnumDecl(name, values, doc);
    }
}

internal sealed class ParsedHeader
{
    public List<TypeDecl> Types { get; } = new();
    public List<EnumDecl> TopLevelEnums { get; } = new();
}

internal sealed class TypeDecl
{
    public string Name { get; }
    public string? Base { get; }
    public string? Doc { get; }
    public bool IsNamespace { get; }
    public HashSet<string> DeclaredModes { get; }
    public List<MemberDecl> Members { get; } = new();
    public List<EnumDecl> NestedEnums { get; } = new();
    public List<TypeDecl> NestedTypes { get; } = new();

    public TypeDecl(string name, string? baseName, string? doc, bool isNamespace = false,
        HashSet<string>? declaredModes = null)
    {
        Name = name; Base = baseName; Doc = doc; IsNamespace = isNamespace;
        DeclaredModes = declaredModes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}

internal enum MemberKind { Field, Method }

internal sealed class MemberDecl
{
    public MemberKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string? Doc { get; set; }
    public string ReturnType { get; set; } = "Void";
    public bool IsArray { get; set; }
    public bool IsDictionary { get; set; }
    public string? DictKey { get; set; }
    public List<ParamDecl> Parameters { get; set; } = new();
    /// <summary>Whether the field was declared <c>const</c> (read-only from script) in the header.</summary>
    public bool IsConst { get; set; }
}

internal sealed class ParamDecl
{
    public string Type { get; }
    public string Name { get; }
    public bool IsArray { get; }
    public bool IsDictionary { get; }
    public string? DictKey { get; }

    public ParamDecl(string type, string name, bool isArray = false, bool isDict = false, string? dictKey = null)
    {
        Type = type; Name = name; IsArray = isArray; IsDictionary = isDict; DictKey = dictKey;
    }
}

internal sealed class EnumDecl
{
    public string Name { get; }
    public List<string> Values { get; }
    public string? Doc { get; }
    public EnumDecl(string name, List<string> values, string? doc)
    {
        Name = name; Values = values; Doc = doc;
    }
}

