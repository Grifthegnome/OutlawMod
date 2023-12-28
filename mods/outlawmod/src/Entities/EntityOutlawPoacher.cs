using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ExpandedAiTasks;

namespace OutlawMod
{
    public class EntityOutlawPoacher : EntityOutlaw
    {
        List<long> callbacks = new List<long>();

        const int HOUND_COMPANIONS_MIN = 1;
        const int HOUND_COMPANIONS_MAX = 3;
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
        
            //If we get despawned by debug stuff, don't spawn out hounds.
            if ( this == null || !this.Alive )
                return;

            int houndToSpawn = this.World.Rand.Next(HOUND_COMPANIONS_MIN, HOUND_COMPANIONS_MAX);

            for( int i = 0; i < houndToSpawn; i++ )
            {
                AttemptSpawnHuntingHound(0f);
            }
        }

        protected void AttemptSpawnHuntingHound(float dt)
        {
            if( Api.Side == EnumAppSide.Server)
            {
                //Don't spawn hounds if hounds are disabled.
                if (!Api.World.Config.GetBool("enableHuntingHounds", true))
                    return;

                AssetLocation code = new AssetLocation("hound-hunting");
                if (code == null)
                    return;

                EntityProperties houndProperties = this.World.GetEntityType(code);

                Debug.Assert(houndProperties != null, "Hound Properties are null");

                Cuboidf collisionBox = houndProperties.SpawnCollisionBox;

                // Delay hound spawning if we're colliding
                if (this.World.CollisionTester.IsColliding(this.World.BlockAccessor, collisionBox, this.ServerPos.XYZ, false))
                {
                    long callbackId = this.World.RegisterCallback(AttemptSpawnHuntingHound, 3000);
                    callbacks.Add(callbackId);
                    return;
                }

                Entity houndEnt = this.World.ClassRegistry.CreateEntity(houndProperties);

                if (houndEnt == null)
                    return;
            
                houndEnt.ServerPos.SetFrom(this.ServerPos);
                houndEnt.Pos.SetFrom(houndEnt.ServerPos);

                if (houndEnt is EntityAgent)
                {
                    EntityAgent houndAgent = (EntityAgent)houndEnt;
                    houndAgent.HerdId = this.HerdId;

                    AiUtility.SetGuardedEntity(houndAgent, this);
                }

                this.World.SpawnEntity(houndEnt);
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawnData )
        {

            base.OnEntityDespawn( despawnData );

            foreach( long callbackId in callbacks )
            {
                this.World.UnregisterCallback(callbackId);
            }
        }

    }   
}
