using Microsoft.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.Generator;

public class ManialinkFile : IGeneratedFile
{
    private const string manialinkXsd = "https://raw.githubusercontent.com/reaby/manialink-xsd/main/manialink_v3.xsd";
    
    public static ManialinkFile Generate(string xml, INamedTypeSymbol scriptSymbol, TextWriter writer, GeneratorSettings settings)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        if (doc.DocumentElement is null)
        {
            throw new Exception("Invalid manialink XML: No root element.");
        }

        if (settings.XmlSchema is not null)
        {
            doc.Schemas.Add(settings.XmlSchema);

            //var xmlModel = doc.CreateProcessingInstruction("xml-model", $"href=\"{manialinkXsd}\"");
            //doc.InsertBefore(xmlModel, doc.FirstChild);

            ValidateManialinkXml(xml, scriptSymbol, settings);
        }

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

        using var xmlWriter = new XmlTextWriter(writer);
        xmlWriter.Formatting = Formatting.Indented;
        xmlWriter.Indentation = 4;
        doc.Save(xmlWriter);

        return new ManialinkFile();
    }

    private static void ValidateManialinkXml(string xml, ISymbol scriptSymbol, GeneratorSettings settings)
    {
        var rs = new XmlReaderSettings();
        rs.Schemas.Add(settings.XmlSchema!);
        rs.ValidationType = ValidationType.Schema;
        rs.ValidationEventHandler += (sender, e) =>
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    var descriptorError = new DiagnosticDescriptor("MSSG001", "Manialink XML validation error",
                        e.Message, "Manialink", DiagnosticSeverity.Error, true);
                    
                    var linePositionStart = new LinePosition(e.Exception.LineNumber - 1, e.Exception.LinePosition - 1);
                    var linePositionEnd = new LinePosition(e.Exception.LineNumber - 1, e.Exception.LinePosition - 1 + 3);

                    var location = Location.Create($"{scriptSymbol.Name}.xml", TextSpan.FromBounds(0, xml.Length),
                        new LinePositionSpan(linePositionStart, linePositionEnd));

                    var diagnosticError = Diagnostic.Create(descriptorError, location);

                    settings.Context.ReportDiagnostic(diagnosticError);
                    break;
                case XmlSeverityType.Warning:
                    var descriptorWarning = new DiagnosticDescriptor("MSSG002", "Manialink XML validation warning",
                        e.Message, "Manialink", DiagnosticSeverity.Warning, true);

                    var diagnosticWarning = Diagnostic.Create(descriptorWarning, Location.None);

                    settings.Context.ReportDiagnostic(diagnosticWarning);
                    break;
            }
        };

        using var strReader = new StringReader(xml);
        using var reader = XmlReader.Create(strReader, rs);
        
        while (reader.Read())
        {
        }
    }
}