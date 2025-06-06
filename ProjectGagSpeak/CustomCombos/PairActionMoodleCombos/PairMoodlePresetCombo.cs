using GagSpeak.CkCommons.Helpers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.IPC;
using GagspeakAPI.Extensions;
using ImGuiNET;

namespace GagSpeak.CustomCombos.Moodles;

public sealed class PairMoodlePresetCombo : CkMoodleComboButtonBase<MoodlePresetInfo>
{
    private int longestPresetCount => _pairRef.LastIpcData.MoodlesPresets.Max(x => x.Statuses.Count);
    public PairMoodlePresetCombo(float iconScale, IconDisplayer monitor, Pair pair, MainHub hub, ILogger log)
        : base(iconScale, monitor, pair, hub, log, () => [ ..pair.LastIpcData.MoodlesPresets.OrderBy(x => x.Title)])
    { }

    protected override bool DisableCondition()
        => _pairRef.PairPerms.MoodlePerms.HasAny(MoodlePerms.PairCanApplyYourMoodlesToYou) is false;

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var moodlePreset = Items[globalIdx];
        var ret = ImGui.Selectable(moodlePreset.Title.StripColorTags(), selected);

        if (moodlePreset.Statuses.Count <= 0)
            return ret;

        ImGui.SameLine();
        var offset = ImGui.GetContentRegionAvail().X - IconSize.X * moodlePreset.Statuses.Count;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        for (int i = 0, iconsDrawn = 0; i < moodlePreset.Statuses.Count; i++)
        {
            var status = moodlePreset.Statuses[i];
            var info = _pairRef.LastIpcData.MoodlesStatuses.FirstOrDefault(x => x.GUID == status);

            if (EqualityComparer<MoodlesStatusInfo>.Default.Equals(info, default))
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + IconSize.X);
                continue;
            }

            _statuses.DrawMoodleIcon(info.IconID, info.Stacks, IconSize);
            // get the dispelable moodle if any.
            var title = info.StatusOnDispell.IsEmptyGuid()
                ? "Unknown"
                : _pairRef.LastIpcData.MoodlesStatuses.FirstOrDefault(x => x.GUID == info.StatusOnDispell).Title ?? "Unknown";
            DrawItemTooltip(info, title);

            if (++iconsDrawn < moodlePreset.Statuses.Count)
                ImGui.SameLine();
        }

        return ret;
    }

    protected override bool CanDoAction(MoodlePresetInfo item)
        => IconDisplayer.CanApplyPairStatus(_pairRef.PairPerms, item.Statuses.Select(
            x => _pairRef.LastIpcData.MoodlesStatuses.FirstOrDefault(y => y.GUID == x)).ToList());

    protected override async Task<bool> OnApplyButton(MoodlePresetInfo item)
    {
        var statusInfos = item.Statuses
            .Select(x => _pairRef.LastIpcData.MoodlesStatuses.FirstOrDefault(y => y.GUID == x))
            .ToArray();

        var dto = new ApplyMoodlesByGuidDto(_pairRef.UserData, statusInfos.Select(x => x.GUID).ToArray(), MoodleType.Preset);
        if (await _mainHub.UserApplyMoodlesByGuid(dto))
        {
            Log.LogDebug($"Applying moodle preset {item.Title} on {_pairRef.GetNickAliasOrUid()}", LoggerType.Permissions);
            return true;
        }
        else
        {
            Log.LogDebug($"Failed to apply moodle preset {item.Title} on {_pairRef.GetNickAliasOrUid()}", LoggerType.Permissions);
            return false;
        }
    }

    protected override async Task<bool> OnRemoveButton(MoodlePresetInfo item)
        => await Task.FromResult(false);
}
