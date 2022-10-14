using Microsoft.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.Generator;

public class ManialinkFile : IGeneratedFile
{
    public static ManialinkFile Generate(Stream xmlStream, INamedTypeSymbol scriptSymbol, SemanticModel semanticModel,
        TextWriter writer, GeneratorHelper helper)
    {
        _ = ValidateManialinkXml(xmlStream, scriptSymbol, helper);

        xmlStream.Position = 0;
        
        var doc = new XmlDocument();
        doc.Load(xmlStream);

        if (doc.DocumentElement is null)
        {
            throw new Exception("Invalid manialink XML: No root element.");
        }

        var descriptionComment = doc.CreateComment("This manialink was generated with ManiaScriptSharp by BigBang1112");

        doc.DocumentElement.PrependChild(descriptionComment);

        using var scriptWriter = new StringWriter();

        var newLineCount = 2;

        for (int i = 0; i < newLineCount; i++)
        {
            scriptWriter.WriteLine();
        }

        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, semanticModel, scriptWriter, helper, doc);
        var head = headBuilder.AnalyzeAndBuild();
        
        var bodyBuilder = new ManiaScriptBodyBuilder(scriptSymbol, semanticModel, scriptWriter, head, helper);
        var body = bodyBuilder.AnalyzeAndBuild();

        // Trick to check if the script is empty, compatible with any kind of new line, will change in the future
        if (scriptWriter.GetStringBuilder().Length > scriptWriter.NewLine.Length * 2)
        {
            var scriptCData = doc.CreateCDataSection(scriptWriter.ToString());

            var scriptElement = doc.CreateElement("script", "manialink");
            scriptElement.AppendChild(scriptCData);
            
            doc.DocumentElement.AppendChild(scriptElement);
        }

        using var xmlWriter = new XmlTextWriter(writer)
        {
            Formatting = Formatting.Indented,
            Indentation = 4
        };
        
        doc.Save(xmlWriter);

        return new ManialinkFile();
    }

    private static bool ValidateManialinkXml(Stream xmlStream, ISymbol scriptSymbol, GeneratorHelper helper)
    {
        if (helper.XmlSchema is null)
        {
            return false;
        }
        
        using var xmlReader = new StreamReader(xmlStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        
        var rs = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema
        };
        
        rs.Schemas.Add(helper.XmlSchema!);
        
        rs.ValidationEventHandler += (sender, e) =>
        {
            var diagnostic = CreateXmlValidationDiagnostic(scriptSymbol, e);
            
            helper.Context.ReportDiagnostic(diagnostic);

            throw e.Exception;
        };

        using var reader = XmlReader.Create(xmlReader, rs);
        
        while (reader.Read())
        {
        }
        
        return true;
    }

    private static Diagnostic CreateXmlValidationDiagnostic(ISymbol scriptSymbol, ValidationEventArgs e)
    {
        var severity = e.Severity switch
        {
            XmlSeverityType.Error => DiagnosticSeverity.Error,
            XmlSeverityType.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Info
        };
        
        var descriptorError = new DiagnosticDescriptor("MSSG001", "Manialink XML validation",
            e.Message, "Manialink",  severity, true);

        var linePosition = new LinePosition(e.Exception.LineNumber - 1, e.Exception.LinePosition - 1);

        var location = Location.Create($"{scriptSymbol.Name}.xml", new(), new(linePosition, linePosition));

        return Diagnostic.Create(descriptorError, location);
    }
}