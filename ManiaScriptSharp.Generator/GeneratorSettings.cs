using System.IO.Abstractions;
using System.Xml.Schema;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public record GeneratorSettings(GeneratorExecutionContext Context, IFileSystem FileSystem,
    string ProjectDir, string OutputDir, XmlSchema? XmlSchema);