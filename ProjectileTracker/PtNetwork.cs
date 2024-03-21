using Vintagestory.API.Common;
using Vintagestory.API.Server;
using ProtoBuf;
using Newtonsoft.Json;
using Vintagestory.API.Config;

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

    /// <summary>
    /// Run when the server receives a message from a client.
    /// </summary>
    /// <param name="player"> Player who sent the message. </param>
    /// <param name="msg"> The message from the client.</param>
    public void OnClientMessage(IServerPlayer player, NetworkApiResponse msg)
    {
        if(msg.response.StartsWith("config |")) {
            api.Logger.Log(EnumLogType.Debug, Lang.Get("projectiletracker:config-log", player.PlayerName, player.PlayerUID));
            Ptconfig newConfig = JsonConvert.DeserializeObject<Ptconfig>(msg.response[8..]);
            ProjectileTrackerModSystem.clientConfigs[player.PlayerUID] = newConfig;
        } else {
            api.Logger.Log(EnumLogType.Debug, "Client Message: " + msg.response); //The config packet causes a logging error we will turn off this log line for that.
        }
        
    }

    /// <summary>
    /// Get config from a player.
    /// </summary>
    /// <param name="api">Server's API.</param>
    /// <param name="player">The player to get the config from.</param>
    /// <returns>The player's config as a Ptconfig.</returns>
    public Ptconfig GetPtconfig(ICoreServerAPI api, IServerPlayer player)
    {
        serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "sendinfo" }, player);
        return api.LoadModConfig<Ptconfig>("ProjectileTrackerConfig.json");
    }

    /// <summary>
    /// Run when a player joins the server. This will retrieve the player's config and send waypoint updates to the player.
    /// </summary>
    /// <param name="player">Player who joined.</param>
    public void OnPlayerJoin(IServerPlayer player) {
        serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "sendinfo" }, player);
        if(ProjectileTrackerModSystem.pendingWaypoints.ContainsKey(player.PlayerUID)) {
            int wpcount = ProjectileTrackerModSystem.pendingWaypoints[player.PlayerUID].Count;
            ptWaypoint.ProcessStoredWaypoints(player.PlayerUID, api);
            serverChannel.SendPacket(new PtNetwork.NetworkApiMessage { message = "wpupdate |" + wpcount }, player);
        }
    }
}