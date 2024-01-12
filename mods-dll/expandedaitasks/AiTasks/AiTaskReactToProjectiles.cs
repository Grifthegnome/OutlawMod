using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ExpandedAiTasks.Managers;

namespace ExpandedAiTasks
{
    public class AiTaskReactToProjectiles : AiTaskBaseExpandedTargetable
    {
        private const float SKIP_BEHAVIOR_IF_PLAYER_BEYOND_RANGE = 100f;

        protected float reactRange = 3;
        protected float reactDBounce = 3.0f;

        private double lastReactTime = 0;

        private EntityProjectile reactionProjectile = null;
        private List<EntityProjectile> knownProjectiles = new List<EntityProjectile>();

        public AiTaskReactToProjectiles(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            this.reactRange = taskConfig["reactRange"].AsFloat(3);
        }

        public override bool ShouldExecute()
        {
            reactionProjectile = null;

            

            if (lastReactTime + reactDBounce > entity.World.ElapsedMilliseconds)
                return false;

            //This is a performative behavior for players, if a player isn't close enough to notice, don't do it.
            if (!AiUtility.IsAnyPlayerWithinRangeOfPos(entity.ServerPos.XYZ, SKIP_BEHAVIOR_IF_PLAYER_BEYOND_RANGE, world))
                return false;

            reactionProjectile = CheckForProjectiles();

            if ( reactionProjectile != null) 
            {
                if (reactionProjectile != null)
                {
                    EntityTargetPairing targetPairing = new EntityTargetPairing(entity, reactionProjectile.FiredBy);
                    entity.Notify("attackEntity", targetPairing);

                    AiUtility.TryNotifyHerdMembersToAttack(entity, reactionProjectile.FiredBy, AiUtility.GetHerdAlertRangeForEntity(entity), true);
                    lastReactTime = entity.World.ElapsedMilliseconds;
                }
            }

            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return false;
        }

        public override void FinishExecute(bool cancelled)
        {
            
        }
        private EntityProjectile CheckForProjectiles()
        {            
            knownProjectiles.Clear();

            List<EntityProjectile> projectilesInRange = EntityManager.GetAllEntityProjectilesInFlightWithinRangeOfPos(entity.ServerPos.XYZ, reactRange);
            knownProjectiles = FilterProjectiles(projectilesInRange);

            EntityProjectile bestProjectile = null;
            foreach ( EntityProjectile projectile in knownProjectiles )
            {
                if ( bestProjectile == null)
                {
                    bestProjectile = projectile;
                    continue;
                }
                    
                if ( projectile.FiredBy is EntityPlayer )
                {
                    if ( bestProjectile.FiredBy is EntityPlayer )
                    {
                        float bestDistSqr = bestProjectile.ServerPos.SquareDistanceTo( bestProjectile.FiredBy.ServerPos );
                        float otherDistSqr = projectile.ServerPos.SquareDistanceTo(projectile.FiredBy.ServerPos);

                        if (bestDistSqr > otherDistSqr)
                            bestProjectile = projectile;
                    }
                    else
                    {
                        bestProjectile = projectile;
                    }
                }
                else
                {
                    float bestDistSqr = bestProjectile.ServerPos.SquareDistanceTo(bestProjectile.FiredBy.ServerPos);
                    float otherDistSqr = projectile.ServerPos.SquareDistanceTo(projectile.FiredBy.ServerPos);

                    if (bestDistSqr > otherDistSqr)
                        bestProjectile = projectile;
                }
            }

            return bestProjectile;
        }

        private List<EntityProjectile> FilterProjectiles(List<EntityProjectile> projectiles )
        {
            List<EntityProjectile> filteredProjectiles = new List<EntityProjectile>();

            foreach (EntityProjectile projectile in projectiles)
            {
                if (projectile.FiredBy != null )
                {
                    if (!IsTargetableEntity(projectile.FiredBy, SKIP_BEHAVIOR_IF_PLAYER_BEYOND_RANGE ) )
                        continue;

                    if (projectile.ApplyGravity )
                    {
                        if ( AwarenessManager.IsAwareOfTarget(entity, projectile, reactRange, reactRange ) )
                        {
                            filteredProjectiles.Add(projectile);
                        }
                    }                    
                }   
            }
            
            return filteredProjectiles;
        }
    }
}
