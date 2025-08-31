using CkCommons;
using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Widgets;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.FileSystems;
using GagSpeak.Gui.Components;
using GagSpeak.Kinksters;
using GagSpeak.PlayerClient;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.Services.Tutorial;
using GagSpeak.State.Managers;
using GagSpeak.State.Models;
using GagspeakAPI.Attributes;
using Dalamud.Bindings.ImGui;
using OtterGui.Extensions;
using OtterGui.Text;

namespace GagSpeak.Gui.Wardrobe;
public partial class RestrictionsPanel : DisposableMediatorSubscriberBase
{
    private readonly RestrictionFileSelector _selector;
    private readonly ActiveItemsDrawer _activeItemDrawer;
    private readonly EquipmentDrawer _equipDrawer;
    private readonly ModPresetDrawer _modDrawer;
    private readonly MoodleDrawer _moodleDrawer;
    private readonly AttributeDrawer _attributeDrawer;
    private readonly RestrictionManager _manager;
    private readonly UiThumbnailService _thumbnails;
    private readonly TutorialService _guides;
    public bool IsEditing => _manager.ItemInEditor != null;
    public RestrictionsPanel(
        ILogger<RestrictionsPanel> logger,
        GagspeakMediator mediator,
        RestrictionFileSelector selector,
        ActiveItemsDrawer activeItemDrawer,
        EquipmentDrawer equipDrawer,
        ModPresetDrawer modDrawer,
        MoodleDrawer moodleDrawer,
        AttributeDrawer traitsDrawer,
        RestrictionManager manager,
        HypnoEffectManager effectPresets,
        KinksterManager pairs,
        UiThumbnailService thumbnails,
        TutorialService guides) : base(logger, mediator)
    {
        _selector = selector;
        _thumbnails = thumbnails;
        _attributeDrawer = traitsDrawer;
        _equipDrawer = equipDrawer;
        _modDrawer = modDrawer;
        _moodleDrawer = moodleDrawer;
        _activeItemDrawer = activeItemDrawer;
        _manager = manager;
        _guides = guides;

        _hypnoEditor = new HypnoEffectEditor("RestrictionEditor", effectPresets);

        Mediator.Subscribe<ThumbnailImageSelected>(this, (msg) =>
        {
            if (msg.Folder is ImageDataType.Restrictions)
            {
                if (manager.Storage.TryGetRestriction(msg.SourceId, out var match))
                {
                    Logger.LogDebug($"Thumbnail updated for {match.Label} to {msg.FileName}");
                    manager.UpdateThumbnail(match, msg.FileName);
                }
            }
            else if (msg.Folder is ImageDataType.Blindfolds && manager.ItemInEditor is BlindfoldRestriction blindfold)
            {
                Logger.LogDebug($"Thumbnail updated for {blindfold.Label} to {blindfold.Properties.OverlayPath}");
                blindfold.Properties.OverlayPath = msg.FileName;
            }
            else if (msg.Folder is ImageDataType.Hypnosis && manager.ItemInEditor is HypnoticRestriction hypnoItem)
            {
                Logger.LogDebug($"Thumbnail updated for {hypnoItem.Label} to {hypnoItem.Properties.OverlayPath}");
                hypnoItem.Properties.OverlayPath = msg.FileName;
            }
        });
    }

    private HypnoEffectEditor _hypnoEditor;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _hypnoEditor.Dispose();
    }

    public void DrawContents(CkHeader.QuadDrawRegions drawRegions, float curveSize, WardrobeTabs tabMenu)
    {
        ImGui.SetCursorScreenPos(drawRegions.TopLeft.Pos);
        using (ImRaii.Child("RestrictionsTopLeft", drawRegions.TopLeft.Size))
            _selector.DrawFilterRow(drawRegions.TopLeft.SizeX);

        ImGui.SetCursorScreenPos(drawRegions.BotLeft.Pos);
        using (ImRaii.Child("RestrictionsBottomLeft", drawRegions.BotLeft.Size, false, WFlags.NoScrollbar))
            _selector.DrawList(drawRegions.BotLeft.SizeX);

        ImGui.SetCursorScreenPos(drawRegions.TopRight.Pos);
        using (ImRaii.Child("RestrictionsTopRight", drawRegions.TopRight.Size))
            tabMenu.Draw(drawRegions.TopRight.Size);

        // Draw the selected Item
        ImGui.SetCursorScreenPos(drawRegions.BotRight.Pos);
        DrawSelectedItemInfo(drawRegions.BotRight, curveSize);
        var lineTopLeft = ImGui.GetItemRectMin() - new Vector2(ImGui.GetStyle().WindowPadding.X, 0);
        var lineBotRight = lineTopLeft + new Vector2(ImGui.GetStyle().WindowPadding.X, ImGui.GetItemRectSize().Y);
        ImGui.GetWindowDrawList().AddRectFilled(lineTopLeft, lineBotRight, CkGui.Color(ImGuiColors.DalamudGrey));

        // Shift down and draw the Active items
        var verticalShift = new Vector2(0, ImGui.GetItemRectSize().Y + ImGui.GetStyle().WindowPadding.Y * 3);
        ImGui.SetCursorScreenPos(drawRegions.BotRight.Pos + verticalShift);
        DrawActiveItemInfo(drawRegions.BotRight.Size - verticalShift);
    }

    public void DrawEditorContents(CkHeader.QuadDrawRegions drawRegions, float curveSize)
    {
        ImGui.SetCursorScreenPos(drawRegions.TopLeft.Pos);
        using (ImRaii.Child("RestrictionsTopLeft", drawRegions.TopLeft.Size))
            DrawEditorHeaderLeft(drawRegions.TopLeft.SizeX);

        ImGui.SetCursorScreenPos(drawRegions.BotLeft.Pos);
        using (ImRaii.Child("RestrictionsBottomLeft", drawRegions.BotLeft.Size, false, WFlags.NoScrollbar))
            DrawEditorLeft(drawRegions.BotLeft.SizeX);

        ImGui.SetCursorScreenPos(drawRegions.TopRight.Pos);
        using (ImRaii.Child("RestrictionsTopRight", drawRegions.TopRight.Size))
            DrawEditorHeaderRight(drawRegions.TopRight.Size);

        ImGui.SetCursorScreenPos(drawRegions.BotRight.Pos);
        using (ImRaii.Child("RestrictionsBottomRight", drawRegions.BotRight.Size))
            DrawEditorRight(drawRegions.BotRight.SizeX);
    }


    private void DrawSelectedItemInfo(CkHeader.DrawRegion drawRegion, float rounding)
    {
        var wdl = ImGui.GetWindowDrawList();
        var height = ImGui.GetFrameHeightWithSpacing() + MoodleDrawer.IconSize.Y;
        var region = new Vector2(drawRegion.Size.X, height);
        var notSelected = _selector.Selected is null;
        var isActive = !notSelected && _manager.IsItemApplied(_selector.Selected!.Identifier);
        var tooltip = notSelected ? "No item selected!" : isActive ? "Item is Active!" : "Double Click to begin editing!";

        using var c = CkRaii.ChildLabelCustomButton("SelItem", region, ImGui.GetFrameHeight(), LabelButton, BeginEdits, tooltip, DFlags.RoundCornersRight, LabelFlags.AddPaddingToHeight);

        var pos = ImGui.GetItemRectMin();
        var imgSize = new Vector2(c.InnerRegion.Y);
        var imgDrawPos = pos with { X = pos.X + c.InnerRegion.X - imgSize.X };
        // Draw the left items.
        if (_selector.Selected is not null)
            DrawSelectedInner(imgSize.X, isActive);
        
        // Draw the right image item.
        ImGui.GetWindowDrawList().AddRectFilled(imgDrawPos, imgDrawPos + imgSize, CkColor.FancyHeaderContrast.Uint(), rounding);
        ImGui.SetCursorScreenPos(imgDrawPos);
        if (_selector.Selected is not null)
        {
            _activeItemDrawer.DrawRestrictionImage(_selector.Selected!, imgSize.Y, rounding, false);
            if (!isActive && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                _thumbnails.SetThumbnailSource(_selector.Selected!.Identifier, new Vector2(120), ImageDataType.Restrictions);
            CkGui.AttachToolTip("The Thumbnail for this item.--SEP--Double Click to change the image.");
        }

        void LabelButton()
        {
            using (var c = CkRaii.Child("##RestrictionSelectorLabel", new Vector2(region.X * .6f, ImGui.GetFrameHeight())))
            {
                var imgSize = new Vector2(c.InnerRegion.Y);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().WindowPadding.X);
                ImUtf8.TextFrameAligned(_selector.Selected?.Label ?? "No Item Selected!");
                ImGui.SameLine(c.InnerRegion.X - imgSize.X * 1.5f);
                if (_selector.Selected is not null)
                {
                    (var image, var tooltip) = _selector.Selected?.Type switch
                    {
                        RestrictionType.Hypnotic => (CosmeticService.CoreTextures.Cache[CoreTexture.HypnoSpiral], "This is a Hypnotic Restriction!"),
                        RestrictionType.Blindfold => (CosmeticService.CoreTextures.Cache[CoreTexture.Blindfolded], "This is a Blindfold Restriction!"),
                        _ => (CosmeticService.CoreTextures.Cache[CoreTexture.Restrained], "This is a generic Restriction.")
                    };
                    ImGui.GetWindowDrawList().AddDalamudImage(image, ImGui.GetCursorScreenPos(), imgSize, tooltip);
                }
            }
        }

        void BeginEdits(ImGuiMouseButton b)
        {
            if (b is ImGuiMouseButton.Left && !notSelected && !isActive)
                _manager.StartEditing(_selector.Selected!);
        }
    }

    private void DrawSelectedInner(float rightOffset, bool isActive)
    {
        using var innerGroup = ImRaii.Group();

        using (CkRaii.Group(CkColor.FancyHeaderContrast.Uint()))
        {
            CkGui.BooleanToColoredIcon(_selector.Selected!.IsEnabled, false);
            CkGui.TextFrameAlignedInline($"Visuals  ");
        }
        if (!isActive && ImGui.IsItemHovered() && ImGui.IsItemClicked())
            _manager.ToggleVisibility(_selector.Selected!.Identifier);
        CkGui.AttachToolTip($"Visuals {(_selector.Selected!.IsEnabled ? "will" : "will not")} be applied.");

        // Next row we need to draw the Glamour Icon, Mod Icon, and hardcore Traits.
        if (ItemSvc.NothingItem(_selector.Selected!.Glamour.Slot).Id != _selector.Selected!.Glamour.GameItem.Id)
        {
            ImUtf8.SameLineInner();
            CkGui.FramedIconText(FAI.Vest);
            CkGui.AttachToolTip($"A --COL--{_selector.Selected!.Glamour.GameItem.Name}--COL-- is attached to the " +
                $"--COL--{_selector.Selected!.Label}--COL--.", color: ImGuiColors.ParsedGold);
        }
        if (_selector.Selected!.Mod.HasData)
        {
            ImUtf8.SameLineInner();
            CkGui.FramedIconText(FAI.FileDownload);
            CkGui.AttachToolTip($"Mod Preset ({_selector.Selected.Mod.Label}) is applied." +
                $"--SEP--Source Mod: {_selector.Selected!.Mod.Container.ModName}");
        }
        if (_selector.Selected!.Traits > 0)
        {
            ImUtf8.SameLineInner();
            _attributeDrawer.DrawTraitPreview(_selector.Selected!.Traits);
        }
        _moodleDrawer.ShowStatusIcons(_selector.Selected!.Moodle, ImGui.GetContentRegionAvail().X);
    }

    private void DrawActiveItemInfo(Vector2 region)
    {
        using var child = CkRaii.Child("ActiveItems", region, wFlags: WFlags.NoScrollbar | WFlags.AlwaysUseWindowPadding);

        if (_manager.ServerRestrictionData is not { } activeSlots)
            return;

        var height = ImGui.GetContentRegionAvail().Y;
        var groupH = ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y;
        var groupSpacing = (height - 5 * groupH) / 7;

        for (var index = 0; index < activeSlots.Restrictions.Length; index++)
        {
            // Spacing.
            if(index > 0) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + groupSpacing);

            if (activeSlots.Restrictions[index].Identifier == Guid.Empty)
            {
                _activeItemDrawer.ApplyItemGroup(index, activeSlots.Restrictions[index]);
                continue;
            }

            // Otherwise, if the item is sucessfully applied, display the locked states, based on what is active.
            if (_manager.ActiveItems.TryGetValue(activeSlots.Restrictions[index].Identifier, out var item))
            {
                // If the padlock is currently locked, show the 'Unlocking' group.
                if (activeSlots.Restrictions[index].IsLocked())
                    _activeItemDrawer.UnlockItemGroup(index, activeSlots.Restrictions[index], item);
                // Otherwise, show the 'Locking' group. Locking group can still change applied items.
                else
                    _activeItemDrawer.LockItemGroup(index, activeSlots.Restrictions[index], item);
            }
        }
    }
}
