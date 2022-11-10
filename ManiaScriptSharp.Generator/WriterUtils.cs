using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator;

public record WriterUtils(int Indent, ImmutableArray<ParameterSyntax> Parameters, ManiaScriptBodyBuilder BodyBuilder);