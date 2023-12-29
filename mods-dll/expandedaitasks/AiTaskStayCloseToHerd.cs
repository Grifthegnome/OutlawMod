using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskStayCloseToHerd : AiTaskBase
    {
        protected Entity herdLeaderEntity;
        protected List<Entity> herdEnts;
        protected float moveSpeed = 0.03f;
        protected float range = 8f;
        protected float maxDistance = 3f;
        protected float arriveDistance = 3f;
        protected bool allowStrayFromHerdInCombat = true;
        protected bool allowHerdConsolidation = false;
        protected float consolidationRange = 40f;

        //Data for entities this ai is allowed to consolidate its herd with.
        protected HashSet<string> consolidationEntitiesByCodeExact = new HashSet<string>();
        protected string[] consolidationEntitiesByCodePartial = new string[0];

        protected bool stuck = false;
        protected bool stopNow = false;
        protected bool allowTeleport = true;
        protected float teleportAfterRange;

        protected Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToHerd(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            range = taskConfig["searchRange"].AsFloat(8f);
            maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            arriveDistance = taskConfig["arriveDistance"].AsFloat(3f);
            allowStrayFromHerdInCombat = taskConfig["allowStrayFromHerdInCombat"].AsBool(true);
            allowHerdConsolidation = taskConfig["allowHerdConsolidation"].AsBool(false);
            consolidationRange = taskConfig["consolidationRange"].AsFloat(40f);

            BuildConsolidationTable(taskConfig);

            allowTeleport = taskConfig["allowTeleport"].AsBool(true);
            teleportAfterRange = taskConfig["teleportAfterRange"].AsFloat(30f);

            Debug.Assert(maxDistance >= arriveDistance, "maxDistance must be greater than or equal to arriveDistance for AiTaskStayCloseToHerd on entity " + entity.Code.Path);
        }

        private void BuildConsolidationTable(JsonObject taskConfig)
        {
            if (taskConfig["consolidationEntityCodes"] != null)
            {
                string[] array = taskConfig["consolidationEntityCodes"].AsArray(new string[0]);
                
                List<string> list = new List<string>();
                foreach (string text in array)
                {
                    if (text.EndsWith("*"))
                    {
                        list.Add(text.Substring(0, text.Length - 1));
                    }
                    else
                    {
                        consolidationEntitiesByCodeExact.Add(text);
                    }
                }

                consolidationEntitiesByCodePartial = list.ToArray();
            }
        }

        public override bool ShouldExecute()
        {
            if (whenInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenInEmotionState) == false)
                    return false;
            }

            if (whenNotInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenNotInEmotionState) == true)
                    return false;
            }

            //Check if we are in combat, allow us to stray from herd.
            if (allowStrayFromHerdInCombat && AiUtility.IsInCombat(entity))
            {
                return false;
            }

            //Try to get herd ents from saved master list.
            herdEnts = AiUtility.GetMasterHerdList(entity);

            if (herdEnts.Count == 0)
            {
                //Get all herd members.
                herdEnts = new List<Entity>();
                entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, (ent) =>
                {
                    if (ent is EntityAgent)
                    {
                        EntityAgent agent = ent as EntityAgent;
                        if (agent.Alive && agent.HerdId == entity.HerdId)
                            herdEnts.Add(agent);
                    }

                    return false;
                });

                //Set new master list.
                AiUtility.SetMasterHerdList(entity, herdEnts);
            }

            //If we can consolidate herds and we are the last one left or our herd is at half strength.
            if (allowHerdConsolidation)
            {
                double herdAliveRatio = AiUtility.PercentOfHerdAlive(herdEnts);

                if ( herdAliveRatio <= 0.5 || herdEnts.Count == 1)
                {
                    Entity newHerdMember = entity.World.GetNearestEntity(entity.ServerPos.XYZ, consolidationRange, consolidationRange, (ent) =>
                    {

                        if (ent is EntityAgent)
                        {
                            //If this Ai is a valid Ai whose herd we can join, we can try to join the herd.
                            EntityAgent agent = ent as EntityAgent;
                            if (CanJoinThisEntityInHerd(agent))
                                return true;
                        }

                        return false;
                    });

                    if (newHerdMember is EntityAgent)
                    {
                        AiUtility.JoinSameHerdAsEntity(entity, newHerdMember);
                        herdLeaderEntity = null;
                        herdEnts = null;
                        return false;
                    }
                }                
            }

            if (herdLeaderEntity == null || !herdLeaderEntity.Alive || entity == herdLeaderEntity)
            {
                //Determine who the herd leader is
                long bestEntityId = entity.EntityId;
                Entity bestCanidate = entity;
                foreach ( Entity herdMember in herdEnts )
                {

                    if ( !herdMember.Alive )
                        continue;

                    //Prioritize a herd member who is in combat, otherwise go with the lowest ent index in the herd.
                    if ( herdMember.EntityId < bestEntityId && !AiUtility.IsInCombat( bestCanidate ) )
                    {
                        bestEntityId = herdMember.EntityId;
                        bestCanidate = herdMember;
                    }
                }

                //Set herd leader
                if (bestCanidate != null)
                    herdLeaderEntity = bestCanidate;
            }

            //If we are the herd leader, then we lead the herd.
            if (entity == herdLeaderEntity)
                return false;

            if (herdLeaderEntity != null && (!herdLeaderEntity.Alive || herdLeaderEntity.ShouldDespawn)) 
                herdLeaderEntity = null;
            
            if (herdLeaderEntity == null) 
                return false;

            //if (pathTraverser.Active == true)
            //    return false;

            double x = herdLeaderEntity.ServerPos.X;
            double y = herdLeaderEntity.ServerPos.Y;
            double z = herdLeaderEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = herdLeaderEntity.SelectionBox.XSize;
           
            pathTraverser.WalkTowards(herdLeaderEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
            stopNow = false;
        }


        public override bool ContinueExecute(float dt)
        {
            if (herdLeaderEntity == null)
                return false;

            double x = herdLeaderEntity.ServerPos.X + targetOffset.X;
            double y = herdLeaderEntity.ServerPos.Y;
            double z = herdLeaderEntity.ServerPos.Z + targetOffset.Z;

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            float distSqr = entity.ServerPos.SquareDistanceTo(x, y, z);

            if (distSqr < arriveDistance * arriveDistance)
            {
                pathTraverser.Stop();
                return false;
            }

            if (stuck && allowTeleport && distSqr > teleportAfterRange * teleportAfterRange)
            {
                AiUtility.TryTeleportToEntity(entity, herdLeaderEntity);
            }

            return !stuck && !stopNow && pathTraverser.Active && herdLeaderEntity != null && herdLeaderEntity.Alive;
        }

        public virtual bool CanJoinThisEntityInHerd(EntityAgent herdMember)
        {
            if (!herdMember.Alive || !herdMember.IsInteractable || herdMember.EntityId == entity.EntityId || herdMember.HerdId == entity.HerdId)
            {
                return false;
            }

            if (consolidationEntitiesByCodeExact.Contains(herdMember.Code.Path))
            {
                return true;
            }

            for (int i = 0; i < consolidationEntitiesByCodePartial.Length; i++)
            {
                if (herdMember.Code.Path.StartsWithFast(consolidationEntitiesByCodePartial[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }

        protected void OnStuck()
        {
            stuck = true;
            
            if ( allowTeleport )
                AiUtility.TryTeleportToEntity(entity, herdLeaderEntity);
            
            pathTraverser.Stop();
        }

        public void OnPathFailed()
        {
            stopNow = true;

            if (allowTeleport)
                AiUtility.TryTeleportToEntity(entity, herdLeaderEntity);

            pathTraverser.Stop();
        }

        protected void OnGoalReached()
        {
            pathTraverser.Stop();
        }

        public override bool Notify(string key, object data)
        {
            
            if (key == "haltMovement")
            {
                //If another task has requested we halt, stop moving to herd leader.
                if (entity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }

            return false;
        }
    }
}
