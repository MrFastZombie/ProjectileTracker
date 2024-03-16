using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Util;
using System.Timers;
using System;
using System.Linq;
using ProjectileTracker;
using System.Reflection;
using System.Collections.Generic;

namespace ProjectileTracker.EntityBehavior {
    using EntityBehavior = Vintagestory.API.Common.Entities.EntityBehavior;
    public class InjectProjectileTracker : EntityBehavior {
        private static readonly MethodInfo ResendWaypoints = typeof(WaypointMapLayer).GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo RebuildMapComponents = typeof(WaypointMapLayer).GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Dictionary<long, bool> projectileLanded = new();

        public InjectProjectileTracker(Entity entity) : base(entity) {
            
        }
        public override string PropertyName()
        {
            return "InjectProjectileTracker";
        } 

        public override void OnEntitySpawn()
        {
            EntityProjectile checkArrow = entity as EntityProjectile;
            IServerAPI api = entity.Api as IServerAPI;
            base.OnEntitySpawn();
            projectileLanded.Add(checkArrow.EntityId, false);

            //ERROR: An exception of type 'System.NullReferenceException' occurred in ProjectileTracker.dll but was not handled in user code: 'Object reference not set to an instance of an object.'
            //api.Logger.Log(EnumLogType.Event, player.GetName() + " fired a projectile with ID: " + checkArrow.EntityId);
        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            EntityProjectile checkArrow = entity as EntityProjectile;
            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnEntityDespawn(despawn);
            api.Logger.Log(EnumLogType.Debug, "Projectile Despawned: " + checkArrow.EntityId);
            //Remove waypoint referring to this projectile entity if it exists.
            RemoveWaypoint(api, checkArrow);
        }

        public override void OnGameTick(float deltaTime)
        {
            EntityProjectile checkArrow = entity as EntityProjectile;
            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnGameTick(deltaTime);

            if(checkArrow.FiredBy == null) return; //No point in making a waypoint if the player is null.

            //Figuring out how to make this work drove me nuts, but this appears to be the most reliable way to check if the projectile has landed without access to onCollided.
            if(projectileLanded[checkArrow.EntityId] == true) return;
            else {
                if(checkArrow.ServerPos.XYZ == checkArrow.PreviousServerPos.XYZ) {
                    api.Logger.Log(EnumLogType.Debug, "Projectile" + checkArrow.EntityId + " has landed");
                    projectileLanded[checkArrow.EntityId] = true;
                    CreateWaypoint(api, checkArrow);
                }
            }

            /* This should work, but it only detects when the projectile lands on the top face of a block.
            if(checkArrow.Collided == true) { //This seems to only detect the collision as it happens, so there is no need to check if the collision already happened.
                api.Logger.Log(EnumLogType.Event, "Detected a projectile collision on projectile with ID: " + checkArrow.EntityId);
                //Add the waypoint here.
                CreateWaypoint(api, checkArrow);
            }*/
        }

        private System.Collections.Generic.List<Waypoint> getWaypoints(ICoreServerAPI api) {
            var waypoints = (api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer).Waypoints;
            return waypoints;
        }

        private static void CreateWaypoint(ICoreServerAPI api, EntityProjectile p) {
            var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
            var player = (p.FiredBy as EntityPlayer).Player as IServerPlayer;
            Waypoint newWaypoint = new() {
                Position = p.ServerPos.XYZ,
                Title = "Projectile " + p.EntityId,
                Pinned = false,
                Icon = "circle",
                Color = 0,
                OwningPlayerUid = player.PlayerUID
            };

            maplayer.AddWaypoint(newWaypoint, (p.FiredBy as EntityPlayer).Player as IServerPlayer);
        }

        private void RemoveWaypoint(ICoreServerAPI api, EntityProjectile p) {
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
                    waypoints.Remove(waypoint);
                    ResendWaypoints.Invoke(maplayer, new Object[] { player });
                    RebuildMapComponents.Invoke(maplayer, null);
                }
            }
        }

        //Until https://github.com/anegostudios/VintageStory-Issues/issues/3723 is fixed, this method will remove orphaned waypoints.
        private void RemoveOrphanedWaypoint(ICoreServerAPI api, EntityProjectile p) {
            var maplayer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;
            var waypoints = getWaypoints(api);
            
            foreach (Waypoint waypoint in waypoints.ToList())
            {
                if(waypoint.Title == "Projectile " + p.EntityId) {
                    waypoints.Remove(waypoint);
                    ResendWaypoints.Invoke(maplayer, new Object[] {api.World.PlayerByUid(waypoint.OwningPlayerUid) as IServerPlayer});
                    RebuildMapComponents.Invoke(maplayer, null);
                }
            }
        }
    }
}