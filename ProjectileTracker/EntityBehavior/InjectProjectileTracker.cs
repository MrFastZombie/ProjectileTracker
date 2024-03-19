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
        private static Dictionary<long, bool> projectileLanded = new();

        private static PtWaypoint ptWaypoint = new();

        public InjectProjectileTracker(Entity entity) : base(entity) {
            
        }
        public override string PropertyName()
        {
            return "InjectProjectileTracker";
        } 

        public override void OnEntitySpawn()
        {
            EntityProjectile checkArrow = entity as EntityProjectile;
            ICoreServerAPI api = entity.Api as ICoreServerAPI;
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
            if(ProjectileTrackerModSystem.serverLoaded == false) return;
            api.Logger.Log(EnumLogType.Debug, "Projectile Despawned: " + checkArrow.EntityId);
            //Remove waypoint referring to this projectile entity if it exists.
            //RemoveWaypoint(api, checkArrow);
            ptWaypoint.RemoveWaypoint(api, checkArrow);
        }

        public override void OnGameTick(float deltaTime)
        {
            EntityProjectile checkArrow = entity as EntityProjectile;
            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnGameTick(deltaTime);

            if(checkArrow.FiredBy == null) return; //No point in making a waypoint if the player is null.
            if(checkArrow.State == EnumEntityState.Inactive) return;  //This will ensure that if a projectile exits the simulation distance that it will not create a waypoint until it is loaded again and lands.

            //Figuring out how to make this work drove me nuts, but this appears to be the most reliable way to check if the projectile has landed without access to onCollided.
            if(projectileLanded[checkArrow.EntityId] == true) return;
            else {
                if(checkArrow.ServerPos.XYZ == checkArrow.PreviousServerPos.XYZ) {
                    api.Logger.Log(EnumLogType.Debug, "Projectile" + checkArrow.EntityId + " has landed");
                    projectileLanded[checkArrow.EntityId] = true;
                    //CreateWaypoint(api, checkArrow);
                    ptWaypoint.CreateWaypoint(api, checkArrow);
                }
            }

            /* This should work, but it only detects when the projectile lands on the top face of a block.
            if(checkArrow.Collided == true) { //This seems to only detect the collision as it happens, so there is no need to check if the collision already happened.
                api.Logger.Log(EnumLogType.Event, "Detected a projectile collision on projectile with ID: " + checkArrow.EntityId);
                //Add the waypoint here.
                CreateWaypoint(api, checkArrow);
            }*/
        }
    }
}