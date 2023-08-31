namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Property)]
public class LocalAttribute : Attribute
{
    public LocalFor For { get; }

    public LocalAttribute(LocalFor @for = LocalFor.This)
    {
        For = @for;
    }
}
