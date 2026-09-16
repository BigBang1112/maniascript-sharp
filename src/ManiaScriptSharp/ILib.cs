namespace ManiaScriptSharp;

/// <summary>
/// Marker interface for classes that represent a ManiaScript library.
/// Use it with a context base class (for example, <c>class MyLib : CMap, ILib</c>) when the
/// library accesses that context through inheritance. Library scripts do not emit
/// <c>#RequireContext</c>.
/// </summary>
public interface ILib;

/// <summary>
/// Legacy generic library form. <typeparamref name="T"/> describes the type exposed through
/// <see cref="Context"/> for C# type checking; it does not emit <c>#RequireContext</c>.
/// </summary>
public interface ILib<T> : ILib
{
    T Context { get; }
}
