namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Method)]
public class ManiaScriptEventMethodAttribute : Attribute
{
    public string DelegateName { get; }
    
    public ManiaScriptEventMethodAttribute(string delegateName)
    {
        DelegateName = delegateName;
    }
}