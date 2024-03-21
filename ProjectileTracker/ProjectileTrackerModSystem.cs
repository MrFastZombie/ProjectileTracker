using ProjectileTracker.EntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Util;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Config;

namespace ProjectileTracker;

public class ProjectileTrackerModSystem : ModSystem
{
    #region Client
    ICoreClientAPI clientAPI;
    IClientNetworkChannel clientChannel;
    Ptconfig clientConfig;
    public override void StartClientSide(ICoreClientAPI api)
    {
        clientAPI = api;
        base.Start(api);

        clientConfig = api.LoadModConfig<Ptconfig>("ProjectileTrackerConfig.json");
        if(clientConfig == null) {
            api.Logger.Log(EnumLogType.Warning, Lang.Get("projectiletracker:client-confignotfound"));
            clientConfig = new Ptconfig();
        }

        clientChannel =
                api.Network.RegisterChannel("projectiletracker")
                .RegisterMessageType(typeof(PtNetwork.NetworkApiMessage))
                .RegisterMessageType(typeof(PtNetwork.NetworkApiResponse))
                .SetMessageHandler<PtNetwork.NetworkApiMessage>(OnServerMessage);

        api.StoreModConfig(clientConfig, "ProjectileTrackerConfig.json");
    }

    private void OnServerMessage(PtNetwork.NetworkApiMessage msg) {
        clientAPI.Logger.Log(EnumLogType.Debug, "Server Message: " + msg.message);
        if(msg.message == "sendinfo") clientChannel.SendPacket(new PtNetwork.NetworkApiResponse { response = "config |" + clientConfig.ToString()});
        if(msg.message.StartsWith("wpupdate |") && clientConfig.EnableProjectileTracker && clientConfig.allowWelcomeMessage) clientAPI.ShowChatMessage(Lang.Get("projectiletracker:welcome", msg.message[10..]));
    }

    #endregion

    #region Server
    ICoreServerAPI serverAPI;
    PtNetwork serverNetwork;

    public static Dictionary<string, Ptconfig> clientConfigs = new();
    public static Dictionary<string, List<Waypoint>> pendingWaypoints = new();
    public static bool serverLoaded = false;
    private Ptconfig serverConfig;
    //IServerNetworkChannel serverChannel;

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverAPI = api;
        base.StartServerSide(api);
        api.RegisterEntityBehaviorClass("InjectProjectileTracker", typeof(InjectProjectileTracker)); //This behavior class contains the logic for detecting projectiles on the server side.
        //var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;

        serverNetwork = new(api);

        serverConfig = api.LoadModConfig<Ptconfig>("ProjectileTrackerConfig.json");
        
        
        api.ChatCommands.Create("ptpurge")
            .WithDescription(Lang.Get("projectiletracker:ptpurge-desc"))
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(new OnCommandDelegate(OnPurgeCommand));

        api.ChatCommands.Create("ptclearorphans")
            .WithDescription(Lang.Get("projectiletracker:ptclearorphans-desc"))
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(new OnCommandDelegate(OnClearOrphansCommand));

        //api.Event.PlayerJoin += Event_PlayerJoin;
        api.Event.PlayerNowPlaying += Event_PlayerJoin;
        api.Event.SaveGameLoaded += OnSaveGameLoading;
        api.Event.GameWorldSave += OnSaveGameSaving;
        api.Event.SaveGameLoaded += OnServerLoaded;
        //api.Event.OnEntitySpawn += OnEntitySpawn;

        api.StoreModConfig(serverConfig, "ProjectileTrackerConfig.json");
    }

    private struct NewBehavior //Thank you Maltiez for having an example of this method for adding entity behaviors in FSMlib.
    { 
         public string code; 
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);

        Ptconfig sConfig =api.LoadModConfig<Ptconfig>("ProjectileTrackerConfig.json");

        NewBehavior ptBehavior = new() {code = "InjectProjectileTracker"};
        JsonObject ptBehaviorJson = new(JToken.FromObject(ptBehavior));
        
        if(api.Side == EnumAppSide.Server) {
            List<string> checkedTyped = new() { "EntityProjectile", "AdvancedEntityProjectile" }; //AdvancedEntityProjectile is for FSMLib.

            foreach (EntityProperties p in api.World.EntityTypes) {
                if(checkedTyped.Contains(p.Class) && !sConfig.projectileBlacklist.Contains(p.Code.Path)) {
                    if(!sConfig.InjectModdedProjectiles && p.Code.Domain != "game") continue; //Only inject vanilla projectiles if InjectModdedProjectiles is false.
                    
                    api.Logger.Log(EnumLogType.Debug, Lang.GetMatching("projectiletracker:injecting", p.Code.Path, p.Class));
                    p.Server.BehaviorsAsJsonObj = p.Server.BehaviorsAsJsonObj.Prepend(ptBehaviorJson).ToArray();
                } 
            }
        }
    }

    private void Event_PlayerJoin(IServerPlayer player)
    {
        serverNetwork.OnPlayerJoin(player);
    }

    private void OnServerLoaded()
    {
        serverLoaded = true;
    }

    private TextCommandResult OnPurgeCommand(TextCommandCallingArgs args) {
        MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
        
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

    private TextCommandResult OnClearOrphansCommand(TextCommandCallingArgs args) {
        MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var maplayer = serverAPI.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = maplayer.Waypoints;

        foreach (Waypoint waypoint in waypoints.ToList()) {
            if(waypoint.Title.StartsWith("Projectile ") && waypoint.OwningPlayerUid == args.Caller.Player.PlayerUID) {
                var entityId = waypoint.Title.Split(' ')[1].ToLong();
                if(serverAPI.World.GetEntityById(entityId) == null) {
                    waypoints.Remove(waypoint);
                    ResendWaypoints.Invoke(maplayer, new Object[] { args.Caller.Player as IServerPlayer });
                    RebuildMapComponents.Invoke(maplayer, null);
                }

            }
        }

        return TextCommandResult.Success();
    }

    private void OnSaveGameSaving() {
        serverAPI.WorldManager.SaveGame.StoreData("ptconfigs", SerializerUtil.Serialize(clientConfigs));
        serverAPI.WorldManager.SaveGame.StoreData("ptwaypoints", SerializerUtil.Serialize(pendingWaypoints));
    }
    private void OnSaveGameLoading() {
        /*byte[] data = serverAPI.WorldManager.SaveGame.GetData("ptconfigs");
        clientConfigs = data == null ? new() : SerializerUtil.Deserialize<Dictionary<string, Ptconfig>>(data);
        serverAPI.Logger.Log(EnumLogType.Debug, "Stored Projectile Tracker configs loaded.");*/

        byte[] wpdata = serverAPI.WorldManager.SaveGame.GetData("ptwaypoints");
        pendingWaypoints = wpdata == null ? new() : SerializerUtil.Deserialize<Dictionary<string, List<Waypoint>>>(wpdata);
    }

    #endregion
}
