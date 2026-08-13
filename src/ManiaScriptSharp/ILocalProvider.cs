using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public interface ILocalProvider
{
    Dictionary<string, IStrongBox> Local { get; }
}
