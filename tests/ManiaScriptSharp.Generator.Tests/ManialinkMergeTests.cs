using System;
using System.Collections.Generic;
using Xunit;
using ManiaScriptSharp.Generator.Emission;

namespace ManiaScriptSharp.Generator.Tests;

public class ManialinkMergeTests
{
    private const string SimpleXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" +
        "<manialink version=\"3\">\r\n" +
        "  <frame/>\r\n" +
        "</manialink>\r\n";

    private const string Script = "Void Foo() { log(\"hi\"); }\r\n";

    [Fact]
    public void MergeIntoManialink_InjectsScriptBeforeClosingTag()
    {
        var result = ManiaScriptGenerator.MergeIntoManialink(SimpleXml, Script, "MyClass");
        Assert.Contains("<script><![CDATA[", result);
        Assert.Contains(Script, result);
        Assert.Contains("]]></script>", result);
        var scriptIdx = result.IndexOf("<script>", StringComparison.Ordinal);
        var closeIdx = result.LastIndexOf("</manialink>", StringComparison.OrdinalIgnoreCase);
        Assert.True(scriptIdx < closeIdx, "script block must appear before </manialink>");
    }

    [Fact]
    public void MergeIntoManialink_SetsNameAttribute_WhenAbsent()
    {
        var result = ManiaScriptGenerator.MergeIntoManialink(SimpleXml, Script, "MyClass");
        Assert.Contains("name=\"MyClass\"", result);
    }

    [Fact]
    public void MergeIntoManialink_ReplacesExistingNameAttribute()
    {
        var xml = SimpleXml.Replace("<manialink version=\"3\">", "<manialink version=\"3\" name=\"Old\">");
        var result = ManiaScriptGenerator.MergeIntoManialink(xml, Script, "NewName");
        Assert.Contains("name=\"NewName\"", result);
        Assert.DoesNotContain("name=\"Old\"", result);
    }

    [Fact]
    public void MergeIntoManialink_ReplacesExistingScriptBlock()
    {
        var xml = SimpleXml.Replace("</manialink>",
            "<script><![CDATA[\r\nVoid Old() {}\r\n]]></script>\r\n</manialink>");
        var result = ManiaScriptGenerator.MergeIntoManialink(xml, Script, "MyClass");
        Assert.DoesNotContain("Void Old()", result);
        Assert.Contains(Script, result);
        // Only one script block.
        Assert.Equal(1, CountOccurrences(result, "<script>"));
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) { count++; idx++; }
        return count;
    }
}

public class ManialinkValidateTests
{
    [Fact]
    public void ValidateManialinkTemplate_ValidXml_ReturnsTrue()
    {
        var xml = "<?xml version=\"1.0\" ?><manialink version=\"3\"><frame/></manialink>";
        Assert.True(ManiaScriptGenerator.ValidateManialinkTemplate(xml, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ValidateManialinkTemplate_MalformedXml_ReturnsFalse()
    {
        var xml = "<manialink version=\"3\"><unclosed></manialink>";
        Assert.False(ManiaScriptGenerator.ValidateManialinkTemplate(xml, out var error));
        Assert.NotNull(error);
        Assert.Contains("well-formed", error);
    }

    [Fact]
    public void ValidateManialinkTemplate_WrongRoot_ReturnsFalse()
    {
        var xml = "<?xml version=\"1.0\" ?><root version=\"3\"/>";
        Assert.False(ManiaScriptGenerator.ValidateManialinkTemplate(xml, out var error));
        Assert.NotNull(error);
        Assert.Contains("<manialink>", error);
    }

    [Fact]
    public void ValidateManialinkTemplate_MissingVersionAttribute_ReturnsFalse()
    {
        var xml = "<?xml version=\"1.0\" ?><manialink><frame/></manialink>";
        Assert.False(ManiaScriptGenerator.ValidateManialinkTemplate(xml, out var error));
        Assert.NotNull(error);
        Assert.Contains("version", error);
    }

    [Fact]
    public void ValidateManialinkTemplate_WrongVersion_ReturnsFalse()
    {
        var xml = "<?xml version=\"1.0\" ?><manialink version=\"2\"><frame/></manialink>";
        Assert.False(ManiaScriptGenerator.ValidateManialinkTemplate(xml, out var error));
        Assert.NotNull(error);
        Assert.Contains("version=\"2\"", error);
    }
}

public class ManialinkBindingValidationTests
{
    private const string XmlWithIds =
        "<?xml version=\"1.0\" ?>" +
        "<manialink version=\"3\">" +
        "<label id=\"LabelCountdown\"/>" +
        "<quad id=\"QuadMapName\"/>" +
        "</manialink>";

    private static ManialinkBinding Binding(string xmlId, bool ignoreValidation = false)
        => new ManialinkBinding(xmlId, xmlId, "CMlLabel", ignoreValidation);

    [Fact]
    public void FindMissingManialinkBindings_AllPresent_ReturnsEmpty()
    {
        var bindings = new List<ManialinkBinding>
        {
            Binding("LabelCountdown"),
            Binding("QuadMapName"),
        };
        var missing = ManiaScriptGenerator.FindMissingManialinkBindings(XmlWithIds, bindings);
        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingManialinkBindings_MissingId_ReturnsIt()
    {
        var bindings = new List<ManialinkBinding>
        {
            Binding("LabelCountdown"),
            Binding("DoesNotExist"),
        };
        var missing = ManiaScriptGenerator.FindMissingManialinkBindings(XmlWithIds, bindings);
        Assert.Single(missing);
        Assert.Equal("DoesNotExist", missing[0].XmlId);
    }

    [Fact]
    public void FindMissingManialinkBindings_IgnoreValidation_Skipped()
    {
        var bindings = new List<ManialinkBinding>
        {
            Binding("LabelCountdown"),
            Binding("DoesNotExist", ignoreValidation: true),
        };
        var missing = ManiaScriptGenerator.FindMissingManialinkBindings(XmlWithIds, bindings);
        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingManialinkBindings_EmptyBindings_ReturnsEmpty()
    {
        var missing = ManiaScriptGenerator.FindMissingManialinkBindings(XmlWithIds, []);
        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingManialinkBindings_MultipleMissing_ReturnsAll()
    {
        var bindings = new List<ManialinkBinding>
        {
            Binding("Missing1"),
            Binding("Missing2"),
            Binding("LabelCountdown"),
        };
        var missing = ManiaScriptGenerator.FindMissingManialinkBindings(XmlWithIds, bindings);
        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, b => b.XmlId == "Missing1");
        Assert.Contains(missing, b => b.XmlId == "Missing2");
    }
}
