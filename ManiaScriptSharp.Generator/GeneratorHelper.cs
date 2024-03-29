using System.Xml.Schema;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public record GeneratorHelper(GeneratorExecutionContext Context,
                              string ProjectDir,
                              string OutputDir,
                              string RootNamespace,
                              XmlSchema? XmlSchema,
                              BuildSettings? BuildSettings);