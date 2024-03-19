using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Util;

namespace ProjectileTracker;
class PtWaypoint
{
    public PtWaypoint() { }

    private static readonly MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
    
    public void CreateWaypoint(ICoreServerAPI api, EntityProjectile p) {
        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var player = (p.FiredBy as EntityPlayer).Player as IServerPlayer;
        Ptconfig playerConfig = ProjectileTrackerModSystem.clientConfigs[player.PlayerUID];
        if(playerConfig == null || !playerConfig.EnableProjectileTracker) return;
        Waypoint newWaypoint = new() {
            Position = p.ServerPos.XYZ,
            Title = "Projectile " + p.EntityId,
            Pinned = false,
            Icon = playerConfig.icon,
            Color = System.Drawing.ColorTranslator.FromHtml(playerConfig.color).ToArgb(),
            OwningPlayerUid = player.PlayerUID
        };

        maplayer.AddWaypoint(newWaypoint, (p.FiredBy as EntityPlayer).Player as IServerPlayer);
    }
    
    public void RemoveWaypoint(ICoreServerAPI api, EntityProjectile p, bool forcestore = false) {
        if(p.FiredBy == null) {
                RemoveOrphanedWaypoint(api, p);
                return; //When the world is reloaded, FiredBy becoems null on saved projectile entiities.
        }

        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = getWaypoints(api);
        var player = (p.FiredBy as EntityPlayer).Player as IServerPlayer;
        foreach (Waypoint waypoint in waypoints.ToList().Where(w => w.OwningPlayerUid == player.PlayerUID))
        {
            if(waypoint.Title == "Projectile " + p.EntityId) {
                if(player == null || forcestore) {
                    api.Logger.Log(EnumLogType.Error, "Projectile " + p.EntityId + " had a waypoint for player " + waypoint.OwningPlayerUid + " but that player could not be retrieved. If this happened during a world load, this can be ignored!");
                    StoreWaypoint(waypoint.OwningPlayerUid, waypoint);
                    continue;
                }

                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] { player });
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }
    } //End of RemoveWaypoint()

    //Until https://github.com/anegostudios/VintageStory-Issues/issues/3723 is fixed, this method will remove waypoints for orphaned projectiles.
    private void RemoveOrphanedWaypoint(ICoreServerAPI api, EntityProjectile p) {
        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = getWaypoints(api);
        
        foreach (Waypoint waypoint in waypoints.ToList())
        {
            if(waypoint.Title == "Projectile " + p.EntityId) {
                var player = api.World.PlayerByUid(waypoint.OwningPlayerUid);
                if(player == null) {
                    api.Logger.Log(EnumLogType.Error, "Orphaned projectile " + p.EntityId + " had a waypoint for player " + waypoint.OwningPlayerUid + " but that player could not be retrieved.");
                    StoreWaypoint(waypoint.OwningPlayerUid, waypoint);
                    continue;
                }

                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] {player as IServerPlayer});
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }
    } //End of RemoveOrphanedWaypoint()

    public System.Collections.Generic.List<Waypoint> getWaypoints(ICoreServerAPI api) {
        var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;
        return waypoints;
    }

    private void StoreWaypoint(string playerUID, Waypoint wp) {
        if(!ProjectileTrackerModSystem.pendingWaypoints.ContainsKey(playerUID)) ProjectileTrackerModSystem.pendingWaypoints[playerUID] = new();
        ProjectileTrackerModSystem.pendingWaypoints[playerUID].Add(wp);
    }

    public void ProcessStoredWaypoints(string playerUID, ICoreServerAPI serverAPI) {
        if(!ProjectileTrackerModSystem.pendingWaypoints.ContainsKey(playerUID)) return;
        List<Waypoint> storedWaypoints = ProjectileTrackerModSystem.pendingWaypoints[playerUID].ToList();
        var waypoints = getWaypoints(serverAPI);

        var maplayer = serverAPI.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        foreach (Waypoint waypoint in storedWaypoints)
        {
            if(waypoint.Title.StartsWith("Projectile ")) {
                waypoints.Remove(waypoint);
            }

            try
            {
                ResendWaypoints.Invoke(maplayer, new Object[] {serverAPI.World.PlayerByUid(playerUID) as IServerPlayer});
                RebuildMapComponents.Invoke(maplayer, null);
                ProjectileTrackerModSystem.pendingWaypoints.Remove(playerUID);
            }
            catch (System.Exception) //I reckon this might happen if the player crashes on connect.
            {
                serverAPI.Logger.Log(EnumLogType.Error, "Projectile Tracker failed to send waypoints for player " + playerUID + " to the client. Stored waypoint updates have not been cleared.");
            }
        }
    }
} //End of PtWaypoint