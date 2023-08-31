namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Property)]
public class NetwriteAttribute : Attribute
{
    public NetFor For { get; }

    public NetwriteAttribute(NetFor @for = NetFor.Teams0)
    {
        For = @for;
    }
}
