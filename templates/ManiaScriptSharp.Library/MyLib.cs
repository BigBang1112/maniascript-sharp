using ManiaScriptSharp;

namespace MyLib;

public class MyLib : ILib<CNod>
{
    public required CNod Context { get; init; }

    public string Greet()
    {
        return "Hello from MyLib!";
    }
}
