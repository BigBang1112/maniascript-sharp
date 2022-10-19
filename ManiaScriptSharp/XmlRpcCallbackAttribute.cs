namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Method)]
public class XmlRpcCallbackAttribute : Attribute
{
    public string Method { get; }

    public XmlRpcCallbackAttribute(string method)
    {
        Method = method;
    }
}