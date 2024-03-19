using ProjectileTracker.EntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System.Linq;
using System;
using System.Reflection;
using ProtoBuf;
using Vintagestory.API.Util;
using System.Security.Cryptography.X509Certificates;
using Vintagestory.ServerMods;
using Newtonsoft.Json;

namespace ProjectileTracker;
class PtWaypoint
{
    public PtWaypoint() { }

    private static readonly MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
    
    public void CreateWaypoint(ICoreServerAPI api, EntityProjectile p) {
        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
            var player = (p.FiredBy as EntityPlayer).Player as IServerPlayer;
            Waypoint newWaypoint = new() {
                Position = p.ServerPos.XYZ,
                Title = "Projectile " + p.EntityId,
                Pinned = false,
                Icon = "ptarrow",
                Color = ColorUtil.Hex2Int("#b55aed"),
                OwningPlayerUid = player.PlayerUID
            };

            maplayer.AddWaypoint(newWaypoint, (p.FiredBy as EntityPlayer).Player as IServerPlayer);
    }
    
    public void RemoveWaypoint(ICoreServerAPI api, EntityProjectile p) {
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
                if(player == null) {
                    api.Logger.Log(EnumLogType.Error, "Projectile " + p.EntityId + " had a waypoint for player " + waypoint.OwningPlayerUid + " but that player could not be retrieved. If this happened during a world load, this can be ignored!");
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
                    continue;
                }

                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] {player as IServerPlayer});
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }
    } //End of RemoveOrphanedWaypoint()

    private System.Collections.Generic.List<Waypoint> getWaypoints(ICoreServerAPI api) {
        var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;
        return waypoints;
    }
} //End of PtWaypoint