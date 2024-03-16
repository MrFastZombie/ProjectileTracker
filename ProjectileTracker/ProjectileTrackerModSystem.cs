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
using Vintagestory.API.Util;

namespace ProjectileTracker;

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
        
        api.ChatCommands.Create("ptpurge")
            .WithDescription("Purge all Projectile Tracker waypoints")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(new OnCommandDelegate(OnPurgeCommand));

        api.ChatCommands.Create("ptclearorphans")
            .WithDescription("Clear all Projectile Tracker waypoints that no longer have a corresponding entity")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(new OnCommandDelegate(OnClearOrphansCommand));
        
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

    #endregion
}
