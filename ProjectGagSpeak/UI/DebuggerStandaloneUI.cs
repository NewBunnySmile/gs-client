using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI;

public class DebuggerStandaloneUI : WindowMediatorSubscriberBase
{
    private readonly DebuggerBinds _bindsDebugger;
    public DebuggerStandaloneUI(ILogger<GlobalChatPopoutUI> logger, GagspeakMediator mediator,
        DebuggerBinds bindDebug) : base(logger, mediator, "Debugger for Bindings")
    {
        _bindsDebugger = bindDebug;

        IsOpen = false;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(380, 500),
            MaximumSize = new Vector2(1000, 2000),
        };
    }

    protected override void PreDrawInternal() { }

    protected override void PostDrawInternal() { }

    protected override void DrawInternal()
    {
        _bindsDebugger.DrawRestrictionStorage();

        ImGui.Separator();

        _bindsDebugger.DrawRestraintStorage();
    }
}
