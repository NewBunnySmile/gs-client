using CkCommons;
using GagSpeak.Interop;
using GagSpeak.Services.Mediator;
using GagSpeak.State.Caches;
using GagSpeak.State.Handlers;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;

namespace GagSpeak.State.Listeners;

public class GlamourListener : IDisposable
{
    private readonly ILogger<GlamourListener> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly IpcCallerGlamourer _ipc;
    private readonly GlamourCache _cache;
    private readonly GlamourHandler _handler;
    public GlamourListener(ILogger<GlamourListener> logger, GagspeakMediator mediator,
        IpcCallerGlamourer ipc, GlamourCache cache, GlamourHandler handler)
    {
        _logger = logger;
        _mediator = mediator;
        _ipc = ipc;
        _cache = cache;
        _handler = handler;

        // Always attempt to immediately cache our player.
        _handler.CacheActorFromLatest();
        
        _ipc.OnStateChanged = StateChangedWithType.Subscriber(Svc.PluginInterface, OnStateChanged);
        _ipc.OnStateFinalized = StateFinalized.Subscriber(Svc.PluginInterface, OnStateFinalized);
        _ipc.OnStateChanged.Enable();
        _ipc.OnStateFinalized.Enable();
    }

    public void Dispose()
    {
        _ipc.OnStateChanged.Disable();
        _ipc.OnStateChanged?.Dispose();
        _ipc.OnStateFinalized.Disable();
        _ipc.OnStateFinalized?.Dispose();
    }

    /// <summary>
    ///     When ANY Glamourer state change occurs for ANY given actor, this is fired. <para />
    ///     
    ///     While it is primarily used to enforce bondage on the client, it is also used to sync
    ///     the client's current glamour state with other Kinksters. <para />
    ///     
    ///     For this purpose, any case where we would abort, we should still push a change to the Mediator.
    /// </summary>
    private async void OnStateChanged(nint address, StateChangeType changeType)
    {
        if (address != PlayerData.ObjectAddress)
            return;

        if (changeType is not (StateChangeType.Equip or StateChangeType.Stains or StateChangeType.Other))
        {
            _mediator.Publish(new GlamourerChanged());
            return;
        }

        if (_handler.BlockIpcCalls is not IpcBlockReason.None)
        {
            // _logger.LogTrace($"[OnStateChanged] ChangeType: [{changeType}] blocked! Still processing! ({_handler.BlockIpcCalls})", LoggerType.IpcGlamourer);
            return;
        }

        // Handle the only state changes we want to care about, as they interfere
        // with OnStateFinalized, or rather are special exceptions.
        if (changeType is StateChangeType.Equip or StateChangeType.Stains)
        {
            if(_cache.FinalGlamour.Keys.Count() <= 0)
            {
                _mediator.Publish(new GlamourerChanged());
                return;
            }

            _logger.LogDebug($"[OnStateChanged] ChangeType: [{changeType}] accepted, Processing ApplySemaphore!", LoggerType.IpcGlamourer);
            await _handler.UpdateGlamourCacheSlim(true);
        }
        else if (changeType is StateChangeType.Other)
        {
            if (_cacheLatestForNextMeta)
            {
                _handler.CacheActorMeta(true);
                _cacheLatestForNextMeta = false;
            }

            if (!_cache.FinalMeta.AnySet())
            {
                _mediator.Publish(new GlamourerChanged());
                return;
            }

            _logger.LogDebug($"[OnStateChanged] ChangeType: [{changeType}] accepted, Processing ApplyMetaCache!", LoggerType.IpcGlamourer);
            await _handler.UpdateMetaCacheSlim(true);
        }

        // We can now inform of the update now that our state is synced with any applied bondage.
        _mediator.Publish(new GlamourerChanged());
    }

    // stupid dumb variable that only even partially helps assist an overarching
    // problem with metadata I wish i never bothered with.
    private bool _cacheLatestForNextMeta = false;

    /// <summary> 
    ///     Any any primary Glamourer Operation has completed, StateFinalized will fire. 
    ///     (This IPC Call is a Godsend).
    /// </summary>
    /// <param name="address"> The address of the actor that was finalized. </param>
    /// <param name="finalizationType"> The type of finalization that occurred. </param>
    /// <remarks> This is primarily used to cache the state of the player after a glamour operation has completed. </remarks>
    private async void OnStateFinalized(nint address, StateFinalizationType finalizationType)
    {
        if (address != PlayerData.ObjectAddress)
            return;

        _logger.LogDebug($"[OnStateFinalized] Type: ({finalizationType})", LoggerType.IpcGlamourer);

        // if the finalization type was a gearset finalized, remove the gearset from the ipc blocker filter.
        if (finalizationType is StateFinalizationType.Gearset)
        {
            // if there was a gearset blocker for the same class
            if (_handler.BlockIpcCalls.HasFlag(IpcBlockReason.Gearset))
            {
                _logger.LogDebug($"[OnStateFinalized] Type was ({finalizationType}), removing Gearset Blocker!", LoggerType.IpcGlamourer);
                _handler.OnEquipGearsetFinalized();
                _cacheLatestForNextMeta = true;
            }
        }
        
        if (_handler.BlockIpcCalls is not IpcBlockReason.None)
        {
            _logger.LogDebug($"[OnStateFinalized] Type: ({finalizationType}) blocked! Still processing! ({_handler.BlockIpcCalls})", LoggerType.IpcGlamourer);
            _mediator.Publish(new GlamourerChanged());
            return;
        }

        _logger.LogDebug($"[OnStateFinalized] Type: ({finalizationType}) accepted, Caching & Applying!", LoggerType.IpcGlamourer);
        await _handler.ReapplyAllCaches();
        // process a kinksterSync update post-bondage reapplication.
        _mediator.Publish(new GlamourerChanged());
    }
}
