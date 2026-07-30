using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public interface INetworkProvider
{
    Dictionary<string, IStrongBox> NetworkData { get; }
}
