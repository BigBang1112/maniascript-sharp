namespace ManiaScriptSharp;

/// <summary>Adds a <c>#Include</c> directive to the generated script.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class IncludeAttribute : Attribute
{
    public string Path { get; }
    public string As { get; set; } = "";
    public IncludeAttribute(string path) { Path = path; }
}
