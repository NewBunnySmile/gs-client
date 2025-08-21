using CkCommons.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using GagSpeak.PlayerClient;
using GagSpeak.Services.Mediator;
using GagSpeak.State.Models;
using GagspeakAPI.Data;
using OtterGui;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;

namespace GagSpeak.CustomCombos.Editor;

public sealed class RestrictionCombo : CkFilterComboCache<RestrictionItem>, IMediatorSubscriber, IDisposable
{
    private readonly FavoritesManager _favorites;
    public Guid _currentRestriction;
    public RestrictionCombo(ILogger log, GagspeakMediator mediator, FavoritesManager favorites, 
        Func<IReadOnlyList<RestrictionItem>> generator) : base(generator, log)
    {
        Mediator = mediator;
        _favorites = favorites;
        _currentRestriction = Guid.Empty;
        SearchByParts = true;
        Mediator.Subscribe<ConfigRestrictionChanged>(this, _ => RefreshCombo());
    }

    public GagspeakMediator Mediator { get; }

    void IDisposable.Dispose()
    {
        Mediator.Unsubscribe<ConfigRestrictionChanged>(this);
        GC.SuppressFinalize(this);
    }

    protected override string ToString(RestrictionItem obj)
        => obj.Label;

    protected override int UpdateCurrentSelected(int currentSelected)
    {
        if (Current?.Identifier == _currentRestriction)
            return currentSelected;

        CurrentSelectionIdx = Items.IndexOf(i => i.Identifier == _currentRestriction);
        Current = CurrentSelectionIdx >= 0 ? Items[CurrentSelectionIdx] : null;
        return CurrentSelectionIdx;
    }

    /// <summary> An override to the normal draw method that forces the current item to be the item passed in. </summary>
    /// <returns> True if a new item was selected, false otherwise. </returns>
    public bool Draw(string label, Guid current, float width, uint? searchBg = null)
        => Draw(label, current, width, CFlags.None, searchBg);

    public bool Draw(string label, Guid current, float width, CFlags flags, uint? searchBg = null)
    {
        InnerWidth = width * 1.25f;
        _currentRestriction = current;
        var preview = Items.FirstOrDefault(i => i.Identifier == current)?.Label ?? "Select Restriction...";
        return Draw(label, preview, string.Empty, width, ImGui.GetTextLineHeightWithSpacing(), flags, searchBg);
    }

    public bool DrawPopup(string label, Guid current, float width, Vector2 drawPos, uint? searchBg = null)
    {
        InnerWidth = width * 1.25f;
        _currentRestriction = current;
        return DrawPopup(label, drawPos, ImGui.GetTextLineHeightWithSpacing(), searchBg);
    }

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var restriction = Items[globalIdx];

        if(Icons.DrawFavoriteStar(_favorites, FavoriteIdContainer.Restriction, restriction.Identifier, false) && CurrentSelectionIdx == globalIdx)
        {
            CurrentSelectionIdx = -1;
            Current = default;
        }
        ImUtf8.SameLineInner();
        var ret = ImGui.Selectable(restriction.Label, selected);
        return ret;
    }
}
