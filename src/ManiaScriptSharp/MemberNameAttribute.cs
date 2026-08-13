namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Delegate)]
public sealed class MemberNameAttribute : Attribute
{
    public string MemberName { get; }

    public MemberNameAttribute(string memberName)
    {
        MemberName = memberName;
    }
}
