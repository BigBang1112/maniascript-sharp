namespace ManiaScriptSharp;

/// <summary>
/// Runtime stubs for ManiaScript built-ins. The generator emits the corresponding ManiaScript calls.
/// </summary>
public static class ManiaScript
{
    public static void Log(string message) { }
    public static void Log(object value) { }
    public static void Assert(bool condition) { }
    public static void Assert(bool condition, string message) { }
    public static void Yield() { }
    public static void Sleep(int milliseconds) { }
    public static void Wait(Func<bool> condition) { }
}
