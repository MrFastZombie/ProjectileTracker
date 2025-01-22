using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using System.Collections.Generic;

namespace ProjectileTracker.EntityBehavior {
    using EntityBehavior = Vintagestory.API.Common.Entities.EntityBehavior;
    public class InjectProjectileTracker : EntityBehavior {
        private static Dictionary<long, bool> projectileLanded = new();

        private static PtWaypoint ptWaypoint = new();
        private static PtEntity ptEntity;

        public InjectProjectileTracker(Entity entity) : base(entity) {
            
        }
        public override string PropertyName()
        {
            return "InjectProjectileTracker";
        } 

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            if(entity == null) return;
            ptEntity = new PtEntity(entity);
            // checkArrow.GetType().FullName -> "CombatOverhaul.RangedSystems.ProjectileEntity"
            projectileLanded.Add(ptEntity.EntityId, false);
        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ptEntity = new PtEntity(entity);
            base.OnEntityDespawn(despawn);

            if(ptEntity.Alive) return; //On world load entities often report being despawned when they are not.
            if(ptEntity.Api.Server.CurrentRunPhase != EnumServerRunPhase.RunGame) return;
            ptWaypoint.RemoveWaypoint(ptEntity.Api, ptEntity);
        }

        public override void OnGameTick(float deltaTime)
        {
            ptEntity = new PtEntity(entity);
            base.OnGameTick(deltaTime);

            //if(checkArrow.GetType().GetProperty("FiredBy") == null) return;
            if(ptEntity == null) return; //No point in making a waypoint if the player is null.
            if(ptEntity.State == EnumEntityState.Inactive) return;  //This will ensure that if a projectile exits the simulation distance that it will not create a waypoint until it is loaded again and lands.
            if(projectileLanded.ContainsKey(ptEntity.EntityId) == false) return;

            if(projectileLanded[ptEntity.EntityId] == true) return;
            else {
                //if(checkArrow.ServerPos.XYZ == checkArrow.PreviousServerPos.XYZ) { //This used to be pretty accurate, has suddenly become too sensitive so I had to change to checking ApplyGravity.
                if(!ptEntity.ApplyGravity) {
                    projectileLanded[ptEntity.EntityId] = true;
                    ptWaypoint.CreateWaypoint(ptEntity.Api, ptEntity);
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