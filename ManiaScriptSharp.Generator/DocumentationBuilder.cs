using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public class DocumentationBuilder
{
    private ManiaScriptBodyBuilder BodyBuilder { get; }

    private TextWriter Writer => BodyBuilder.Writer;

    public DocumentationBuilder(ManiaScriptBodyBuilder bodyBuilder)
    {
        BodyBuilder = bodyBuilder;
    }
    
    public void WriteDocumentation(int ident, ISymbol symbol)
    {
        var docXml = symbol.GetDocumentationCommentXml();

        if (string.IsNullOrWhiteSpace(docXml))
        {
            return;
        }

        var doc = XDocument.Parse(docXml);

        if (doc.Root is null)
        {
            return;
        }

        var summary = doc.Root.Element("summary")?.Value;

        Writer.Write(ident, "/** ");
        Writer.Write(summary?.Trim());

        var isFirst = true;

        var parameters = doc.Root.Elements("param");

        foreach (var parameter in parameters)
        {
            if (isFirst)
            {
                Writer.WriteLine();
                Writer.WriteLine(ident, " *");
                isFirst = false;
            }

            var name = parameter.Attribute("name")?.Value;
            var description = parameter.Value;

            Writer.Write(ident, " *\t@param\t\t");

            Writer.Write(name is null ? "[!missing name!]" : Standardizer.StandardizeUnderscoreName(name));

            Writer.Write("  ");
            Writer.WriteLine(description?.Trim());
        }
        
        var returns = doc.Root.Element("returns")?.Value;
        
        if (!string.IsNullOrWhiteSpace(returns))
        {
            if (isFirst)
            {
                Writer.WriteLine();
                isFirst = false;
            }
            
            Writer.WriteLine(ident, " *");

            Writer.Write(ident, " *\t@return\t\t");
            Writer.WriteLine(returns?.Trim());
        }

        Writer.WriteLine(isFirst ? 0 : ident, " */");
    }
}