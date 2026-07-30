using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.ApiGenerator;

// ─── data model ──────────────────────────────────────────────────────────────

internal sealed class ScriptDocComment
{
    public string? Summary { get; set; }
    public IReadOnlyList<(string Name, string Desc)> Params { get; set; } = [];
    public string? Returns { get; set; }
}

internal sealed class ScriptParameter
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class ScriptFunction
{
    public ScriptDocComment? Doc { get; set; }
    public string ReturnType { get; set; } = "";
    public string Name { get; set; } = "";
    public IReadOnlyList<ScriptParameter> Parameters { get; set; } = [];
}

internal sealed class ScriptInclude
{
    /// <summary>The raw include path, e.g. "Libs/Nadeo/Layers2.Script.txt" or "TextLib".</summary>
    public string Path { get; set; } = "";
    /// <summary>The alias declared after <c>as</c>, e.g. "Layers2" or "TextLib".</summary>
    public string Alias { get; set; } = "";
}

internal sealed class ScriptConst
{
    public string Name { get; set; } = "";
    /// <summary>The raw ManiaScript value, e.g. <c>0</c>, <c>"hello"</c>, <c>True</c>.</summary>
    public string RawValue { get; set; } = "";
}

internal sealed class ScriptLabel
{
    /// <summary>The label name as it appears between the triple-stars, e.g. <c>InitMap</c>.</summary>
    public string Name { get; set; } = "";
}

internal sealed class ScriptStructField
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class ScriptStruct
{
    public string Name { get; set; } = "";
    public IReadOnlyList<ScriptStructField> Fields { get; set; } = [];
}

internal sealed class ParsedScript
{
    public string? RequireContext { get; set; }
    /// <summary>The raw path from <c>#Extends</c>, e.g. "Modes/ModeBase2.Script.txt".</summary>
    public string? Extends { get; set; }
    public ScriptDocComment? ScriptDoc { get; set; }
    public IReadOnlyList<ScriptInclude> Includes { get; set; } = [];
    public IReadOnlyList<ScriptConst> Consts { get; set; } = [];
    public IReadOnlyList<ScriptFunction> Functions { get; set; } = [];
    public IReadOnlyList<ScriptLabel> Labels { get; set; } = [];
    public IReadOnlyList<ScriptStruct> Structs { get; set; } = [];
}

// ─── parser ──────────────────────────────────────────────────────────────────

/// <summary>
/// Parses a ManiaScript .Script.txt file and extracts public function signatures.
/// Functions in a <c>// Private</c> section or whose names start with
/// <c>Private_</c> are excluded.
/// </summary>
internal sealed class ScriptParser
{
    private readonly string _src;
    private int _pos;

    // state
    private string? _pendingDoc;
    private bool _inPrivateSection; // false = public by default

    public ScriptParser(string source) => _src = source;

    // ── public entry point ────────────────────────────────────────────────────

    public ParsedScript Parse()
    {
        var requireContext = ExtractRequireContext();
        _pos = 0;
        _pendingDoc = null;
        _inPrivateSection = false;

        string? scriptDoc = null;
        bool scriptDocFound = false;
        string? extends = null;
        var includes = new List<ScriptInclude>();
        var functions = new List<ScriptFunction>();
        var labels = new List<ScriptLabel>();

        while (_pos < _src.Length)
        {
            SkipSpaces();
            if (_pos >= _src.Length) break;

            char c = _src[_pos];

            // ManiaScript directive (#Const, #Include, #RequireContext, …)
            if (c == '#')
            {
                _pendingDoc = null;
                var inc = TryParseInclude(out var isExtends);
                if (inc != null)
                {
                    if (isExtends) extends = inc.Path;
                    else includes.Add(inc);
                }
                else SkipLine();
                continue;
            }

            // Line comment: may contain section markers (// Private / // Public)
            // Also handles /// doc comments and plain // sentence docs.
            if (c == '/' && Peek(1) == '/')
            {
                _pos += 2;
                bool isTripleSlash = _pos < _src.Length && _src[_pos] == '/';
                if (isTripleSlash) _pos++; // skip third '/'

                var lineContent = ReadToEol();
                UpdateSectionState(lineContent);

                if (isTripleSlash)
                {
                    // /// style: always treat as doc comment; accumulate multi-line
                    var docText = lineContent.TrimStart(' ', '\t');
                    _pendingDoc = _pendingDoc is null ? docText : _pendingDoc + "\n" + docText;
                }
                else
                {
                    // // style: capture as informal doc only when the content
                    // looks like a description (not a separator or section header).
                    var stripped = Regex.Replace(lineContent, @"[-~=*/\\ ]+", " ").Trim();
                    if (stripped.Length == 0 || IsSectionKeyword(stripped))
                        _pendingDoc = null; // separator / section header — clear pending doc
                    else
                        _pendingDoc = stripped; // informal single-line doc
                }
                continue;
            }

            // Block comment: /** doc */ or /* non-doc */
            if (c == '/' && Peek(1) == '*')
            {
                var (text, isDoc) = ReadBlockComment();
                _pendingDoc = isDoc ? text : null;
                if (!scriptDocFound && isDoc && text is not null)
                {
                    scriptDoc = text;
                    scriptDocFound = true;
                }
                continue;
            }

            // Label block: ***LabelName***
            if (c == '*')
            {
                _pendingDoc = null;
                var labelName = TryReadLabel();
                if (labelName is not null)
                    labels.Add(new ScriptLabel { Name = labelName });
                else
                    SkipLine();
                continue;
            }

            // Triple-quoted string at top level (skip — not a function def)
            if (c == '"' && Peek(1) == '"' && Peek(2) == '"')
            {
                _pendingDoc = null;
                SkipTripleQuotedString();
                continue;
            }

            // Simple string literal at top level
            if (c == '"')
            {
                _pendingDoc = null;
                SkipSimpleString();
                continue;
            }

            // Everything else should be a function definition.
            // Try to parse; on failure skip the line.
            int savedPos = _pos;
            string? savedDoc = _pendingDoc;
            _pendingDoc = null;

            if (TryReadFunctionDef(savedDoc, out var func))
            {
                if (func is not null) functions.Add(func);
            }
            else
            {
                _pos = savedPos;
                SkipLine();
            }
        }

        return new ParsedScript
        {
            RequireContext = requireContext,
            Extends = extends,
            ScriptDoc = ParseDocComment(scriptDoc),
            Includes = includes,
            Consts = ExtractConsts(),
            Functions = functions,
            Labels = labels,
            Structs = ExtractStructs(),
        };
    }

    // ── label block parser ───────────────────────────────────────────────────

    /// <summary>
    /// If the current position is at a <c>***LabelName***</c> line, parses the label
    /// name, skips the body block, and returns the name; otherwise returns <c>null</c>.
    /// </summary>
    private string? TryReadLabel()
    {
        int saved = _pos;

        // Must start with ***
        if (_pos + 3 > _src.Length ||
            _src[_pos] != '*' || _src[_pos + 1] != '*' || _src[_pos + 2] != '*')
            return null;
        _pos += 3;

        // Read the label name (must be a valid identifier)
        var nameStart = _pos;
        while (_pos < _src.Length && _src[_pos] != '*' && _src[_pos] != '\n' && _src[_pos] != '\r')
            _pos++;
        var name = _src.Substring(nameStart, _pos - nameStart).Trim();

        // Must be followed by closing ***
        if (_pos + 3 > _src.Length ||
            _src[_pos] != '*' || _src[_pos + 1] != '*' || _src[_pos + 2] != '*')
        { _pos = saved; return null; }
        _pos += 3;

        // Name must be a valid identifier
        if (string.IsNullOrEmpty(name) || !IsIdentStart(name[0]))
        { _pos = saved; return null; }
        foreach (char ch in name)
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            { _pos = saved; return null; }

        SkipLine(); // rest of the ***Name*** line

        // Skip body: optional opening *** + content lines + closing ***
        SkipSpaces();
        if (_pos + 2 < _src.Length && _src[_pos] == '*' && _src[_pos + 1] == '*' && _src[_pos + 2] == '*')
        {
            // Check it's a standalone *** (not another ***Name***)
            int peek = _pos + 3;
            while (peek < _src.Length && _src[peek] != '\n' && _src[peek] != '\r')
            {
                if (_src[peek] != '*' && _src[peek] != ' ' && _src[peek] != '\t')
                {
                    peek = -1; // has non-star non-space chars → it's a named label, not a body marker
                    break;
                }
                peek++;
            }

            if (peek != -1)
            {
                SkipLine(); // consume body-open ***
                // Skip body content until closing ***
                while (_pos < _src.Length)
                {
                    SkipSpaces();
                    if (_pos + 2 < _src.Length &&
                        _src[_pos] == '*' && _src[_pos + 1] == '*' && _src[_pos + 2] == '*')
                    {
                        SkipLine(); // consume body-close ***
                        break;
                    }
                    SkipLine();
                }
            }
        }

        return name;
    }

    // ── #Include parser ─────────────────────────────────────────────────────

    /// <summary>
    /// If the current position is at a <c>#Include "path" as Alias</c> line, parses
    /// and returns it; otherwise returns <c>null</c> and leaves position unchanged
    /// so the caller can fall through to <see cref="SkipLine"/>.
    /// </summary>
    private ScriptInclude? TryParseInclude(out bool isExtends)
    {
        isExtends = false;
        // Expect '#' at _pos
        int saved = _pos;
        _pos++; // skip '#'
        SkipInlineSpaces();

        // Read directive name
        if (_pos >= _src.Length || !char.IsLetter(_src[_pos])) { _pos = saved; SkipLine(); return null; }
        var directive = ReadIdentifier();
        bool isInclude = directive.Equals("Include", StringComparison.OrdinalIgnoreCase);
        isExtends = directive.Equals("Extends", StringComparison.OrdinalIgnoreCase);
        if (!isInclude && !isExtends)
        {
            SkipLine();
            return null;
        }

        SkipInlineSpaces();
        if (_pos >= _src.Length || _src[_pos] != '"') { SkipLine(); return null; }

        // Read the quoted path
        _pos++; // skip opening '"'
        var pathStart = _pos;
        while (_pos < _src.Length && _src[_pos] != '"' && _src[_pos] != '\n') _pos++;
        var path = _src.Substring(pathStart, _pos - pathStart);
        if (_pos < _src.Length && _src[_pos] == '"') _pos++; // skip closing '"'

        SkipInlineSpaces();

        // For #Extends there is no "as Alias" — synthesise the alias from the filename.
        if (isExtends)
        {
            // Derive alias from filename: "Modes/ModeBase2.Script.txt" → "ModeBase2"
            var leaf = path.Split('/');
            var aliasStr = leaf[leaf.Length - 1];
            if (aliasStr.EndsWith(".Script.txt", StringComparison.OrdinalIgnoreCase))
                aliasStr = aliasStr.Substring(0, aliasStr.Length - ".Script.txt".Length);
            SkipLine();
            return new ScriptInclude { Path = path, Alias = aliasStr };
        }

        // Expect 'as'
        if (_pos + 2 <= _src.Length &&
            _src[_pos] == 'a' && _pos + 1 < _src.Length && _src[_pos + 1] == 's' &&
            (_pos + 2 >= _src.Length || !char.IsLetterOrDigit(_src[_pos + 2])))
        {
            _pos += 2;
            SkipInlineSpaces();
        }
        else
        {
            SkipLine();
            return null;
        }

        if (_pos >= _src.Length || !IsIdentStart(_src[_pos])) { SkipLine(); return null; }
        var alias = ReadIdentifier();
        SkipLine();

        return new ScriptInclude { Path = path, Alias = alias };
    }

    // ── #RequireContext extraction ────────────────────────────────────────────

    private string? ExtractRequireContext()
    {
        var m = Regex.Match(_src, @"^#RequireContext\s+(\w+)", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── #Const extraction ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all <c>#Const Name value</c> declarations whose values can be
    /// represented as a C# const (integer, real, boolean, or string literal).
    /// Complex values (ManiaScript arrays, vectors, …) are silently skipped.
    /// Constants whose names contain <c>Private</c> are excluded.
    /// </summary>
    private List<ScriptConst> ExtractConsts()
    {
        var result = new List<ScriptConst>();
        // Match: #Const  Name  value  (optional trailing ///< comment)
        var regex = new Regex(
            @"^#Const\s+(\w+)\s+(.+?)(?:\s*//.*)?$",
            RegexOptions.Multiline);
        foreach (Match m in regex.Matches(_src))
        {
            var name = m.Groups[1].Value;
            // Skip private-by-convention constants.
            if (name.IndexOf("Private", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var raw = m.Groups[2].Value.Trim();
            // Skip complex values (ManiaScript arrays, vectors, etc.).
            if (raw.Length == 0 || raw[0] == '[' || raw[0] == '<') continue;
            result.Add(new ScriptConst { Name = name, RawValue = raw });
        }
        return result;
    }

    // ── #Struct extraction ────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all <c>#Struct Name { ... }</c> declarations from the source.
    /// Each struct's fields are parsed for their ManiaScript type and name.
    /// </summary>
    private List<ScriptStruct> ExtractStructs()
    {
        var result = new List<ScriptStruct>();
        // Match: #Struct  Name  {  ...body...  }
        // Uses Singleline so '.' matches newlines within the braced body.
        var structRegex = new Regex(
            @"#Struct\s+(\w+)\s*\{([^}]*)\}",
            RegexOptions.Singleline);

        // Match each field line: Type Name; or Type Name = default;
        var fieldRegex = new Regex(
            @"^\s*(\w+(?:\[\w*\])?)\s+(\w+)\s*(?:=\s*[^;]*)?\s*;",
            RegexOptions.Multiline);

        foreach (Match sm in structRegex.Matches(_src))
        {
            var structName = sm.Groups[1].Value;
            var body = sm.Groups[2].Value;

            var fields = new List<ScriptStructField>();
            foreach (Match fm in fieldRegex.Matches(body))
            {
                fields.Add(new ScriptStructField
                {
                    Type = fm.Groups[1].Value,
                    Name = fm.Groups[2].Value,
                });
            }

            result.Add(new ScriptStruct { Name = structName, Fields = fields });
        }

        return result;
    }

    // ── section state machine ─────────────────────────────────────────────────

    private void UpdateSectionState(string lineContent)
    {
        // Strip separator characters (-, ~, =, *, /, space) so
        //   "// -------- //"  → ""   (no match)
        //   "// Private"      → "Private"
        //   "// Public"       → "Public"
        var stripped = Regex.Replace(lineContent, @"[-~=*/\\ ]+", " ").Trim();
        if (string.Equals(stripped, "Private", StringComparison.OrdinalIgnoreCase))
            _inPrivateSection = true;
        else if (string.Equals(stripped, "Public", StringComparison.OrdinalIgnoreCase))
            _inPrivateSection = false;
    }

    /// <summary>
    /// Returns true if a stripped <c>//</c> comment line is a section header rather
    /// than real documentation (e.g. "Functions", "MARK: Libraries", …).
    /// </summary>
    private static bool IsSectionKeyword(string stripped) =>
        stripped.StartsWith("MARK:", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Functions",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Constants",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Libraries",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Globals",    StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Globales",   StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Structures", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Includes",   StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Private",    StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stripped, "Public",     StringComparison.OrdinalIgnoreCase);

    // ── function definition parser ────────────────────────────────────────────

    private bool TryReadFunctionDef(string? rawDoc, out ScriptFunction? func)
    {
        func = null;

        SkipSpaces();
        var returnType = TryReadType();
        if (returnType is null) return false;

        // "declare" at the top level is a global variable declaration, not a function.
        // "extends" / "typedef" are directives that look like identifiers.
        if (returnType is "declare" or "extends" or "typedef" or "implements")
        {
            SkipLine();
            return true; // consumed the line; no function yielded
        }

        SkipInlineSpaces();
        if (_pos >= _src.Length || !IsIdentStart(_src[_pos])) return false;

        var funcName = ReadIdentifier();

        SkipInlineSpaces();
        if (_pos >= _src.Length || _src[_pos] != '(') return false;
        _pos++; // consume '('

        var parameters = ReadParameters();

        // Expect '{' — the function body opener (may be on the next line)
        SkipSpaces();
        if (_pos >= _src.Length || _src[_pos] != '{') return false;

        SkipBracedBody();

        bool isPrivate = _inPrivateSection
            || funcName.StartsWith("Private_", StringComparison.Ordinal);

        if (!isPrivate)
        {
            func = new ScriptFunction
            {
                Doc = ParseDocComment(rawDoc),
                ReturnType = returnType,
                Name = funcName,
                Parameters = parameters,
            };
        }

        return true;
    }

    // ── parameter list ────────────────────────────────────────────────────────

    private List<ScriptParameter> ReadParameters()
    {
        var result = new List<ScriptParameter>();

        while (_pos < _src.Length && _src[_pos] != ')')
        {
            SkipSpaces();
            if (_pos >= _src.Length || _src[_pos] == ')') break;

            var type = TryReadType();
            if (type is null) break;

            SkipInlineSpaces();
            if (_pos >= _src.Length || !IsIdentStart(_src[_pos])) break;

            var name = ReadIdentifier();
            result.Add(new ScriptParameter { Type = type, Name = name });

            SkipSpaces();
            if (_pos < _src.Length && _src[_pos] == ',') _pos++;
        }

        if (_pos < _src.Length && _src[_pos] == ')') _pos++;
        return result;
    }

    // ── type reader ───────────────────────────────────────────────────────────

    private string? TryReadType()
    {
        if (_pos >= _src.Length || !IsIdentStart(_src[_pos])) return null;

        var baseName = ReadQualifiedIdentifier();

        // Array / associative-array suffix: [] or [KeyType]
        if (_pos < _src.Length && _src[_pos] == '[')
        {
            _pos++; // consume '['
            SkipSpaces();
            if (_pos < _src.Length && _src[_pos] == ']')
            {
                _pos++;
                return baseName + "[]";
            }
            var keyType = (_pos < _src.Length && IsIdentStart(_src[_pos]))
                ? ReadQualifiedIdentifier()
                : null;
            SkipSpaces();
            if (_pos < _src.Length && _src[_pos] == ']') _pos++;
            return keyType is not null
                ? baseName + "[" + keyType + "]"
                : baseName + "[]";
        }

        return baseName;
    }

    // ── identifier readers ────────────────────────────────────────────────────

    private string ReadQualifiedIdentifier()
    {
        var sb = new StringBuilder();
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
            sb.Append(_src[_pos++]);
        // Handle :: qualifier (e.g. CUIConfig::EUISound)
        while (_pos + 1 < _src.Length && _src[_pos] == ':' && _src[_pos + 1] == ':')
        {
            sb.Append("::");
            _pos += 2;
            while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
                sb.Append(_src[_pos++]);
        }
        return sb.ToString();
    }

    private string ReadIdentifier()
    {
        var sb = new StringBuilder();
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
            sb.Append(_src[_pos++]);
        return sb.ToString();
    }

    // ── body / string skipping ────────────────────────────────────────────────

    private void SkipBracedBody()
    {
        if (_pos >= _src.Length || _src[_pos] != '{') return;
        int depth = 0;
        while (_pos < _src.Length)
        {
            char c = _src[_pos++];
            switch (c)
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    if (--depth == 0) return;
                    break;
                case '"':
                    // Check triple-quoted string (current _pos is after the consumed '"')
                    if (_pos + 1 < _src.Length && _src[_pos] == '"' && _src[_pos + 1] == '"')
                    {
                        _pos += 2; // skip the two additional '"'
                        // Find closing """
                        while (_pos + 2 < _src.Length &&
                               !(_src[_pos] == '"' && _src[_pos + 1] == '"' && _src[_pos + 2] == '"'))
                            _pos++;
                        if (_pos + 2 < _src.Length) _pos += 3;
                    }
                    else
                    {
                        // Simple string
                        while (_pos < _src.Length && _src[_pos] != '"' && _src[_pos] != '\n')
                        {
                            if (_src[_pos] == '\\') _pos++;
                            _pos++;
                        }
                        if (_pos < _src.Length && _src[_pos] == '"') _pos++;
                    }
                    break;
                case '/':
                    if (_pos < _src.Length && _src[_pos] == '/')
                    {
                        _pos++;
                        while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
                    }
                    else if (_pos < _src.Length && _src[_pos] == '*')
                    {
                        _pos++;
                        while (_pos + 1 < _src.Length && !(_src[_pos] == '*' && _src[_pos + 1] == '/'))
                            _pos++;
                        if (_pos + 1 < _src.Length) _pos += 2;
                    }
                    break;
            }
        }
    }

    private (string? Text, bool IsDoc) ReadBlockComment()
    {
        // _pos is at the first '/'
        _pos += 2; // skip '/*'
        bool isDoc = _pos < _src.Length && _src[_pos] == '*';
        // Skip the extra '*' of '/**' (but not a standalone '/**/' which is already '/*' + '/')
        if (isDoc && _pos + 1 < _src.Length && _src[_pos + 1] == '/')
        {
            _pos += 2; // skip '*/'
            return (null, false);
        }

        var sb = new StringBuilder();
        while (_pos + 1 < _src.Length && !(_src[_pos] == '*' && _src[_pos + 1] == '/'))
            sb.Append(_src[_pos++]);
        if (_pos + 1 < _src.Length) _pos += 2; // skip '*/'

        return (sb.ToString(), isDoc);
    }

    private void SkipTripleQuotedString()
    {
        // _pos is at the first '"' of """
        _pos += 3;
        while (_pos + 2 < _src.Length)
        {
            if (_src[_pos] == '"' && _src[_pos + 1] == '"' && _src[_pos + 2] == '"')
            {
                _pos += 3;
                return;
            }
            _pos++;
        }
        _pos = _src.Length;
    }

    private void SkipSimpleString()
    {
        // _pos is at the '"'
        _pos++; // skip opening '"'
        while (_pos < _src.Length && _src[_pos] != '"' && _src[_pos] != '\n')
        {
            if (_src[_pos] == '\\') _pos++;
            _pos++;
        }
        if (_pos < _src.Length && _src[_pos] == '"') _pos++;
    }

    // ── generic skip helpers ──────────────────────────────────────────────────

    private string ReadToEol()
    {
        int start = _pos;
        while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
        var result = _src.Substring(start, _pos - start);
        if (_pos < _src.Length) _pos++; // skip '\n'
        return result;
    }

    private void SkipLine()
    {
        while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
        if (_pos < _src.Length) _pos++;
    }

    private void SkipSpaces()
    {
        while (_pos < _src.Length &&
               (_src[_pos] is ' ' or '\t' or '\r' or '\n'))
            _pos++;
    }

    private void SkipInlineSpaces()
    {
        while (_pos < _src.Length && (_src[_pos] is ' ' or '\t'))
            _pos++;
    }

    private char Peek(int offset = 0)
    {
        int idx = _pos + offset;
        return idx < _src.Length ? _src[idx] : '\0';
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

    // ── doc comment parser ────────────────────────────────────────────────────

    private static ScriptDocComment? ParseDocComment(string? raw)
    {
        if (raw is null) return null;

        var summaryParts = new List<string>();
        var paramList = new List<(string Name, string Desc)>();
        string? returns = null;

        foreach (var line in raw.Split('\n'))
        {
            // Strip leading whitespace and '*' decoration
            var trimmed = line.Trim().TrimStart('*').Trim();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("@param", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed.Substring(6).Trim();
                var parts = rest.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    paramList.Add((parts[0].TrimStart('_'), parts[1].Trim()));
                else if (parts.Length == 1)
                    paramList.Add((parts[0].TrimStart('_'), ""));
            }
            else if (trimmed.StartsWith("@return", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed.Substring(7).Trim();
                if (rest.Length > 0) returns = rest;
            }
            else
            {
                summaryParts.Add(trimmed);
            }
        }

        var summary = summaryParts.Count > 0
            ? string.Join(" ", summaryParts)
            : null;

        if (summary is null && paramList.Count == 0 && returns is null)
            return null;

        return new ScriptDocComment
        {
            Summary = summary,
            Params = paramList,
            Returns = returns,
        };
    }
}
