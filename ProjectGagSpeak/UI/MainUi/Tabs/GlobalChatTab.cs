using CkCommons.Gui;
using CkCommons.Helpers;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerClient;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Tutorial;
using GagSpeak.State.Managers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;

namespace GagSpeak.Gui.MainWindow;

// this can easily become the "contact list" tab of the "main UI" window.
public class GlobalChatTab : DisposableMediatorSubscriberBase
{
    private readonly MainHub _hub;
    private readonly GlobalChatLog _globalChat;
    private readonly GlobalPermissions _globals;
    private readonly MufflerService _garbler;
    private readonly GagRestrictionManager _gagManager;
    private readonly MainConfig _mainConfig;
    private readonly KinkPlateService _plateManager;
    private readonly TutorialService _guides;

    public GlobalChatTab(
        ILogger<GlobalChatTab> logger,
        GagspeakMediator mediator,
        MainHub hub,
        GlobalChatLog globalChat,
        GlobalPermissions globals,
        MufflerService garbler,
        GagRestrictionManager gagManager,
        MainConfig mainConfig,
        KinkPlateService plateManager,
        TutorialService guides) : base(logger, mediator)
    {
        _hub = hub;
        _globalChat = globalChat;
        _globals = globals;
        _garbler = garbler;
        _gagManager = gagManager;
        _mainConfig = mainConfig;
        _plateManager = plateManager;
        _guides = guides;
    }

    public void DrawDiscoverySection()
    {
        ImGuiUtil.Center("Global GagSpeak Chat");
        ImGui.Separator();
        DrawGlobalChatlog("GS_MainGlobal");
    }

    private bool shouldFocusChatInput = false;
    private bool showMessagePreview = false;
    private string NextChatMessage = string.Empty;

    public void DrawGlobalChatlog(string windowId)
    {
        using var scrollbar = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 10f);
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();

        // grab the profile object from the profile service.
        var profile = _plateManager.GetKinkPlate(MainHub.PlayerUserData);
        if(profile.KinkPlateInfo.Disabled)
        {
            ImGui.Spacing();
            CkGui.ColorTextCentered("Social Features have been Restricted", ImGuiColors.DalamudRed);
            ImGui.Spacing();
            CkGui.ColorTextCentered("Cannot View Global Chat because of this.", ImGuiColors.DalamudRed);
            return;
        }
        // Calculate the height for the chat log, leaving space for the input text field
        var inputTextHeight = ImGui.GetFrameHeightWithSpacing();
        var chatLogHeight = CurrentRegion.Y - inputTextHeight;

        // Create a child for the chat log
        var region = new Vector2(CurrentRegion.X, chatLogHeight);
        _globalChat.DrawChat(region, showMessagePreview);
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.GlobalChat, ImGui.GetWindowPos(), ImGui.GetWindowSize());

        // Now draw out the input text field
        var nextMessageRef = NextChatMessage;
        // Set keyboard focus to the chat input box if needed
        if (shouldFocusChatInput)
        {
            // if we currently are focusing the window this is present on, set the keyboard focus.
            if(ImGui.IsWindowFocused())
            {
                ImGui.SetKeyboardFocusHere(0);
                shouldFocusChatInput = false;
            }
        }

        // Set width for input box and create it with a hint
        var Icon = _globalChat.DoAutoScroll ? FAI.ArrowDownUpLock : FAI.ArrowDownUpAcrossLine;
        ImGui.SetNextItemWidth(CurrentRegion.X - CkGui.IconButtonSize(Icon).X*2 - ImGui.GetStyle().ItemInnerSpacing.X*2);
        if (ImGui.InputTextWithHint("##ChatInputBox" + windowId, "chat message here...", ref nextMessageRef, 300))
        {
            // Update stored message
            NextChatMessage = nextMessageRef;
        }

        // Check if the input text field is focused and Enter is pressed
        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            shouldFocusChatInput = true;

            // If message is empty, return
            if (string.IsNullOrWhiteSpace(NextChatMessage))
                return;

            // Process message if gagged
            if ((_gagManager.ServerGagData?.IsGagged() ?? true) && (_globals.Current?.ChatGarblerActive ?? false))
                NextChatMessage = _garbler.ProcessMessage(NextChatMessage);

            // Send message to the server
            Logger.LogTrace($"Sending Message: {NextChatMessage}");
            _hub.UserSendGlobalChat(new(MainHub.PlayerUserData, NextChatMessage, _mainConfig.Current.PreferThreeCharaAnonName)).ConfigureAwait(false);

            // Clear message and trigger achievement event
            NextChatMessage = string.Empty;
            GagspeakEventManager.AchievementEvent(UnlocksEvent.GlobalSent);
        }

        // Update preview display based on input field activity
        showMessagePreview = ImGui.IsItemActive();

        // Toggle AutoScroll functionality
        ImUtf8.SameLineInner();
        if (CkGui.IconButton(Icon))
            _globalChat.SetAutoScroll(!_globalChat.DoAutoScroll);
        CkGui.AttachToolTip($"Toggles AutoScroll (Current: {(_globalChat.DoAutoScroll ? "Enabled" : "Disabled")})");

        // draw the popout button
        ImUtf8.SameLineInner();
        if (CkGui.IconButton(FAI.Expand, disabled: !KeyMonitor.ShiftPressed()))
            Mediator.Publish(new UiToggleMessage(typeof(GlobalChatPopoutUI)));
        CkGui.AttachToolTip("Open the Global Chat in a Popout Window--SEP--Hold SHIFT to activate!");
    }
}

