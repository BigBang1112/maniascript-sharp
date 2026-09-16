namespace ManiaScriptSharp;

/// <summary>Adds a host-exposed ManiaScript <c>#Command</c> directive to the generated script.</summary>
/// <remarks>
/// Command directives describe the command that a game-mode host exposes. Handle the resulting
/// <c>OnCommand</c> event through the context API in the usual way.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>The command name received from the host, such as <c>Command_SetPause</c>.</summary>
    public string Name { get; }

    /// <summary>The type of each value supplied with the command.</summary>
    public Type[] ParameterTypes { get; }

    /// <summary>Display text shown by the host. Defaults to the command name.</summary>
    public string? As { get; set; }

    /// <summary>Whether the display text is wrapped in <c>_()</c>. Defaults to <see langword="true"/>.</summary>
    public bool Translated { get; set; } = true;

    public CommandAttribute(string name, params Type[] parameterTypes)
    {
        Name = name;
        ParameterTypes = parameterTypes;
    }
}
