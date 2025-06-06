using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.VibeRoom;
using GagspeakAPI.Enums;
using GagspeakAPI.Hub;
using GagspeakAPI.Network;

namespace GagSpeak.VibeLobby;
public class VibeRoomManager : DisposableMediatorSubscriberBase
{
    private readonly MainHub _hub;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly SexToyManager _vibeService;

    private List<RoomInvite> CurrentInvites = new();

    public Task? LobbyManagementTask { get; private set; }
    public Task? LobbyDataStreamTask { get; private set; }
    public Task? LobbyUserUpdateTask { get; private set; }


    public VibeRoomManager(ILogger<VibeRoomManager> logger, GagspeakMediator mediator, 
        MainHub hub, OnFrameworkService frameworkUtils) : base(logger, mediator)
    {
        _hub = hub;
        _frameworkUtils = frameworkUtils;
    }

    public async Task CreateVibeRoom(string roomName, string roomPassword = "")
    {
        // Attempt to create a Vibe Room.
        var result = await _hub.RoomCreate(new RoomCreateRequest(roomName, VibeRoomFlags.None) { Password = roomPassword });
        if (result.ErrorCode is GagSpeakApiEc.Success)
        {
            Logger.LogInformation("Vibe Room created successfully.", LoggerType.VibeRooms);

        }

    }
}
