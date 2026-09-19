using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Classifies C# assignment syntax that represents object/dictionary initialization.</summary>
internal static class AssignmentSyntax
{
    /// <summary>
    /// An entry in an object or dictionary initializer uses assignment syntax, but does not
    /// evaluate to an assignment value. For example, <c>new Dictionary&lt;Text, Integer&gt;
    /// { ["score"] = 1 }</c> must remain supported.
    /// </summary>
    public static bool IsInitializerEntry(AssignmentExpressionSyntax assignment)
        => assignment.Parent is InitializerExpressionSyntax;
}
