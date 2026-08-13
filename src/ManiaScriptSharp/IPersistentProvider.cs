using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public interface IPersistentProvider
{
    Dictionary<string, IStrongBox> Persistent { get; }
}
