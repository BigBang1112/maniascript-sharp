namespace ManiaScriptSharp;

/// <summary>
/// Place on an <see cref="IContext"/> class to suppress the generated
/// <c>while(True) { yield; ... }</c> wrapper entirely. <c>Loop()</c> is not emitted at all,
/// even if it has a body — useful for one-off scripts where <c>Main()</c> runs once and the
/// script then ends.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NoLoopAttribute : Attribute;
