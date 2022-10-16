using System.Collections.Immutable;

namespace ManiaScriptSharp;

public interface IMode
{
    string Version { get; }
    ImmutableArray<string> CompatibleMapTypes { get; }
}