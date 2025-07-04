using CkCommons.Gui;
using CkCommons.Widgets;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace GagSpeak.Gui.Components;

public class PublicationTabs : IconTabBar<PublicationTabs.SelectedTab>
{
    public enum SelectedTab
    {
        Patterns,
        Moodles,
    }

    public PublicationTabs()
    {
        AddDrawButton(FontAwesomeIcon.CommentDots, SelectedTab.Patterns, "Pattern Publisher");
        AddDrawButton(FontAwesomeIcon.CommentDots, SelectedTab.Moodles, "Moodle Publisher");
    }

    public override void Draw(float availableWidth)
    {
        if (_tabButtons.Count == 0)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0)));
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * (_tabButtons.Count - 1))) / _tabButtons.Count;
        var buttonY = CkGui.IconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);
        var drawList = ImGui.GetWindowDrawList();

        ImGuiHelpers.ScaledDummy(spacing.Y / 2f);

        foreach (var tab in _tabButtons)
            DrawTabButton(tab, buttonSize, spacing, drawList);

        // advance to the new line and dispose of the button color.
        ImGui.NewLine();
        color.Dispose();

        ImGuiHelpers.ScaledDummy(3f);
        ImGui.Separator();
    }

}
