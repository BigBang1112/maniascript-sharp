using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public interface IMetadataProvider
{
    Dictionary<string, IStrongBox> Metadata { get; }
}
