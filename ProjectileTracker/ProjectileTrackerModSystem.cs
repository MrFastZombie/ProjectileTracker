using ProjectileTracker.EntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Linq;
using System;
using System.Reflection;
using ProtoBuf;

namespace ProjectileTracker;

//The network code is based on the Wiki's example as of 3/14/2024. https://wiki.vintagestory.at/index.php?title=Modding:Network_API&oldid=169493
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
public class ProjectileTrackerModSystem : ModSystem
{
    #region Client
    ICoreClientAPI clientAPI;
    IClientNetworkChannel clientChannel;
    public override void StartClientSide(ICoreClientAPI api)
    {
        clientAPI = api;
        base.Start(api);
        //api.RegisterEntityBehaviorClass("InjectProjectileTracker", typeof(InjectProjectileTracker));

        clientChannel = 
            api.Network.RegisterChannel("ProjectileTracker")
            .RegisterMessageType(typeof(NetworkApiMessage))
            .RegisterMessageType(typeof(NetworkApiResponse))
            .SetMessageHandler<NetworkApiMessage>(OnServerMessage);
    }

    private void OnServerMessage(NetworkApiMessage networkMessage) {
        clientAPI.ShowChatMessage("Received folling message from server: " + networkMessage.message);
        clientAPI.ShowChatMessage("Sending response");
        clientChannel.SendPacket(new NetworkApiResponse() {
            response = "Hi server!"
        });
    }

    #endregion

    #region Server
    ICoreServerAPI serverAPI;
    IServerNetworkChannel serverChannel;

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverAPI = api;
        base.StartServerSide(api);
        api.RegisterEntityBehaviorClass("InjectProjectileTracker", typeof(InjectProjectileTracker)); //This behavior class contains the logic for detecting projectiles on the server side.
        //var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;
        
        serverChannel = 
            api.Network.RegisterChannel("ProjectileTracker")
            .RegisterMessageType(typeof(NetworkApiMessage))
            .RegisterMessageType(typeof(NetworkApiResponse))
            .SetMessageHandler<NetworkApiResponse>(OnClientMessage);

        api.ChatCommands.Create("ptpurge")
            .WithDescription("Purge all Projectile Tracker waypoints")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(new OnCommandDelegate(OnPurgeCommand));
        
        api.ChatCommands.Create("pttest")
            .WithDescription("Test command")
            .RequiresPrivilege(Privilege.controlserver)
            .HandleWith(new OnCommandDelegate(OnPttestCommand));
        
    }

    private TextCommandResult OnPttestCommand(TextCommandCallingArgs args) {
        serverChannel.BroadcastPacket(new NetworkApiMessage() {
            message = "test",
        });
        return TextCommandResult.Success();
    }

    private TextCommandResult OnPurgeCommand(TextCommandCallingArgs args) {
        MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
        
        //Code here
        var maplayer = serverAPI.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = maplayer.Waypoints;

        foreach (Waypoint waypoint in waypoints.ToList()) {
            if(waypoint.Title.StartsWith("Projectile ") && waypoint.OwningPlayerUid == args.Caller.Player.PlayerUID) {
                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] { args.Caller.Player as IServerPlayer });
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }

        return TextCommandResult.Success();
    }

    private void OnClientMessage(IPlayer fromPlayer, NetworkApiResponse networkMessage) {
        serverAPI.SendMessageToGroup(
            GlobalConstants.GeneralChatGroup,
            "Received following message from " + fromPlayer.PlayerName + ": " + networkMessage.response,
            EnumChatType.Notification
        );
    }

    #endregion
}
