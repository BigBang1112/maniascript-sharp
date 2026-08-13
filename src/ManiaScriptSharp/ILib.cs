namespace ManiaScriptSharp;

public interface ILib;

public interface ILib<T> : ILib
{
    T Context { get; }
}
