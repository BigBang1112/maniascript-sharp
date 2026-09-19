namespace ManiaScriptSharp;

/// <summary>Controls how a C# struct's fields are named in generated ManiaScript.</summary>
public enum StructFieldNameStyle
{
    /// <summary>Use the existing <c>PascalCase</c> convention.</summary>
    PascalCase,

    /// <summary>Emit fields as <c>camelCase</c>.</summary>
    CamelCase,

    /// <summary>Emit fields as <c>snake_case</c>.</summary>
    SnakeCase,
}

/// <summary>
/// Selects the naming convention used for all fields in a generated ManiaScript struct.
/// </summary>
/// <example>
/// <code>
/// [StructFieldNaming(StructFieldNameStyle.SnakeCase)]
/// public struct PlayerState { public int TotalScore; }
/// </code>
/// emits <c>TotalScore</c> as <c>total_score</c>.
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class StructFieldNamingAttribute : Attribute
{
    public StructFieldNameStyle Style { get; }

    public StructFieldNamingAttribute(StructFieldNameStyle style) => Style = style;
}
