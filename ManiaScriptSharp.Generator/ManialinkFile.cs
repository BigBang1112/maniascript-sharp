using Microsoft.CodeAnalysis;
using System.Text;
using System.Xml;

namespace ManiaScriptSharp.Generator;

public class ManialinkFile : IGeneratedFile
{
    public static ManialinkFile Generate(string xml, INamedTypeSymbol scriptSymbol, TextWriter writer)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        using var scriptWriter = new StringWriter();

        var newLineCount = 2;

        for (int i = 0; i < newLineCount; i++)
        {
            scriptWriter.WriteLine();
        }

        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, scriptWriter, isEmbeddedInManialink: true);
        var head = headBuilder.AnalyzeAndBuild();

        // Trick to check if the script is empty, compatible with any kind of new line, will change in the future
        if (scriptWriter.GetStringBuilder().Length > scriptWriter.NewLine.Length * 2)
        {
            var scriptCData = doc.CreateCDataSection(scriptWriter.ToString());

            var scriptElement = doc.CreateElement("script");
            scriptElement.AppendChild(scriptCData);

            doc.DocumentElement.AppendChild(scriptElement);
        }
        
        doc.Save(writer);

        return new ManialinkFile();
    }
}