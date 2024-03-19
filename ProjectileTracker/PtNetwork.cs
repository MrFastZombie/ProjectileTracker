using Vintagestory.API.Common;
using Vintagestory.API.Server;
using ProtoBuf;
using Newtonsoft.Json;

namespace ProjectileTracker;
class PtNetwork
{

    ICoreServerAPI api;
    private readonly IServerNetworkChannel serverChannel;
    private static PtWaypoint ptWaypoint = new();
                 
    public PtNetwork(ICoreServerAPI api)
    {
        this.api = api;
        this.serverChannel = 
             api.Network.RegisterChannel("projectiletracker")
                .RegisterMessageType(typeof(NetworkApiMessage))
                .RegisterMessageType(typeof(NetworkApiResponse))
                .SetMessageHandler<NetworkApiResponse>(OnClientMessage);
    }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NetworkApiMessage
    {
        public string message;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NetworkApiResponse
    {
        public string response;
    }

    public void OnClientMessage(IServerPlayer player, NetworkApiResponse msg)
    {
        api.Logger.Log(EnumLogType.Debug, "Client Message: " + msg.response);
        if(msg.response.StartsWith("config |")) {
            Ptconfig newConfig = JsonConvert.DeserializeObject<Ptconfig>(msg.response[8..]);
            ProjectileTrackerModSystem.clientConfigs[player.PlayerUID] = newConfig;
        }
        
    }

    public Ptconfig GetPtconfig(ICoreServerAPI api, IServerPlayer player)
    {
        serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "sendinfo" }, player);
        return api.LoadModConfig<Ptconfig>("ProjectileTrackerConfig.json");
    }

    public void OnPlayerJoin(IServerPlayer player) {
        serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "sendinfo" }, player);
        if(ProjectileTrackerModSystem.pendingWaypoints.ContainsKey(player.PlayerUID)) {
            int wpcount = ProjectileTrackerModSystem.pendingWaypoints[player.PlayerUID].Count;
            ptWaypoint.ProcessStoredWaypoints(player.PlayerUID, api);
            serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "wpupdate |" + wpcount }, player);
        }
    }
}