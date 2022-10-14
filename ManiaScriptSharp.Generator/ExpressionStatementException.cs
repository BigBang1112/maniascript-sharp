using System.Runtime.Serialization;

namespace ManiaScriptSharp.Generator;

[Serializable]
public class ExpressionStatementException : Exception
{
    public ExpressionStatementException() : base()
    {
    }

    public ExpressionStatementException(string message) : base(message)
    {
    }

    public ExpressionStatementException(string message, Exception inner) : base(message, inner)
    {
    }

    protected ExpressionStatementException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
    {
    }
}