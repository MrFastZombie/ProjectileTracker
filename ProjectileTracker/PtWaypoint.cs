using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Config;

namespace ProjectileTracker;
class PtWaypoint
{
    public PtWaypoint() { }

    private static readonly MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
    
    // ------------------------------------------------------------------------------------Public Functions------------------------------------------------------------------------------------
    
    /// <summary>
    /// Creates a projectile waypoint.
    /// </summary>
    /// <param name="api">Server's api.</param>
    /// <param name="p">The projectile to create a waypoint for.</param>
    public void CreateWaypoint(ICoreServerAPI api, PtEntity p) {
        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var player = p.Player;
            if(player == null) return;
        string path = p.Path;
        Ptconfig playerConfig = ProjectileTrackerModSystem.clientConfigs[player.PlayerUID];

        if(path == null) return;
        if(playerConfig == null || playerConfig.projectileBlacklist.Contains(path) || !playerConfig.EnableProjectileTracker) return; //Don't create a waypoint if either the player does not have the mod or if they disabled the mod in the config

        Waypoint newWaypoint = new() {
            Position = p.Pos,
            Title = "Projectile " + p.EntityId,
            Pinned = false,
            Icon = playerConfig.icon,
            Color = System.Drawing.ColorTranslator.FromHtml(playerConfig.color).ToArgb(), //Accepts HTML hex color codes.
            OwningPlayerUid = player.PlayerUID
        };

        maplayer.AddWaypoint(newWaypoint, p.Player);
    } // End of CreateWaypoint()
    
    /// <summary>
    /// Removes a projectile waypoint.
    /// </summary>
    /// <param name="api">Server's api</param>
    /// <param name="p">Projectile to remove a waypoint for.</param>
    /// <param name="forcestore">Debug parameter that allows you to force a waypoint to be stored instead of being sent directly to the player.</param>
    public void RemoveWaypoint(ICoreServerAPI api, PtEntity p, bool forcestore = false) {
        if(p == null) return;
        if(p.Player == null) {
                RemoveOrphanedWaypoint(api, p);
                return; //When the world is reloaded, FiredBy becomes null on saved projectile entities.
        }

        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = getWaypoints(api);
        var player = p.Player;

        foreach (Waypoint waypoint in waypoints.ToList().Where(w => w.OwningPlayerUid == player.PlayerUID)) //For every waypoint the player owns, check if it relates the the projectile and remove it if so.
        {
            if(waypoint.Title == "Projectile " + p.EntityId) {
                if(player == null || forcestore) {
                    api.Logger.Log(EnumLogType.Error, Lang.Get("projectiletracker:remove-error", p.EntityId, waypoint.OwningPlayerUid));
                    StoreWaypoint(waypoint.OwningPlayerUid, waypoint);
                    continue;
                }

                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] { player });
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }
    } //End of RemoveWaypoint()

    /// <summary>
    /// Retrives the list of waypoints from the server's map layer.
    /// </summary>
    /// <param name="api">Server's api.</param>
    /// <returns></returns>
    public System.Collections.Generic.List<Waypoint> getWaypoints(ICoreServerAPI api) {
        var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;
        return waypoints;
    } //End of getWaypoints()

    /// <summary>
    /// Removes all stored waypoints for a player and sends the updates to the player.
    /// </summary>
    /// <param name="playerUID">UID of the player.</param>
    /// <param name="serverAPI">Server's API.</param>
    public void ProcessStoredWaypoints(string playerUID, ICoreServerAPI serverAPI) {
        if(!ProjectileTrackerModSystem.pendingWaypointNames.ContainsKey(playerUID)) return;
        List<string> wpnames = ProjectileTrackerModSystem.pendingWaypointNames[playerUID].ToList();
        var maplayer = serverAPI.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = getWaypoints(serverAPI);
        bool success = true;

        foreach (Waypoint waypoint in waypoints.ToList()) {
            if(wpnames.Contains(waypoint.Title) && waypoint.OwningPlayerUid == playerUID) {
                waypoints.Remove(waypoint);
            }
        }

        try
        {
            ResendWaypoints.Invoke(maplayer, new Object[] {serverAPI.World.PlayerByUid(playerUID) as IServerPlayer});
            RebuildMapComponents.Invoke(maplayer, null);
        }
        catch (System.Exception) //I reckon this might happen if the player crashes on connect.
        {
            success = false;
            serverAPI.Logger.Log(EnumLogType.Error, Lang.Get("projectiletracker:process-error", playerUID));
        }

        if(success) ProjectileTrackerModSystem.pendingWaypointNames.Remove(playerUID);
    } //End of ProcessStoredWaypoints()

    // ------------------------------------------------------------------------------------Helper Functions------------------------------------------------------------------------------------

    /// <summary>
    /// Until https://github.com/anegostudios/VintageStory-Issues/issues/3723 is fixed, this method will remove waypoints for orphaned projectiles.
    /// </summary>
    /// <param name="api">Server's api.</param>
    /// <param name="p">The Projectile that may have a waypoint.</param>
    private void RemoveOrphanedWaypoint(ICoreServerAPI api, PtEntity p) {
        var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
        var waypoints = getWaypoints(api);

        if(p == null) return;
        
        foreach (Waypoint waypoint in waypoints.ToList())
        {
            if(waypoint.Title == "Projectile " + p.EntityId) {
                var player = api.World.PlayerByUid(waypoint.OwningPlayerUid);
                if(player == null) {
                    api.Logger.Log(EnumLogType.Error, Lang.Get("projectiletracker:remove-error-orphan", p.EntityId, waypoint.OwningPlayerUid));
                    StoreWaypoint(waypoint.OwningPlayerUid, waypoint);
                    continue;
                }

                waypoints.Remove(waypoint);
                ResendWaypoints.Invoke(maplayer, new Object[] {player as IServerPlayer});
                RebuildMapComponents.Invoke(maplayer, null);
            }
        }
    } //End of RemoveOrphanedWaypoint()

    /// <summary>
    /// Stores a waypoint update so that it can be sent to the client later. For when the client that owns a waypoint is not online.
    /// </summary>
    /// <param name="playerUID">UID of the player.</param>
    /// <param name="wp">Waypoint that needs to be stored.</param>
    private static void StoreWaypoint(string playerUID, Waypoint wp) {
        if(!ProjectileTrackerModSystem.pendingWaypointNames.ContainsKey(playerUID)) ProjectileTrackerModSystem.pendingWaypointNames[playerUID] = new();
        ProjectileTrackerModSystem.pendingWaypointNames[playerUID].Add(wp.Title);
    } //End of StoreWaypoint()

} //End of PtWaypoint