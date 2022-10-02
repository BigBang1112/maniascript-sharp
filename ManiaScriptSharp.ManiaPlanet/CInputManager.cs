namespace ManiaScriptSharp;

public partial class CInputManager
{
    public delegate void PadButtonPressEventHandler(CInputPad pad, EButton button, bool isAutoRepeat, int keyCode, string keyName);
}