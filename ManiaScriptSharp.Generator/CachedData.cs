namespace ManiaScriptSharp.Generator;

public static class CachedData
{
    public static HashSet<string> DeclarationModes { get; } = new()
    {
        "Local",
        "Metadata",
        "Persistent",
        "Netread",
        "Netwrite"
    };
}