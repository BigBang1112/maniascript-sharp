using System.Runtime.Serialization;

namespace ManiaScriptSharp.Generator;

[Serializable]
public class ExpressionException : Exception
{
    public ExpressionException() : base()
    {
    }

    public ExpressionException(string message) : base(message)
    {
    }

    public ExpressionException(string message, Exception inner) : base(message, inner)
    {
    }

    protected ExpressionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}