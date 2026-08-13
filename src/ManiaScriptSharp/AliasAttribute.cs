namespace ManiaScriptSharp;

/// <summary>Marks an assignment target as using ManiaScript alias semantics (<c>&lt;=&gt;</c>).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AliasAttribute : Attribute;
