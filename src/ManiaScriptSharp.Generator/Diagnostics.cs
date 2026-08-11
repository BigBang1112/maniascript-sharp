using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor EmissionFailed = new(
        id: "MSS001",
        title: "ManiaScript emission failed",
        messageFormat: "Failed to translate '{0}' to ManiaScript: {1}",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FileWriteFailed = new(
        id: "MSS002",
        title: "ManiaScript file write failed",
        messageFormat: "Could not write '{0}': {1}",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Unsupported = new(
        id: "MSS003",
        title: "Unsupported C# construct",
        messageFormat: "C# construct '{0}' is not translatable to ManiaScript.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LibContextAccess = new(
        id: "MSS004",
        title: "ILib.Context accessed directly",
        messageFormat: "Do not access 'Context' on an ILib field; call members directly instead.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidXmlTemplate = new(
        id: "MSS005",
        title: "Invalid Manialink XML template",
        messageFormat: "The XML template for '{0}' is invalid: {1}",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ManialinkControlNotFound = new(
        id: "MSS006",
        title: "ManialinkControl id not found in XML template",
        messageFormat: "No element with id='{0}' was found in the XML template. Add IgnoreValidation = true to suppress.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CNodeInstantiation = new(
        id: "MSS007",
        title: "Cannot instantiate CNod class",
        messageFormat: "'{0}' is a ManiaScript API class and cannot be instantiated with 'new'. Instances are provided by the runtime.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedLinq = new(
        id: "MSS008",
        title: "Unsupported LINQ method",
        messageFormat: "LINQ method '{0}' has no direct ManiaScript equivalent and cannot be translated.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoLoopIgnoresEvents = new(
        id: "MSS009",
        title: "Event registrations ignored due to [NoLoop]",
        messageFormat: "'{0}' has [NoLoop] applied, so its registered event handlers will never run because the event-processing loop is not emitted.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ContextEntryPointCalledDirectly = new(
        id: "MSS010",
        title: "IContext.Main/Loop called directly",
        messageFormat: "'{0}()' is invoked by the generated main() wrapper and must not be called directly.",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CircularFunctionCalls = new(
        id: "MSS011",
        title: "Circular calls between functions",
        messageFormat: "'{0}' takes part in a call cycle with another function. ManiaScript requires every function to be defined before it is called and does not allow circular calls between distinct functions (self-recursion is fine).",
        category: "ManiaScriptSharp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
