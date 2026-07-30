using System;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests for the event translation system: EventCollector → MainEmitter → StatementEmitter.
/// Each test verifies a distinct aspect of the C# event subscription → ManiaScript
/// foreach/switch-Event translation.
/// </summary>
public class EventEmitterTests : EmitterTestBase
{
    // ────────── Helpers ──────────

    /// Delegate definitions used by several tests, placed in the Test class body.
    private const string ClickDelegates = @"
        [ManiaScriptEvent(""PendingEvents"")]
        public delegate void ClickEventHandler(string controlId);
        public event ClickEventHandler? Click;";

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) { count++; idx++; }
        return count;
    }

    // ────────── No-op cases ──────────

    [Fact]
    public void EmitMain_EmptyClass_ProducesNoOutput()
    {
        Assert.Equal("", EmitMain(""));
    }

    [Fact]
    public void EmitMain_EmptyMainMethod_ProducesNoOutput()
    {
        // No Loop() → no main() wrapper; empty Main() body emits nothing.
        Assert.Equal("", EmitMain("void Main() { }"));
    }

    [Fact]
    public void EmitMain_MainMethodWithStatement_NoHelperFunctions_EmitsDirectly()
    {
        // No other functions → body emitted directly at top level, no main() wrapper.
        var output = EmitMain("void Main() { }");
        Assert.DoesNotContain("main() {", output);
    }

    [Fact]
    public void EmitMain_MainMethodWithHelperFunction_WrapsInMain()
    {
        // Helper function present → main() wrapper required by ManiaScript.
        var output = EmitMain("void Foo() {} void Main() { Foo(); }");
        Assert.Contains("Foo();", output);
        Assert.Contains("main() {", output);
        Assert.DoesNotContain("while", output);
    }

    [Fact]
    public void EmitMain_EmptyLoopMethod_ProducesWhileLoop()
    {
        var output = EmitMain("void Loop() { }");
        Assert.Contains("while (True) {", output);
        Assert.Contains("yield;", output);
    }

    [Fact]
    public void EmitMain_LoopThrowsNotImplemented_NoWhileLoop()
    {
        // Loop() that only throws NotImplementedException is a stub — suppress the while loop.
        var output = EmitMain("void Loop() { throw new System.NotImplementedException(); }");
        Assert.DoesNotContain("while", output);
    }

    [Fact]
    public void EmitMain_LoopThrowsNotImplemented_WithEvents_StillProducesWhileLoop()
    {
        // Even though Loop() is a stub, subscribed events must still be handled inside
        // a while(True) loop — the stub only suppresses the Loop() body, not the loop itself.
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}
            void Loop() {{ throw new System.NotImplementedException(); }}");
        Assert.Contains("while (True) {", output);
        Assert.Contains("yield;", output);
        Assert.Contains("PendingEvents", output);
    }

    [Fact]
    public void EmitMain_NoLoopAttribute_SuppressesWhileLoopEvenWithBody()
    {
        // [NoLoop] fully suppresses the while(True) wrapper, and Loop()'s body is never emitted.
        var output = EmitMain(
            "void Loop() { Foo(); } void Foo() { }",
            classAttributes: "[NoLoop]");
        Assert.DoesNotContain("while", output);
        Assert.DoesNotContain("Foo();", output);
    }

    [Fact]
    public void EmitMain_NoLoopAttribute_SuppressesEventLoopToo()
    {
        // Registered events are also dropped along with the while loop when [NoLoop] is set.
        var output = EmitMain(
            $@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}",
            classAttributes: "[NoLoop]");
        Assert.DoesNotContain("while", output);
        Assert.DoesNotContain("PendingEvents", output);
    }

    // ────────── Main() body ──────────

    [Fact]
    public void EmitMain_MainWithStatement_StatementAppearsBeforeWhileLoop()
    {
        var output = EmitMain(@"
            void Foo() {}
            void Bar() {}
            void Main() { Foo(); }
            void Loop() { Bar(); }");

        var fooIdx = output.IndexOf("Foo();", StringComparison.Ordinal);
        var whileIdx = output.IndexOf("while (True) {", StringComparison.Ordinal);
        var barIdx = output.IndexOf("Bar();", StringComparison.Ordinal);

        Assert.True(fooIdx >= 0, "Foo() should appear in output");
        Assert.True(fooIdx < whileIdx, "Main() body must precede the while loop");
        Assert.True(whileIdx < barIdx, "while loop must precede Loop() body");
    }

    // ────────── Event subscriptions skipped ──────────

    [Fact]
    public void EmitMain_EventSubscriptionInMain_NotEmittedVerbatim()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}");

        Assert.DoesNotContain("+=", output);
    }

    [Fact]
    public void EmitMain_EventSubscriptionWithOtherStatement_OnlyOtherStatementInMainBody()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void Foo() {{}}
            void OnClick(string controlId) {{ }}
            void Main() {{ Foo(); Click += OnClick; }}");

        Assert.Contains("Foo();", output);
        Assert.DoesNotContain("+=", output);
    }

    // ────────── Class-level typed events ──────────

    [Fact]
    public void EmitMain_TypedEvent_MethodGroup_GeneratesForeachAndSwitch()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}");

        Assert.Contains("foreach (Event in PendingEvents) {", output);
        Assert.Contains("switch (Event.Type) {", output);
        Assert.Contains("case CMlScriptEvent::Type::Click: {", output);
    }

    [Fact]
    public void EmitMain_TypedEvent_MethodGroup_CallsHandlerWithEventFields()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}");

        // Param "controlId" → PascalCase → "ControlId"
        Assert.Contains("OnClick(Event.ControlId);", output);
    }

    [Fact]
    public void EmitMain_TypedEvent_LambdaBlockBody_BindsParamsAndInlinesBody()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void Foo() {{}}
            void Main() {{ Click += (controlId) => {{ Foo(); }}; }}");

        Assert.Contains("case CMlScriptEvent::Type::Click: {", output);
        Assert.Contains("declare ControlId = Event.ControlId;", output);
        Assert.Contains("Foo();", output);
    }

    [Fact]
    public void EmitMain_TypedEvent_MultipleParams_AllMappedToEventFields()
    {
        var output = EmitMain(@"
            [ManiaScriptEvent(""PendingEvents"")]
            public delegate void KeyPressEventHandler(int keyCode, string keyName);
            public event KeyPressEventHandler? KeyPress;
            void OnKey(int keyCode, string keyName) { }
            void Main() { KeyPress += OnKey; }");

        Assert.Contains("case CMlScriptEvent::Type::KeyPress: {", output);
        Assert.Contains("OnKey(Event.KeyCode, Event.KeyName);", output);
    }

    // ────────── [MemberName] attribute ──────────

    [Fact]
    public void EmitMain_TypedEvent_MemberNameAttribute_UsesCustomPropertyName()
    {
        var output = EmitMain(@"
            [ManiaScriptEvent(""PendingEvents"")]
            public delegate void NavEventHandler([MemberName(""NavAction"")] string action);
            public event NavEventHandler? Nav;
            void OnNav(string action) { }
            void Main() { Nav += OnNav; }");

        // eventTypeName = ""Nav"" (NavEventHandler → strips EventHandler → Nav)
        Assert.Contains("case CMlScriptEvent::Type::Nav: {", output);
        // MemberName overrides the PascalCase(paramName) derivation
        Assert.Contains("OnNav(Event.NavAction);", output);
    }

    // ────────── General event handler (Pending) ──────────

    [Fact]
    public void EmitMain_GeneralHandler_CallsHandlerWithWholeEvent()
    {
        var output = EmitMain(@"
            [ManiaScriptEvent(""PendingEvents"")]
            public delegate void PendingEventHandler(object e);
            public event PendingEventHandler? Pending;
            void OnEvent(object e) { }
            void Main() { Pending += OnEvent; }");

        Assert.Contains("OnEvent(Event);", output);
    }

    [Fact]
    public void EmitMain_GeneralHandler_AppearsBeforeTypedSwitch()
    {
        var output = EmitMain($@"
            [ManiaScriptEvent(""PendingEvents"")]
            public delegate void PendingEventHandler(object e);
            public event PendingEventHandler? Pending;
            {ClickDelegates}
            void OnEvent(object e) {{ }}
            void OnClick(string controlId) {{ }}
            void Main() {{ Pending += OnEvent; Click += OnClick; }}");

        var generalIdx = output.IndexOf("OnEvent(Event);", StringComparison.Ordinal);
        var switchIdx  = output.IndexOf("switch (Event.Type) {", StringComparison.Ordinal);
        Assert.True(generalIdx >= 0 && generalIdx < switchIdx,
            "General handler must fire before the Event.Type switch");
    }

    // ────────── Control-specific external events ──────────

    [Fact]
    public void EmitMain_ExternalEvent_MethodGroup_GeneratesControlSwitch()
    {
        const string extraCode = @"
class MyControl {
    [ManiaScriptExternalEvent(""Test"", ""PendingEvents"", ""MouseClick"", ""Control"")]
    public event System.Action? Click;
}";
        var output = EmitMain(@"
            MyControl myControl;
            void OnClick() { }
            void Main() { myControl.Click += OnClick; }",
            extraCode);

        Assert.Contains("case CMlScriptEvent::Type::MouseClick: {", output);
        Assert.Contains("switch (Event.Control) {", output);
        Assert.Contains("case MyControl: {", output);
        Assert.Contains("OnClick();", output);
    }

    [Fact]
    public void EmitMain_ExternalEvent_LambdaBlockBody_InlinesBody()
    {
        const string extraCode = @"
class MyControl {
    [ManiaScriptExternalEvent(""Test"", ""PendingEvents"", ""MouseClick"", ""Control"")]
    public event System.Action? Click;
}";
        var output = EmitMain(@"
            MyControl myControl;
            void Foo() {}
            void Main() { myControl.Click += () => { Foo(); }; }",
            extraCode);

        Assert.Contains("case MyControl: {", output);
        Assert.Contains("Foo();", output);
    }

    [Fact]
    public void EmitMain_MultipleExternalEvents_MergedIntoSingleControlSwitch()
    {
        const string extraCode = @"
class MyControl {
    [ManiaScriptExternalEvent(""Test"", ""PendingEvents"", ""MouseClick"", ""Control"")]
    public event System.Action? Click;
}";
        var output = EmitMain(@"
            MyControl ctrl1;
            MyControl ctrl2;
            void OnA() {} void OnB() {}
            void Main() { ctrl1.Click += OnA; ctrl2.Click += OnB; }",
            extraCode);

        // Only one switch(Event.Type) block
        Assert.Equal(1, CountOccurrences(output, "case CMlScriptEvent::Type::MouseClick: {"));
        // Both controls appear in the switch(Event.Control) block
        Assert.Contains("case Ctrl1: {", output);
        Assert.Contains("case Ctrl2: {", output);
    }

    // ────────── Mixed class-level + control-specific ──────────

    [Fact]
    public void EmitMain_MixedSubscriptions_CorrectOrder()
    {
        const string extraCode = @"
class MyControl {
    [ManiaScriptExternalEvent(""Test"", ""PendingEvents"", ""MouseClick"", ""Control"")]
    public event System.Action? Click;
}";
        var output = EmitMain($@"
            MyControl myControl;
            {ClickDelegates}
            void OnAny(string controlId) {{ }}
            void OnSpecific() {{ }}
            void Main() {{ Click += OnAny; myControl.Click += OnSpecific; }}",
            extraCode);

        // Only one foreach
        Assert.Equal(1, CountOccurrences(output, "foreach (Event in PendingEvents) {"));
        // Class-level typed call appears before the control switch (both under same case)
        var anyIdx      = output.IndexOf("OnAny(", StringComparison.Ordinal);
        var controlIdx  = output.IndexOf("switch (Event.Control) {", StringComparison.Ordinal);
        Assert.True(anyIdx < controlIdx, "Class-level typed handler must precede switch(Event.Control)");
    }

    // ────────── return in Loop() → break ──────────

    [Fact]
    public void EmitMain_ReturnInLoop_EmitsContinue()
    {
        var output = EmitMain("void Loop() { return; }");
        Assert.Contains("continue;", output);
        Assert.DoesNotContain("return;", output);
    }

    [Fact]
    public void EmitMain_ReturnInMain_EmitsReturn()
    {
        var output = EmitMain("void Main() { return; }  void Loop() { }");
        // The return; in Main() must stay as return; and not become break;
        Assert.Contains("return;", output);
    }

    // ────────── Manual foreach in Loop() ──────────

    [Fact]
    public void EmitMain_ManualForeachInLoop_EventSwitchInjectedInside()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}
            void Loop() {{
                foreach (var e in PendingEvents) {{ }}
            }}");

        Assert.Contains("foreach (Event in PendingEvents) {", output);
        Assert.Contains("case CMlScriptEvent::Type::Click: {", output);
        // Injection means only one foreach, not two
        Assert.Equal(1, CountOccurrences(output, "foreach (Event in PendingEvents) {"));
    }

    [Fact]
    public void EmitMain_ManualForeachInLoop_UserBodyAppearsBeforeInjectedSwitch()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void Foo() {{}}
            void OnClick(string controlId) {{ }}
            void Main() {{ Click += OnClick; }}
            void Loop() {{
                foreach (var e in PendingEvents) {{ Foo(); }}
            }}");

        var fooIdx    = output.IndexOf("Foo();", StringComparison.Ordinal);
        var switchIdx = output.IndexOf("switch (Event.Type) {", StringComparison.Ordinal);
        Assert.True(fooIdx >= 0 && fooIdx < switchIdx,
            "User body inside the foreach must appear before the injected switch");
    }

    [Fact]
    public void EmitMain_NoManualForeachButHasEvents_AutoGeneratesForeach()
    {
        var output = EmitMain($@"
            {ClickDelegates}
            void OnClick(string controlId) {{ }}
            void Loop() {{ }}
            void Main() {{ Click += OnClick; }}");

        Assert.Contains("foreach (Event in PendingEvents) {", output);
    }
}
