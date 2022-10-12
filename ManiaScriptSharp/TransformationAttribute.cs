namespace ManiaScriptSharp;

public class TransformationAttribute : Attribute
{
    public string TransformationMethodName { get; }
    public bool IgnoreInAnonymous { get; set; }
    
    public TransformationAttribute(string transformationMethodName)
    {
        TransformationMethodName = transformationMethodName;
    }
}