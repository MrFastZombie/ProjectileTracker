using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using System.Resources;

namespace ProjectileTracker;

class PtEntity {
    public PtEntity(dynamic entity) {
        Entity = entity;
        Valid = true;

        if(entity.GetType().FullName == "Vintagestory.GameContent.EntityProjectile") {
            if(entity.Api == null) Api = null;
            else Api = entity.Api as ICoreServerAPI;
            if(Entity.FiredBy == null) Player = null;
            else Player = Entity.FiredBy.Player as IServerPlayer;
            EntityId = entity.EntityId;
            Pos = Entity.ServerPos.XYZ;
            Alive = entity.Alive;
            ApplyGravity = entity.ApplyGravity;
            Path = entity.Code.Path;
            State = entity.State;
        } 
        else if(entity.GetType().FullName == "CombatOverhaul.RangedSystems.ProjectileEntity") {
            if(entity.Api == null) Api = null;
            else Api = entity.Api as ICoreServerAPI;
            if(Api.World.GetEntityById(Entity.ShooterId) == null) Player = null;
            else Player = Api.World.GetEntityById(Entity.ShooterId).Player as IServerPlayer;
            EntityId = entity.EntityId;
            Pos = Entity.ServerPos.XYZ;
            Alive = entity.Alive;
            ApplyGravity = entity.ApplyGravity;
            Path = entity.Code.Path;
            State = entity.State;
        }

        if(Api == null || Player == null || Pos == null || Path == null ) Valid = false;

    }

    public ICoreServerAPI Api { get; set; }

    public IServerPlayer Player { get; set; }

    public long EntityId { get; set;}

    public Vintagestory.API.MathTools.Vec3d Pos { get; set; }

    public dynamic Entity { get; set; }

    public string Path { get; set; }

    public bool Alive { get; set; }
    public bool ApplyGravity { get; set; }

    public EnumEntityState State { get; set; }

    public bool Valid { get; }

}