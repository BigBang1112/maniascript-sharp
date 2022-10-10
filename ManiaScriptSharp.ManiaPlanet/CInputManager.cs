namespace ManiaScriptSharp;

public partial class CInputManager
{
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PadButtonPressEventHandler(CInputPad pad, EButton button, bool isAutoRepeat, int keyCode, string keyName);
}