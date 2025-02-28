using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Common.Lua;
using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.Localization;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.Configs;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using ImGuiNET;
using OtterGui.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FAI = Dalamud.Interface.FontAwesomeIcon;


namespace GagSpeak.UI;

/// <summary> 
/// The shared service for UI elements within our plugin. 
/// <para>
/// This function should be expected to take advantage 
/// of classes with common functionality, preventing copy pasting.
/// </para>
/// Think of it as a collection of helpers for all functions.
/// </summary>
public partial class UiSharedService
{
    public static readonly ImGuiWindowFlags PopupWindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    public const string TooltipSeparator = "--SEP--";
    public const string ColorToggleSeparator = "--COL--";
    private const string _nicknameEnd = "##GAGSPEAK_USER_NICKNAME_END##";
    private const string _nicknameStart = "##GAGSPEAK_USER_NICKNAME_START##";

    private readonly ILogger<UiSharedService> _logger;
    private readonly MainHub _hub;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly UiFontService _fonts;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly IpcManager _ipcManager;
    private readonly IDalamudPluginInterface _pi;
    private readonly ITextureProvider _textureProvider;

    public Dictionary<string, object> _selectedComboItems;    // the selected combo items
    public Dictionary<string, string> SearchStrings;

    public UiSharedService(ILogger<UiSharedService> logger, MainHub hub,
        ServerConfigurationManager serverConfigs,
        UiFontService fonts, OnFrameworkService frameworkUtil, IpcManager ipcManager, 
        IDalamudPluginInterface pi, ITextureProvider textureProvider)
    {
        _logger = logger;
        _hub = hub;
        _serverConfigs = serverConfigs;
        _fonts = fonts;
        _frameworkUtil = frameworkUtil;
        _ipcManager = ipcManager;
        _pi = pi;
        _textureProvider = textureProvider;

        _selectedComboItems = new(StringComparer.Ordinal);
        SearchStrings = new(StringComparer.Ordinal);
    }
    public Vector2 LastMainUIWindowPosition { get; set; } = Vector2.Zero;
    public Vector2 LastMainUIWindowSize { get; set; } = Vector2.Zero;

    public IFontHandle GameFont => _fonts.GameFont;
    public IFontHandle IconFont => _fonts.IconFont;
    public IFontHandle UidFont => _fonts.UidFont;
    public IFontHandle GagspeakFont => _fonts.GagspeakFont;
    public IFontHandle GagspeakLabelFont => _fonts.GagspeakLabelFont;
    public IFontHandle GagspeakTitleFont => _fonts.GagspeakTitleFont;
    public IDalamudTextureWrap GetImageFromDirectoryFile(string path)
        => _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Assets", path)).GetWrapOrEmpty();

    public IDalamudTextureWrap GetGameStatusIcon(uint IconId)
        => _textureProvider.GetFromGameIcon(new GameIconLookup(IconId)).GetWrapOrEmpty();

    /// <summary> 
    /// A helper function to attach a tooltip to a section in the UI currently hovered. 
    /// </summary>
    public static void AttachToolTip(string text, float borderSize = 1f, Vector4? color = null)
    {
        if (text.IsNullOrWhitespace()) return;

        // if the item is currently hovered, with the ImGuiHoveredFlags set to allow when disabled
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.One * 8f);
            using var rounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 4f);
            using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, borderSize);
            using var frameColor = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
            // begin the tooltip interface
            ImGui.BeginTooltip();
            // push the text wrap position to the font size times 35
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            // we will then check to see if the text contains a tooltip
            if (text.Contains(TooltipSeparator, StringComparison.Ordinal))
            {
                // if it does, we will split the text by the tooltip
                var splitText = text.Split(TooltipSeparator, StringSplitOptions.None);
                // for each of the split text, we will display the text unformatted
                for (var i = 0; i < splitText.Length; i++)
                {
                    if (splitText[i].Contains(ColorToggleSeparator, StringComparison.Ordinal) && color.HasValue)
                    {
                        var colorSplitText = splitText[i].Split(ColorToggleSeparator, StringSplitOptions.None);
                        var useColor = false;

                        for (var j = 0; j < colorSplitText.Length; j++)
                        {
                            if (useColor)
                            {
                                ImGui.SameLine(0, 0); // Prevent new line
                                ImGui.TextColored(color.Value, colorSplitText[j]);
                            }
                            else
                            {
                                if (j > 0) ImGui.SameLine(0, 0); // Prevent new line
                                ImGui.TextUnformatted(colorSplitText[j]);
                            }
                            // Toggle the color for the next segment
                            useColor = !useColor;
                        }
                    }
                    else
                    {
                        ImGui.TextUnformatted(splitText[i]);
                    }
                    if (i != splitText.Length - 1) ImGui.Separator();
                }
            }
            // otherwise, if it contains no tooltip, then we will display the text unformatted
            else
            {
                ImGui.TextUnformatted(text);
            }
            // finally, pop the text wrap position and end the tooltip
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    /// <summary>
    /// A helper function for centering the next displayed window.
    /// </summary>
    /// <param name="width"> The width of the window. </param>
    /// <param name="height"> The height of the window. </param>
    /// <param name="cond"> The condition for the ImGuiWindow to be displayed . </param>
    public static void CenterNextWindow(float width, float height, ImGuiCond cond = ImGuiCond.None)
    {
        // get the center of the main viewport
        var center = ImGui.GetMainViewport().GetCenter();
        // then set the next window position to the center minus half the width and height
        ImGui.SetNextWindowPos(new Vector2(center.X - width / 2, center.Y - height / 2), cond);
    }

    /// <summary>
    /// A helper function for retrieving the proper color value given RGBA.
    /// </summary>
    /// <returns> The color formatted as a uint </returns>
    public static uint Color(byte r, byte g, byte b, byte a)
    { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

    /// <summary>
    /// A helper function for retrieving the proper color value given a vector4.
    /// </summary>
    /// <returns> The color formatted as a uint </returns>
    public static uint Color(Vector4 color)
    {
        uint ret = (byte)(color.W * 255);
        ret <<= 8;
        ret += (byte)(color.Z * 255);
        ret <<= 8;
        ret += (byte)(color.Y * 255);
        ret <<= 8;
        ret += (byte)(color.X * 255);
        return ret;
    }

    /// <summary>
    /// A helper function for displaying colortext. Keep in mind that this already exists in ottergui and we likely dont need it.
    /// </summary>
    public static void ColorText(string text, Vector4 color)
    {
        using var raiicolor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
    }

    /// <summary> Displays colored text based on the boolean value of true or false. </summary>
    public static void ColorTextBool(string text, bool value)
    {
        var color = value ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed;
        ColorText(text, color);
    }

    /// <summary> Displays colored text based on the boolean value of true or false. </summary>
    /// <remarks> Can provide custom colors if desired. </remarks>
    public static void ColorTextBool(string text, bool value, Vector4 colorTrue = default, Vector4 colorFalse = default)
    {
        var color = value
            ? (colorTrue == default) ? ImGuiColors.HealerGreen : colorTrue
            : (colorFalse == default) ? ImGuiColors.DalamudRed : colorFalse;

        ColorText(text, color);
    }

    public static void ColorTextCentered(string text, Vector4 color)
    {
        var offset = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) / 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        ColorText(text, color);
    }

    /// <summary>
    /// A helper function for wrapped text that is colored.  Keep in mind that this already exists in ottergui and we likely dont need it.
    /// </summary>
    public static void ColorTextWrapped(string text, Vector4 color)
    {
        using var raiicolor = ImRaii.PushColor(ImGuiCol.Text, color);
        TextWrapped(text);
    }

    /// <summary>
    /// Helper function to draw the outlined font in ImGui.
    /// Im not actually sure if this is in ottergui or not.
    /// </summary>
    public static void DrawOutlinedFont(string text, Vector4 fontColor, Vector4 outlineColor, int thickness)
    {
        var original = ImGui.GetCursorPos();

        using (ImRaii.PushColor(ImGuiCol.Text, outlineColor))
        {
            ImGui.SetCursorPos(original with { Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness, Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness, Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness, Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness, Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, fontColor))
        {
            ImGui.SetCursorPos(original);
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original);
            ImGui.TextUnformatted(text);
        }
    }

    public static void DrawOutlinedFont(ImDrawListPtr drawList, string text, Vector2 textPos, uint fontColor, uint outlineColor, int thickness)
    {
        drawList.AddText(textPos with { Y = textPos.Y - thickness },
            outlineColor, text);
        drawList.AddText(textPos with { X = textPos.X - thickness },
            outlineColor, text);
        drawList.AddText(textPos with { Y = textPos.Y + thickness },
            outlineColor, text);
        drawList.AddText(textPos with { X = textPos.X + thickness },
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y - thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y + thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y + thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y - thickness),
            outlineColor, text);

        drawList.AddText(textPos, fontColor, text);
        drawList.AddText(textPos, fontColor, text);
    }

    public static Vector4 GetBoolColor(bool input) => input ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;

    public Vector2 GetGlobalHelperScaleSize(Vector2 size) => size * ImGuiHelpers.GlobalScale;

    public float GetFontScalerFloat() => ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f);

    public float GetButtonSize(string text)
    {
        var vector2 = ImGui.CalcTextSize(text);
        return vector2.X + ImGui.GetStyle().FramePadding.X * 2f;
    }

    public float GetIconTextButtonSize(FAI icon, string text)
    {
        Vector2 vector;
        using (_fonts.IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());

        var vector2 = ImGui.CalcTextSize(text);
        var num = 3f * ImGuiHelpers.GlobalScale;
        return vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num;
    }

    public Vector2 CalcFontTextSize(string text, IFontHandle fontHandle = null!)
    {
        if (fontHandle is null)
            return ImGui.CalcTextSize(text);

        using (fontHandle.Push())
            return ImGui.CalcTextSize(text);
    }

    /// <summary>
    /// Helper function for retrieving the nickname of a clients paired users.
    /// </summary>
    /// <param name="pairs"> The list of pairs the client has. </param>
    /// <returns> The string of nicknames for the pairs. </returns>
    public static string GetNicknames(List<Pair> pairs)
    {
        StringBuilder sb = new();
        sb.AppendLine(_nicknameStart);
        foreach (var entry in pairs)
        {
            var note = entry.GetNickname();
            if (note.IsNullOrEmpty()) continue;

            sb.Append(entry.UserData.UID).Append(":\"").Append(entry.GetNickname()).AppendLine("\"");
        }
        sb.AppendLine(_nicknameEnd);

        return sb.ToString();
    }

    public static float GetWindowContentRegionWidth() => ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

    public bool DrawScaledCenterButtonImage(string ID, Vector2 buttonSize, Vector4 buttonColor,
        Vector2 imageSize, IDalamudTextureWrap image)
    {
        // push ID for the function
        ImGui.PushID(ID);
        // grab the current cursor position
        var InitialPos = ImGui.GetCursorPos();
        // calculate the difference in height between the button and the image
        var heightDiff = buttonSize.Y - imageSize.Y;
        // draw out the button centered
        if (UtilsExtensions.CenteredLineWidths.TryGetValue(ID, out var dims))
        {
            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - dims / 2);
        }
        var oldCur = ImGui.GetCursorPosX();
        var result = ImGui.Button(string.Empty, buttonSize);
        //_logger.LogTrace("Result of button: {result}", result);
        ImGui.SameLine(0, 0);
        UtilsExtensions.CenteredLineWidths[ID] = ImGui.GetCursorPosX() - oldCur;
        ImGui.Dummy(Vector2.Zero);
        // now go back up to the inital position, then step down by the height difference/2
        ImGui.SetCursorPosY(InitialPos.Y + heightDiff / 2);
        UtilsExtensions.ImGuiLineCentered($"###CenterImage{ID}", () =>
        {
            ImGui.Image(image.ImGuiHandle, imageSize, Vector2.Zero, Vector2.One, buttonColor);
        });
        ImGui.PopID();
        // return the result
        return result;
    }

    public static void DrawGrouped(Action imguiDrawAction, float? width = null, float height = 0, float rounding = 5f, Vector4 color = default)
    {
        var cursorPos = ImGui.GetCursorPos();
        using (ImRaii.Group())
        {
            if (width != null)
            {
                ImGuiHelpers.ScaledDummy(width.Value, height);
                ImGui.SetCursorPos(cursorPos);
            }
            imguiDrawAction.Invoke();
        }

        ImGui.GetWindowDrawList().AddRect(
            ImGui.GetItemRectMin() - ImGui.GetStyle().ItemInnerSpacing,
            ImGui.GetItemRectMax() + ImGui.GetStyle().ItemInnerSpacing,
            Color(color), rounding);
    }


    /// <summary> The additional param for an ID is optional. if not provided, the id will be the text. </summary>
    public bool IconButton(FAI icon, float? height = null, string? id = null, bool disabled = false, bool inPopup = false)
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
        var num = 0;
        if (inPopup)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 1.0f, 1.0f, 0.0f));
            num++;
        }

        var text = icon.ToIconString();

        ImGui.PushID((id == null) ? icon.ToIconString() : id + icon.ToIconString());
        Vector2 vector;
        using (_fonts.IconFont.Push())
            vector = ImGui.CalcTextSize(text);
        var windowDrawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var x = vector.X + ImGui.GetStyle().FramePadding.X * 2f;
        var frameHeight = height ?? ImGui.GetFrameHeight();
        var result = ImGui.Button(string.Empty, new Vector2(x, frameHeight));
        var pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X,
            cursorScreenPos.Y + (height ?? ImGui.GetFrameHeight()) / 2f - (vector.Y / 2f));
        using (_fonts.IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.PopID();

        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }
        return result && !disabled;
    }

    private bool IconTextButtonInternal(FAI icon, string text, Vector4? defaultColor = null, float? width = null, bool disabled = false, string id = "")
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
        var num = 0;
        if (defaultColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, defaultColor.Value);
            num++;
        }

        ImGui.PushID(text + "##" + id);
        Vector2 vector;
        using (_fonts.IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());
        var vector2 = ImGui.CalcTextSize(text);
        var windowDrawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var num2 = 3f * ImGuiHelpers.GlobalScale;
        var x = width ?? vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num2;
        var frameHeight = ImGui.GetFrameHeight();
        var result = ImGui.Button(string.Empty, new Vector2(x, frameHeight));
        var pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        using (_fonts.IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), icon.ToIconString());
        var pos2 = new Vector2(pos.X + vector.X + num2, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        windowDrawList.AddText(pos2, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.PopID();
        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }
        dis.Pop();

        return result && !disabled;
    }

    public bool IconTextButton(FAI icon, string text, float? width = null, bool isInPopup = false, bool disabled = false, string id = "Identifier")
    {
        return IconTextButtonInternal(icon, text,
            isInPopup ? new Vector4(1.0f, 1.0f, 1.0f, 0.0f) : null,
            width <= 0 ? null : width,
            disabled, id);
    }

    private bool IconSliderFloatInternal(string id, FAI icon, string label, ref float valueRef, float min,
        float max, Vector4? defaultColor = null, float? width = null, bool disabled = false, string format = "%.1f")
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
        var num = 0;
        // Disable if issues, tends to be culpret
        if (defaultColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, defaultColor.Value);
            num++;
        }

        ImGui.PushID(id);
        Vector2 vector;
        using (_fonts.IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());
        var vector2 = ImGui.CalcTextSize(label);
        var windowDrawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var num2 = 3f * ImGuiHelpers.GlobalScale;
        var x = width ?? vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num2;
        var frameHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorPosX(vector.X + ImGui.GetStyle().FramePadding.X * 2f);
        ImGui.SetNextItemWidth(x - vector.X - num2 * 4); // idk why this works, it probably doesnt on different scaling. Idfk. Look into later.
        var result = ImGui.SliderFloat(label + "##" + id, ref valueRef, min, max, format);

        var pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        using (_fonts.IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), icon.ToIconString());
        ImGui.PopID();
        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }
        dis.Pop();

        return result && !disabled;
    }

    public bool IconSliderFloat(string id, FAI icon, string label, ref float valueRef,
        float min, float max, float? width = null, bool isInPopup = false, bool disabled = false)
    {
        return IconSliderFloatInternal(id, icon, label, ref valueRef, min, max,
            isInPopup ? new Vector4(1.0f, 1.0f, 1.0f, 0.1f) : null,
            width <= 0 ? null : width,
            disabled);
    }

    private bool IconInputTextInternal(string id, FAI icon, string label, string hint, ref string inputStr,
        uint maxLength, Vector4? defaultColor = null, float? width = null, bool disabled = false)
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
        var num = 0;
        // Disable if issues, tends to be culpret
        if (defaultColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, defaultColor.Value);
            num++;
        }

        ImGui.PushID(id);
        Vector2 vector;
        using (_fonts.IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());
        var vector2 = ImGui.CalcTextSize(label);
        var windowDrawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var num2 = 3f * ImGuiHelpers.GlobalScale;
        var x = width ?? vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num2;
        var frameHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorPosX(vector.X + ImGui.GetStyle().FramePadding.X * 2f);
        ImGui.SetNextItemWidth(x - vector.X - num2 * 4); // idk why this works, it probably doesnt on different scaling. Idfk. Look into later.
        var result = ImGui.InputTextWithHint(label, hint, ref inputStr, maxLength, ImGuiInputTextFlags.EnterReturnsTrue);

        var pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        using (_fonts.IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), icon.ToIconString());
        ImGui.PopID();
        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }
        dis.Pop();

        return result && !disabled;
    }

    public bool IconInputText(string id, FAI icon, string label, string hint, ref string inputStr,
        uint maxLength, float? width = null, bool isInPopup = false, bool disabled = false)
    {
        return IconInputTextInternal(id, icon, label, hint, ref inputStr, maxLength,
            isInPopup ? new Vector4(1.0f, 1.0f, 1.0f, 0.1f) : null,
            width <= 0 ? null : width,
            disabled);
    }

    public static void SetScaledWindowSize(float width, bool centerWindow = true)
    {
        var newLineHeight = ImGui.GetCursorPosY();
        ImGui.NewLine();
        newLineHeight = ImGui.GetCursorPosY() - newLineHeight;
        var y = ImGui.GetCursorPos().Y + ImGui.GetWindowContentRegionMin().Y - newLineHeight * 2 - ImGui.GetStyle().ItemSpacing.Y;

        SetScaledWindowSize(width, y, centerWindow, scaledHeight: true);
    }

    public static void SetScaledWindowSize(float width, float height, bool centerWindow = true, bool scaledHeight = false)
    {
        ImGui.SameLine();
        var x = width * ImGuiHelpers.GlobalScale;
        var y = scaledHeight ? height : height * ImGuiHelpers.GlobalScale;

        if (centerWindow)
        {
            CenterWindow(x, y);
        }

        ImGui.SetWindowSize(new Vector2(x, y));
    }

    public static void CopyableDisplayText(string text, string tooltip = "Click to copy")
    {
        // then when the item is clicked, copy it to clipboard so we can share with others
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(text);
        }
        UiSharedService.AttachToolTip(tooltip);
    }


    public static void TextWrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public static Vector4 UploadColor((long, long) data) => data.Item1 == 0 ? ImGuiColors.DalamudGrey :
        data.Item1 == data.Item2 ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudYellow;

    public bool ApplyNicknamesFromClipboard(string notes, bool overwrite)
    {
        var splitNicknames = notes.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
        var splitNicknamesStart = splitNicknames.FirstOrDefault();
        var splitNicknamesEnd = splitNicknames.LastOrDefault();
        if (!string.Equals(splitNicknamesStart, _nicknameStart, StringComparison.Ordinal) || !string.Equals(splitNicknamesEnd, _nicknameEnd, StringComparison.Ordinal))
        {
            return false;
        }

        splitNicknames.RemoveAll(n => string.Equals(n, _nicknameStart, StringComparison.Ordinal) || string.Equals(n, _nicknameEnd, StringComparison.Ordinal));

        foreach (var note in splitNicknames)
        {
            try
            {
                var splittedEntry = note.Split(":", 2, StringSplitOptions.RemoveEmptyEntries);
                var uid = splittedEntry[0];
                var comment = splittedEntry[1].Trim('"');
                if (_serverConfigs.GetNicknameForUid(uid) != null && !overwrite) continue;
                _serverConfigs.SetNicknameForUid(uid, comment);
            }
            catch
            {
                _logger.LogWarning("Could not parse {note}", note);
            }
        }

        _serverConfigs.SaveNicknames();

        return true;
    }

    public void GagspeakText(string text, Vector4? color = null)
        => FontText(text, _fonts.GagspeakFont, color);

    public void GagspeakBigText(string text, Vector4? color = null)
        => FontText(text, _fonts.GagspeakLabelFont, color);

    public void GagspeakTitleText(string text, Vector4? color = null)
        => FontText(text, _fonts.GagspeakTitleFont, color);

    public void BigText(string text, Vector4? color = null)
        => FontText(text, _fonts.UidFont, color);

    private static int FindWrapPosition(string text, float wrapWidth)
    {
        float currentWidth = 0;
        var lastSpacePos = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            currentWidth += ImGui.CalcTextSize(c.ToString()).X;
            if (char.IsWhiteSpace(c))
            {
                lastSpacePos = i;
            }
            if (currentWidth > wrapWidth)
            {
                return lastSpacePos >= 0 ? lastSpacePos : i;
            }
        }
        return -1;
    }

    private static string FormatTextForDisplay(string text, float wrapWidth)
    {
        // Normalize newlines for processing
        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();

        // Traverse each line to check if it exceeds the wrap width
        for (var i = 0; i < lines.Count; i++)
        {
            var lineWidth = ImGui.CalcTextSize(lines[i]).X;

            while (lineWidth > wrapWidth)
            {
                // Find where to break the line
                var wrapPos = FindWrapPosition(lines[i], wrapWidth);
                if (wrapPos >= 0)
                {
                    // Insert a newline at the wrap position
                    var part1 = lines[i].Substring(0, wrapPos);
                    var part2 = lines[i].Substring(wrapPos).TrimStart();
                    lines[i] = part1;
                    lines.Insert(i + 1, part2);
                    lineWidth = ImGui.CalcTextSize(part2).X;
                }
                else
                {
                    break;
                }
            }
        }

        // Join lines with \n for internal representation
        return string.Join("\n", lines);
    }

    private static unsafe int TextEditCallback(ImGuiInputTextCallbackData* data, float wrapWidth)
    {
        var text = Marshal.PtrToStringAnsi((IntPtr)data->Buf, data->BufTextLen);

        // Normalize newlines for processing
        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();

        var textModified = false;

        // Traverse each line to check if it exceeds the wrap width
        for (var i = 0; i < lines.Count; i++)
        {
            var lineWidth = ImGui.CalcTextSize(lines[i]).X;

            // Skip wrapping if this line ends with \r (i.e., it's a true newline)
            if (lines[i].EndsWith("\r"))
            {
                continue;
            }

            while (lineWidth > wrapWidth)
            {
                // Find where to break the line
                var wrapPos = FindWrapPosition(lines[i], wrapWidth);
                if (wrapPos >= 0)
                {
                    // Insert a newline at the wrap position
                    var part1 = lines[i].Substring(0, wrapPos);
                    var part2 = lines[i].Substring(wrapPos).TrimStart();
                    lines[i] = part1;
                    lines.Insert(i + 1, part2);
                    textModified = true;
                    lineWidth = ImGui.CalcTextSize(part2).X;
                }
                else
                {
                    break;
                }
            }
        }

        // Merge lines back to the buffer
        if (textModified)
        {
            var newText = string.Join("\n", lines); // Use \n for internal representation

            var newTextBytes = Encoding.UTF8.GetBytes(newText.PadRight(data->BufSize, '\0'));
            Marshal.Copy(newTextBytes, 0, (IntPtr)data->Buf, newTextBytes.Length);
            data->BufTextLen = newText.Length;
            data->BufDirty = 1;
            data->CursorPos = Math.Min(data->CursorPos, data->BufTextLen);
        }

        return 0;
    }

    public unsafe static bool InputTextWrapMultiline(string id, ref string text, uint maxLength = 500, int lineHeight = 2, float? width = null)
    {
        var wrapWidth = width ?? ImGui.GetContentRegionAvail().X; // Determine wrap width

        // Format text for display
        text = FormatTextForDisplay(text, wrapWidth);

        var result = ImGui.InputTextMultiline(id, ref text, maxLength,
             new(width ?? ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeightWithSpacing() * lineHeight), // Expand height calculation
             ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.NoHorizontalScroll, // Flag settings
             (data) => { return TextEditCallback(data, wrapWidth); });

        // Restore \r\n for display consistency
        text = text.Replace("\n", "");

        return result;
    }

    public void BooleanToColoredIcon(bool value, bool inline = true, FAI trueIcon = FAI.Check, FAI falseIcon = FAI.Times, Vector4 colorTrue = default, Vector4 colorFalse = default)
    {
        if (inline)
            ImGui.SameLine();

        if (value)
            using (ImRaii.PushColor(ImGuiCol.Text, (colorTrue == default) ? ImGuiColors.HealerGreen : colorTrue)) IconText(trueIcon);
        else
            using (ImRaii.PushColor(ImGuiCol.Text, (colorFalse == default) ? ImGuiColors.DalamudRed : colorFalse)) IconText(falseIcon);
    }

    public void DrawCombo<T>(string comboName, float width, IEnumerable<T> comboItems, Func<T, string> toName,
        Action<T?>? onSelected = null, T? initialSelectedItem = default, bool shouldShowLabel = true,
        ImGuiComboFlags flags = ImGuiComboFlags.None, string defaultPreviewText = "Nothing Selected..")
    {
        using var scrollbarWidth = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 12f);
        var comboLabel = shouldShowLabel ? $"{comboName}##{comboName}" : $"##{comboName}";
        if (!comboItems.Any())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo(comboLabel, defaultPreviewText, flags))
            {
                ImGui.EndCombo();
            }
            return;
        }

        if (!_selectedComboItems.TryGetValue(comboName, out var selectedItem) && selectedItem == null)
        {
            if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
            {
                selectedItem = initialSelectedItem;
                _selectedComboItems[comboName] = selectedItem!;
                if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
                    onSelected?.Invoke(initialSelectedItem);
            }
            else
            {
                selectedItem = comboItems.First();
                _selectedComboItems[comboName] = selectedItem!;
            }
        }

        var displayText = selectedItem == null ? defaultPreviewText : toName((T)selectedItem!);

        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo(comboLabel, displayText, flags))
        {
            foreach (var item in comboItems)
            {
                var isSelected = EqualityComparer<T>.Default.Equals(item, (T?)selectedItem);
                if (ImGui.Selectable(toName(item), isSelected))
                {
                    _selectedComboItems[comboName] = item!;
                    onSelected?.Invoke(item!);
                }
            }

            ImGui.EndCombo();
        }
        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _logger.LogTrace("Right-clicked on {comboName}. Resetting to default value.", comboName);
            selectedItem = comboItems.First();
            _selectedComboItems[comboName] = selectedItem!;
            onSelected?.Invoke((T)selectedItem!);
        }
        return;
    }

    public void DrawComboSearchable<T>(string comboName, float width, IEnumerable<T> comboItems, Func<T, string> toName,
        bool showLabel = true, Action<T?>? onSelected = null, T? initialSelectedItem = default,
        string defaultPreviewText = "No Items Available...", ImGuiComboFlags flags = ImGuiComboFlags.None)
    {
        using var scrollbarWidth = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 12f);
        try
        {
            // Return default if there are no items to display in the combo box.
            var comboLabel = showLabel ? $"{comboName}##{comboName}" : $"##{comboName}";
            if (!comboItems.Any())
            {
                ImGui.SetNextItemWidth(width);
                if (ImGui.BeginCombo(comboLabel, defaultPreviewText, flags))
                {
                    ImGui.EndCombo();
                }
                return;
            }

            // try to get currently selected item from a dictionary storing selections for each combo box.
            if (!_selectedComboItems.TryGetValue(comboName, out var selectedItem) && selectedItem == null)
            {
                if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
                {
                    selectedItem = initialSelectedItem;
                    _selectedComboItems[comboName] = selectedItem!;
                    if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
                        onSelected?.Invoke(initialSelectedItem);
                }
                else
                {
                    selectedItem = comboItems.First();
                    _selectedComboItems[comboName] = selectedItem!;
                }
            }

            // Retrieve or initialize the search string for this combo box.
            if (!SearchStrings.TryGetValue(comboName, out var searchString))
            {
                searchString = string.Empty;
                SearchStrings[comboName] = searchString;
            }

            var displayText = selectedItem == null ? defaultPreviewText : toName((T)selectedItem!);

            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo(comboLabel, displayText, flags))
            {
                // Search filter
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##filter", "Filter...", ref searchString, 255);
                SearchStrings[comboName] = searchString;
                var searchText = searchString.ToLowerInvariant();

                var filteredItems = string.IsNullOrEmpty(searchText)
                    ? comboItems
                    : comboItems.Where(item => toName(item).ToLowerInvariant().Contains(searchText));

                // display filtered content.
                foreach (var item in filteredItems)
                {
                    var isSelected = EqualityComparer<T>.Default.Equals(item, (T?)selectedItem);
                    if (ImGui.Selectable(toName(item), isSelected))
                    {
                        _logger.LogTrace("Selected {item} from {comboName}", toName(item), comboName);
                        _selectedComboItems[comboName] = item!;
                        onSelected?.Invoke(item!);
                    }
                }
                ImGui.EndCombo();
            }
            // Check if the item was right-clicked. If so, reset to default value.
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _logger.LogTrace("Right-clicked on {comboName}. Resetting to default value.", comboName);
                selectedItem = comboItems.First();
                _selectedComboItems[comboName] = selectedItem!;
                onSelected?.Invoke((T)selectedItem!);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DrawComboSearchable");
        }
    }

    public void DrawTimeSpanCombo(string label, TimeSpan patternMaxDuration, ref TimeSpan patternDuration, float width, string format = "hh\\:mm\\:ss", bool showLabel = true)
    {
        if (patternDuration > patternMaxDuration) patternDuration = patternMaxDuration;

        var maxDurationFormatted = patternMaxDuration.ToString(format);
        var patternDurationFormatted = patternDuration.ToString(format);

        // Button to open popup
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.Button($"{patternDurationFormatted} / {maxDurationFormatted}##TimeSpanCombo-{label}", new Vector2(width, ImGui.GetFrameHeight())))
        {
            ImGui.SetNextWindowPos(new Vector2(pos.X, pos.Y + ImGui.GetFrameHeight()));
            ImGui.OpenPopup($"TimeSpanPopup-{label}");
        }
        // just to the right of it, aligned with the button, display the label
        if (showLabel)
        {
            ImUtf8.SameLineInner();
            ImGui.TextUnformatted(label);
        }

        // Popup
        if (ImGui.BeginPopup($"TimeSpanPopup-{label}"))
        {
            DrawTimeSpanUI(ref patternDuration, patternMaxDuration, width, format);
            ImGui.EndPopup();
        }
    }

    private void DrawTimeSpanUI(ref TimeSpan patternDuration, TimeSpan maxDuration, float width, string format)
    {
        var totalColumns = GetColumnCountFromFormat(format);
        var extraPadding = ImGui.GetStyle().ItemSpacing.X;

        Vector2 patternHourTextSize;
        Vector2 patternMinuteTextSize;
        Vector2 patternSecondTextSize;
        Vector2 patternMillisecondTextSize;

        using (_fonts.UidFont.Push())
        {
            patternHourTextSize = ImGui.CalcTextSize($"{patternDuration.Hours:00}h");
            patternMinuteTextSize = ImGui.CalcTextSize($"{patternDuration.Minutes:00}m");
            patternSecondTextSize = ImGui.CalcTextSize($"{patternDuration.Seconds:00}s");
            patternMillisecondTextSize = ImGui.CalcTextSize($"{patternDuration.Milliseconds:000}ms");
        }

        // Specify the number of columns. In this case, 2 for minutes and seconds.
        if (ImGui.BeginTable("TimeDurationTable", totalColumns)) // 3 columns for hours, minutes, seconds
        {
            // Setup columns based on the format
            if (format.Contains("hh")) ImGui.TableSetupColumn("##Hours", ImGuiTableColumnFlags.WidthFixed, patternHourTextSize.X + totalColumns + 1);
            if (format.Contains("mm")) ImGui.TableSetupColumn("##Minutes", ImGuiTableColumnFlags.WidthFixed, patternMinuteTextSize.X + totalColumns + 1);
            if (format.Contains("ss")) ImGui.TableSetupColumn("##Seconds", ImGuiTableColumnFlags.WidthFixed, patternSecondTextSize.X + totalColumns + 1);
            if (format.Contains("fff")) ImGui.TableSetupColumn("##Milliseconds", ImGuiTableColumnFlags.WidthFixed, patternMillisecondTextSize.X + totalColumns + 1);
            ImGui.TableNextRow();

            // Draw components based on the format
            if (format.Contains("hh"))
            {
                ImGui.TableNextColumn();
                DrawTimeComponentUI(ref patternDuration, maxDuration, "h");
            }
            if (format.Contains("mm"))
            {
                ImGui.TableNextColumn();
                DrawTimeComponentUI(ref patternDuration, maxDuration, "m");
            }
            if (format.Contains("ss"))
            {
                ImGui.TableNextColumn();
                DrawTimeComponentUI(ref patternDuration, maxDuration, "s");
            }
            if (format.Contains("fff"))
            {
                ImGui.TableNextColumn();
                DrawTimeComponentUI(ref patternDuration, maxDuration, "ms");
            }

            ImGui.EndTable();
        }
    }

    private void DrawTimeComponentUI(ref TimeSpan duration, TimeSpan maxDuration, string suffix)
    {
        var prevValue = suffix switch
        {
            "h" => $"{Math.Max(0, (duration.Hours - 1)):00}",
            "m" => $"{Math.Max(0, (duration.Minutes - 1)):00}",
            "s" => $"{Math.Max(0, (duration.Seconds - 1)):00}",
            "ms" => $"{Math.Max(0, (duration.Milliseconds - 10)):000}",
            _ => $"UNK"
        };

        var currentValue = suffix switch
        {
            "h" => $"{duration.Hours:00}h",
            "m" => $"{duration.Minutes:00}m",
            "s" => $"{duration.Seconds:00}s",
            "ms" => $"{duration.Milliseconds:000}ms",
            _ => $"UNK"
        };

        var nextValue = suffix switch
        {
            "h" => $"{Math.Min(maxDuration.Hours, (duration.Hours + 1)):00}",
            "m" => $"{Math.Min(maxDuration.Minutes, (duration.Minutes + 1)):00}",
            "s" => $"{Math.Min(maxDuration.Seconds, (duration.Seconds + 1)):00}",
            "ms" => $"{Math.Min(maxDuration.Milliseconds, (duration.Milliseconds + 10)):000}",
            _ => $"UNK"
        };

        float CurrentValBigSize;
        using (_fonts.UidFont.Push())
        {
            CurrentValBigSize = ImGui.CalcTextSize(currentValue).X;
        }
        var offset = (CurrentValBigSize - ImGui.CalcTextSize(prevValue).X) / 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        ImGui.TextDisabled(prevValue); // Previous value (centered)
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5f);
        BigText(currentValue);

        // adjust the value with the mouse wheel
        if (ImGui.IsItemHovered() && ImGui.GetIO().MouseWheel != 0)
        {
            var hours = duration.Hours;
            var minutes = duration.Minutes;
            var seconds = duration.Seconds;
            var milliseconds = duration.Milliseconds;

            var delta = -(int)ImGui.GetIO().MouseWheel;
            if (suffix == "h") { hours += delta; }
            if (suffix == "m") { minutes += delta; }
            if (suffix == "s") { seconds += delta; }
            if (suffix == "ms") { milliseconds += delta * 10; }
            // Rollover and clamp logic
            if (milliseconds < 0) { milliseconds += 1000; seconds--; }
            if (milliseconds > 999) { milliseconds -= 1000; seconds++; }
            if (seconds < 0) { seconds += 60; minutes--; }
            if (seconds > 59) { seconds -= 60; minutes++; }
            if (minutes < 0) { minutes += 60; hours--; }
            if (minutes > 59) { minutes -= 60; hours++; }

            hours = Math.Clamp(hours, 0, maxDuration.Hours);
            minutes = Math.Clamp(minutes, 0, (hours == maxDuration.Hours ? maxDuration.Minutes : 59));
            seconds = Math.Clamp(seconds, 0, (minutes == (hours == maxDuration.Hours ? maxDuration.Minutes : 59) ? maxDuration.Seconds : 59));
            milliseconds = Math.Clamp(milliseconds, 0, (seconds == (minutes == (hours == maxDuration.Hours ? maxDuration.Minutes : 59) ? maxDuration.Seconds : 59) ? maxDuration.Milliseconds : 999));

            // update the duration
            duration = new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5f);
        var offset2 = (CurrentValBigSize - ImGui.CalcTextSize(prevValue).X) / 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset2);
        ImGui.TextDisabled(nextValue); // Previous value (centered)
    }
    private int GetColumnCountFromFormat(string format)
    {
        var columnCount = 0;
        if (format.Contains("hh")) columnCount++;
        if (format.Contains("mm")) columnCount++;
        if (format.Contains("ss")) columnCount++;
        if (format.Contains("fff")) columnCount++;
        return columnCount;
    }

    public void SetCursorXtoCenter(float width)
    {
        // push the big boi font for the UID
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - width / 2);
    }
    public void DrawHelpText(string helpText, bool inner = false)
    {
        if (inner) { ImUtf8.SameLineInner(); }
        else { ImGui.SameLine(); }
        var hovering = ImGui.IsMouseHoveringRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetTextLineHeight()));
        IconText(FAI.QuestionCircle, hovering ? ImGui.GetColorU32(ImGuiColors.TankBlue) : ImGui.GetColorU32(ImGuiCol.TextDisabled));
        AttachToolTip(helpText);
    }

    public bool DrawOtherPluginState()
    {
        var check = FAI.Check;
        var cross = FAI.SquareXmark;
        ImGui.TextUnformatted(GSLoc.Settings.OptionalPlugins);

        ImGui.SameLine();
        ImGui.TextUnformatted("Penumbra");
        ImGui.SameLine();
        IconText(IpcCallerPenumbra.APIAvailable ? check : cross, GetBoolColor(IpcCallerPenumbra.APIAvailable));
        ImGui.SameLine();
        AttachToolTip(IpcCallerPenumbra.APIAvailable ? GSLoc.Settings.PluginValid : GSLoc.Settings.PluginInvalid);
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextUnformatted("Glamourer");
        ImGui.SameLine();
        IconText(IpcCallerGlamourer.APIAvailable ? check : cross, GetBoolColor(IpcCallerGlamourer.APIAvailable));
        ImGui.SameLine();
        AttachToolTip(IpcCallerGlamourer.APIAvailable ? GSLoc.Settings.PluginValid : GSLoc.Settings.PluginInvalid);
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextUnformatted("Customize+");
        ImGui.SameLine();
        IconText(IpcCallerCustomize.APIAvailable ? check : cross, GetBoolColor(IpcCallerCustomize.APIAvailable));
        ImGui.SameLine();
        AttachToolTip(IpcCallerCustomize.APIAvailable ? GSLoc.Settings.PluginValid : GSLoc.Settings.PluginInvalid);
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextUnformatted("Moodles");
        ImGui.SameLine();
        IconText(IpcCallerMoodles.APIAvailable ? check : cross, GetBoolColor(IpcCallerMoodles.APIAvailable));
        ImGui.SameLine();
        AttachToolTip(IpcCallerMoodles.APIAvailable ? GSLoc.Settings.PluginValid : GSLoc.Settings.PluginInvalid);
        ImGui.Spacing();

        return true;
    }

    public Vector2 GetIconButtonSize(FAI icon)
    {
        using var font = _fonts.IconFont.Push();
        return ImGuiHelpers.GetButtonSize(icon.ToIconString());
    }

    public Vector2 GetIconData(FAI icon)
    {
        using var font = _fonts.IconFont.Push();
        return ImGui.CalcTextSize(icon.ToIconString());
    }

    public void IconText(FAI icon, uint color)
    {
        FontText(icon.ToIconString(), _fonts.IconFont, color);
    }

    public void IconText(FAI icon, Vector4? color = null)
    {
        IconText(icon, color == null ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(color.Value));
    }

    // for grabbing key states (we did something similar with the hardcore module)
    [LibraryImport("user32")]
    internal static partial short GetKeyState(int nVirtKey);


    private static void CenterWindow(float width, float height, ImGuiCond cond = ImGuiCond.None)
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetWindowPos(new Vector2(center.X - width / 2, center.Y - height / 2), cond);
    }

    [GeneratedRegex(@"^(?:[a-zA-Z]:\\[\w\s\-\\]+?|\/(?:[\w\s\-\/])+?)$", RegexOptions.ECMAScript, 5000)]
    private static partial Regex PathRegex();

    private void FontText(string text, IFontHandle font, Vector4? color = null)
    {
        FontText(text, font, color == null ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(color.Value));
    }

    private void FontText(string text, IFontHandle font, uint color)
    {
        using var pushedFont = font.Push();
        using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
    }

    /// <summary> 
    /// Retrieves the various UID text color based on the current server state.
    /// </summary>
    /// <returns> The color of the UID text in Vector4 format .</returns>
    public Vector4 GetUidColor()
    {
        return MainHub.ServerStatus switch
        {
            ServerState.Connecting => ImGuiColors.DalamudYellow,
            ServerState.Reconnecting => ImGuiColors.DalamudRed,
            ServerState.Connected => ImGuiColors.ParsedPink,
            ServerState.Disconnected => ImGuiColors.DalamudYellow,
            ServerState.Disconnecting => ImGuiColors.DalamudYellow,
            ServerState.Unauthorized => ImGuiColors.DalamudRed,
            ServerState.VersionMisMatch => ImGuiColors.DalamudRed,
            ServerState.Offline => ImGuiColors.DalamudRed,
            ServerState.NoSecretKey => ImGuiColors.DalamudYellow,
            _ => ImGuiColors.DalamudRed
        };
    }

    public Vector4 GetServerStateColor()
    {
        return MainHub.ServerStatus switch
        {
            ServerState.Connecting => ImGuiColors.DalamudYellow,
            ServerState.Reconnecting => ImGuiColors.DalamudYellow,
            ServerState.Connected => ImGuiColors.HealerGreen,
            ServerState.Disconnected => ImGuiColors.DalamudRed,
            ServerState.Disconnecting => ImGuiColors.DalamudYellow,
            ServerState.Unauthorized => ImGuiColors.ParsedOrange,
            ServerState.VersionMisMatch => ImGuiColors.ParsedOrange,
            ServerState.Offline => ImGuiColors.DPSRed,
            ServerState.NoSecretKey => ImGuiColors.ParsedOrange,
            _ => ImGuiColors.ParsedOrange
        };
    }

    public FAI GetServerStateIcon(ServerState state)
    {
        return state switch
        {
            ServerState.Connecting => FAI.SatelliteDish,
            ServerState.Reconnecting => FAI.SatelliteDish,
            ServerState.Connected => FAI.Link,
            ServerState.Disconnected => FAI.Unlink,
            ServerState.Disconnecting => FAI.SatelliteDish,
            ServerState.Unauthorized => FAI.Shield,
            ServerState.VersionMisMatch => FAI.Unlink,
            ServerState.Offline => FAI.Signal,
            ServerState.NoSecretKey => FAI.Key,
            _ => FAI.ExclamationTriangle
        };
    }

    /// <summary> 
    /// Retrieves the various UID text based on the current server state.
    /// </summary>
    /// <returns> The text of the UID.</returns>
    public string GetUidText()
    {
        return MainHub.ServerStatus switch
        {
            ServerState.Reconnecting => "Reconnecting",
            ServerState.Connecting => "Connecting",
            ServerState.Disconnected => "Disconnected",
            ServerState.Disconnecting => "Disconnecting",
            ServerState.Unauthorized => "Unauthorized",
            ServerState.VersionMisMatch => "Version mismatch",
            ServerState.Offline => "Unavailable",
            ServerState.NoSecretKey => "No Secret Key",
            ServerState.Connected => MainHub.DisplayName, // displays when connected, your UID
            _ => string.Empty
        };
    }

    public sealed record IconScaleData(Vector2 IconSize, Vector2 NormalizedIconScale, float OffsetX, float IconScaling);
}
