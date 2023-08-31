namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Property)]
public class NetreadAttribute : Attribute
{
    public NetFor For { get; }

    public NetreadAttribute(NetFor @for)
    {
        For = @for;
    }
}
