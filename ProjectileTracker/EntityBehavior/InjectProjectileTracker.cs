using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
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
            dynamic checkArrow = entity;
                if(checkArrow == null) return;
                if(checkArrow.Api == null) return;

            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnEntitySpawn();
            // checkArrow.GetType().FullName -> "CombatOverhaul.RangedSystems.ProjectileEntity"
            projectileLanded.Add(checkArrow.EntityId, false);
        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            dynamic checkArrow = entity;
                if(checkArrow == null) return;
                if(checkArrow.Api == null) return;

            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnEntityDespawn(despawn);
            
            if(checkArrow.Alive) return; //On world load entities often report being despawned when they are not.
            ptWaypoint.RemoveWaypoint(api, checkArrow);
        }

        public override void OnGameTick(float deltaTime)
        {
            dynamic checkArrow = entity;
                if(checkArrow == null) return;
                if(checkArrow.Api == null) return;

            ICoreServerAPI api = entity.Api as ICoreServerAPI;
            base.OnGameTick(deltaTime);

            if(checkArrow.GetType().GetProperty("FiredBy") == null) return;
            if(checkArrow.FiredBy == null) return; //No point in making a waypoint if the player is null.
            if(checkArrow.State == EnumEntityState.Inactive) return;  //This will ensure that if a projectile exits the simulation distance that it will not create a waypoint until it is loaded again and lands.
            if(projectileLanded.ContainsKey(checkArrow.EntityId) == false) return;

            if(projectileLanded[checkArrow.EntityId] == true) return;
            else {
                //if(checkArrow.ServerPos.XYZ == checkArrow.PreviousServerPos.XYZ) { //This used to be pretty accurate, has suddenly become too sensitive so I had to change to checking ApplyGravity.
                if(!checkArrow.ApplyGravity) {
                    projectileLanded[checkArrow.EntityId] = true;
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