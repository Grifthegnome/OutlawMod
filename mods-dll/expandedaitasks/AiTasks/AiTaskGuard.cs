using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Xml.XPath;
using System.Drawing;


namespace ExpandedAiTasks
{
    public class AiTaskGuard : AiTaskBaseExpandedTargetable
    {
        protected Entity guardedEntity;
        protected Entity attacker;
        protected float attackerStartTargetMs = 0;

        float detectionDistance = 20f;
        float maxDistance = 6f;
        float arriveDistance = 3f;
        float moveSpeedNear = 0.04f;
        float moveSpeedFarAway = 0.04f;
        float guardAgroDurationMs = 30000f;
        float guardAgroChaseDist = 40f;

        string moveNearAnimation = "Walk";
        string moveFarAnimation = "Run";

        bool guardHerd = false;
        bool aggroOnProximity = false;
        float aggroProximity = 5f;

        protected bool guardTargetSwimmingLastFrame = false;

        protected bool stuck = false;
        protected bool stopNow = false;
        protected bool allowTeleport = true;
        protected float teleportAfterRange;

        protected Vec3d targetOffset = new Vec3d();

        //Guarding is not an agressive action.
        public override bool AggressiveTargeting => false;

        float stepHeight;

        public AiTaskGuard(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            detectionDistance = taskConfig["detectionDistance"].AsFloat(20f);
            maxDistance = taskConfig["maxDistance"].AsFloat(6f);
            arriveDistance = taskConfig["arriveDistance"].AsFloat(3f);
            moveSpeedNear = taskConfig["moveSpeed"].AsFloat(0.006f);
            moveSpeedFarAway = taskConfig["moveSpeedFarAway"].AsFloat(0.04f);
            guardAgroDurationMs = taskConfig["guardAgroDurationMs"].AsFloat(30000f);
            guardAgroChaseDist = taskConfig["guardAgroChaseDist"].AsFloat(40f);

            moveNearAnimation = taskConfig["moveNearAnimation"].AsString("Walk");
            moveFarAnimation = taskConfig["moveFarAnimation"].AsString("Run");

            guardHerd = taskConfig["guardHerd"].AsBool(false);
            aggroOnProximity = taskConfig["aggroOnProximity"].AsBool(false);
            aggroProximity = taskConfig["aggroProximity"].AsFloat(5f);

            allowTeleport = taskConfig["allowTeleport"].AsBool(true);
            teleportAfterRange = taskConfig["teleportAfterRange"].AsFloat(15f);

            Debug.Assert(maxDistance >= arriveDistance, "maxDistance must be greater than or equal to arriveDistance for AiTaskGuard on entity " + entity.Code.Path);
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

            //If we have been directly attacked, skip guarding.
            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > guardAgroDurationMs)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5 && IsTargetableEntity(attackedByEntity, detectionDistance, true))
            {
                //Treat our attacker as if they attacked our guard target.
                attacker = attackedByEntity;
                attackerStartTargetMs = entity.World.ElapsedMilliseconds;
                TrySendGuardedEntityAttackedNotfications(attacker);
                return false;
            }

            //Get the individual we have to guard, if they exist.
            guardedEntity = GetGuardedEntity();

            if (guardHerd && ( guardedEntity == null || !guardedEntity.Alive) )
            {

                UpdateHerdCount();

                if ( guardedEntity == null )
                    guardedEntity = GetBestGuardTargetFromHerd();
            }

            if (guardedEntity == null || !guardedEntity.Alive)
                return false;

            //If someone has attacked our guard entity, tell our targeting behaviors to target the enemy and early out.
            if (attacker == null || !attacker.Alive)
            {
                attacker = partitionUtil.GetNearestInteractableEntity(guardedEntity.ServerPos.XYZ, detectionDistance, (e) => IsThreateningGuardedTarget(e, detectionDistance));
                attackerStartTargetMs = entity.World.ElapsedMilliseconds;
            }

            double distToGuardedEntSqr = entity.ServerPos.SquareDistanceTo(guardedEntity.ServerPos.XYZ);

            if ( attacker != null)
            {
                if (distToGuardedEntSqr <= guardAgroChaseDist * guardAgroChaseDist)
                {
                    if ( entity.World.ElapsedMilliseconds <= attackerStartTargetMs + guardAgroDurationMs)
                    {
                        TrySendGuardedEntityAttackedNotfications(attacker);
                        return false;
                    }                    
                }

                //Tell other tasks to clear guard target data.
                attacker = null;
                SendGuardChaseStopNotfications();
                     
            }
            else
            {
                //Tell other tasks to clear guard target data.
                attacker = null;
                SendGuardChaseStopNotfications();
            }

            return distToGuardedEntSqr > maxDistance * maxDistance;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            stepHeight = bh == null ? 0.6f : bh.stepHeight;

            float size = guardedEntity.SelectionBox.XSize;

            guardTargetSwimmingLastFrame = guardedEntity.Swimming || guardedEntity.FeetInLiquid;

            PlayBestMoveAnimation();
            pathTraverser.NavigateTo_Async(guardedEntity.ServerPos.XYZ, GetBestMoveSpeed(), size + 0.2f, OnGoalReached, OnStuck, OnPathFailed, 5000 );

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
            stopNow = false;
        }

        protected float GetBestMoveSpeed()
        {
            double distSqr = entity.ServerPos.SquareDistanceTo( guardedEntity.ServerPos.XYZ );
            float size = entity.SelectionBox.XSize;

            if (size < 0)
                size *= 1;

            if (distSqr > ( maxDistance + size) * ( maxDistance + size) )
                return moveSpeedFarAway;

            return moveSpeedNear;
        }

        protected void PlayBestMoveAnimation()
        {
            double distSqr = entity.ServerPos.SquareDistanceTo(guardedEntity.ServerPos.XYZ);
            float size = entity.SelectionBox.XSize;

            if (size < 0)
                size *= 1;

            if (distSqr > (maxDistance + size) * (maxDistance + size))
            {
                if (moveFarAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = moveFarAnimation, Code = moveFarAnimation }.Init());

                if (moveNearAnimation != null)
                    entity.AnimManager.StopAnimation(moveNearAnimation);
            }
            else
            {
                if (moveNearAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = moveNearAnimation, Code = moveNearAnimation }.Init());

                if (moveFarAnimation != null)
                    entity.AnimManager.StopAnimation(moveFarAnimation);
            }  
        }

        protected EntityAgent GetBestGuardTargetFromHerd()
        {
            EntityAgent bestGuardTarget = null;
            double bestGuardDistSqr = -1.0;

            foreach( EntityAgent herdMember in herdMembers)
            {
                //We can only guard herd members that are set to be guardable.
                if (!IsGuardableEntity(herdMember))
                    continue;

                if ( bestGuardTarget != null )
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo( herdMember.ServerPos.XYZ );
                    if ( distSqr < bestGuardDistSqr )
                    {
                        bestGuardTarget = herdMember;
                        bestGuardDistSqr = distSqr;
                    }
                }
                else
                {
                    bestGuardTarget = herdMember;
                    bestGuardDistSqr = entity.ServerPos.XYZ.SquareDistanceTo( herdMember.ServerPos.XYZ );
                }
            }

            return bestGuardTarget;
        }

        
        public bool IsThreateningGuardedTarget(Entity ent, float range)
        {
            if (!base.IsTargetableEntity(ent, range, true)) 
                return false;

            //Don't aggro if herd member.
            if (ent is EntityAgent)
            {
                EntityAgent agent = ent as EntityAgent;
                if (agent.HerdId == entity.HerdId)
                    return false;
            }

            //If we will aggro if anything gets too close, (even if it's not in a hositle beahvior).
            if (aggroOnProximity && IsProximityTarget(ent))
            {
                double distSqr = ent.ServerPos.XYZ.SquareDistanceTo( guardedEntity.ServerPos.XYZ );
                if (distSqr <= aggroProximity * aggroProximity)
                    return true;
            }

            //If this entity is the last entity to attack our guarded target and attacked in the our specified time window.
            double lastTimeAttackedMs = AiUtility.GetLastTimeAttackedMs(guardedEntity);
            if (AiUtility.GetLastAttacker(guardedEntity) == ent && entity.World.ElapsedMilliseconds <= lastTimeAttackedMs + guardAgroDurationMs)
                return true;

            //If entity is an Ai with hostile intentions.
            var tasks = ent.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.ActiveTasksBySlot;
            return tasks?.FirstOrDefault(task => {
                return task is AiTaskBaseTargetable at && at.TargetEntity == guardedEntity && at.AggressiveTargeting;
            }) != null;
        }

        protected bool IsProximityTarget( Entity ent)
        {
            if (ent is EntityPlayer)
                return true;

            return false;
        }

        float targetUpdateTime = 0f;
        float nextTargetCheckTime = 0f;
        const float GUARD_TARGET_CHECK_INTERVAL = 0.25f;

        public override bool CanContinueExecute()
        {
            return pathTraverser.Ready;
        }

        public override bool ContinueExecute(float dt)
        {
            targetUpdateTime += dt;

            if (guardedEntity == null || !guardedEntity.Alive)
                return false;

            if (nextTargetCheckTime <= targetUpdateTime)
            {
                //If someone has attacked our guard entity, tell our targeting behaviors to target the enemy and early out.
                Entity attacker = partitionUtil.GetNearestInteractableEntity(guardedEntity.ServerPos.XYZ, maxDistance, (e) => IsThreateningGuardedTarget(e, detectionDistance));
                if (attacker != null)
                {
                    TrySendGuardedEntityAttackedNotfications(attacker);
                    return false;
                }
                else
                {
                    //Tell other tasks to clear guard target data.
                    SendGuardChaseStopNotfications();
                }

                nextTargetCheckTime = targetUpdateTime + GUARD_TARGET_CHECK_INTERVAL;
            }

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
            double x = guardedEntity.ServerPos.X + targetOffset.X;
            double y = guardedEntity.ServerPos.Y;
            double z = guardedEntity.ServerPos.Z + targetOffset.Z;

            Vec3d guardPos = new Vec3d(x, y, z);
            Vec3d guardPosClamped = AiUtility.ClampPositionToGround(world, guardPos, 5);

            pathTraverser.CurrentTarget.X = guardPosClamped.X;
            pathTraverser.CurrentTarget.Y = guardPosClamped.Y;
            pathTraverser.CurrentTarget.Z = guardPosClamped.X;

            float size = guardedEntity.SelectionBox.XSize;

            if ( guardedEntity.Swimming || guardedEntity.FeetInLiquid )
            {
                PlayBestMoveAnimation();
                guardPosClamped = UpdateSwimSteering(guardPosClamped);
                pathTraverser.WalkTowards(guardPosClamped, GetBestMoveSpeed(), size + 0.2f, OnGoalReached, OnStuck);
            }
            else if ( (!guardedEntity.Swimming && !guardedEntity.FeetInLiquid ) && guardTargetSwimmingLastFrame )
            {
                PlayBestMoveAnimation();
                pathTraverser.NavigateTo_Async(guardPosClamped, GetBestMoveSpeed(), size + 0.2f, OnGoalReached, OnStuck, OnPathFailed, 5000);
            }

            //DebugUtility.DebugDrawPosition(world, guardPosClamped, 255, 0, 255);

            float dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            if (dist < arriveDistance * arriveDistance)
            {
                pathTraverser.Stop();
                return false;
            }

            if (allowTeleport && dist > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05)
            {
                AiUtility.TryTeleportToEntity(entity, guardedEntity);
            }

            guardTargetSwimmingLastFrame = guardedEntity.Swimming || guardedEntity.FeetInLiquid;

            return !stuck && !stopNow && pathTraverser.Active;
        }

        protected bool IsGuardableEntity( Entity ent)
        {
            if (ent == null || !ent.Alive || ent.EntityId == entity.EntityId)
                return false;

            if (targetEntityCodesExact.Contains(ent.Code.Path))
            {
                return true;
            }

            for (int i = 0; i < targetEntityCodesBeginsWith.Length; i++)
            {
                if (ent.Code.Path.StartsWithFast(targetEntityCodesBeginsWith[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private Vec3d FindDecentTeleportPos()
        {
            var ba = entity.World.BlockAccessor;
            var rnd = entity.World.Rand;

            Vec3d pos = new Vec3d();
            BlockPos bpos = new BlockPos();
            for (int i = 0; i < 20; i++)
            {
                double rndx = rnd.NextDouble() * 10 - 5;
                double rndz = rnd.NextDouble() * 10 - 5;
                pos.Set(guardedEntity.ServerPos.X + rndx, guardedEntity.ServerPos.Y, guardedEntity.ServerPos.Z + rndz);

                for (int j = 0; j < 8; j++)
                {
                    // Produces: 0, -1, 1, -2, 2, -3, 3
                    int dy = (1 - (j % 2) * 2) * (int)Math.Ceiling(j / 2f);

                    bpos.Set((int)pos.X, (int)(pos.Y + dy + 0.5), (int)pos.Z);
                    Block aboveBlock = ba.GetBlock(bpos);
                    var boxes = aboveBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes != null && boxes.Length > 0) continue;

                    bpos.Set((int)pos.X, (int)(pos.Y + dy - 0.1), (int)pos.Z);
                    Block belowBlock = ba.GetBlock(bpos);
                    boxes = belowBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes == null || boxes.Length == 0) continue;

                    return pos;
                }
            }

            return null;
        }

        Vec3d tmpVec = new Vec3d();
        Vec3d collTmpVec = new Vec3d();
        private Vec3d UpdateSwimSteering( Vec3d steerTarget )
        {
            float yaw = (float)Math.Atan2(entity.ServerPos.X - steerTarget.X, entity.ServerPos.Z - steerTarget.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (Traversable(tmpVec))
            {
                steerTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return steerTarget;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (Traversable(tmpVec))
            {
                steerTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return steerTarget;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (Traversable(tmpVec))
            {
                steerTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return steerTarget;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            steerTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);

            return steerTarget;
        }

        bool Traversable(Vec3d pos)
        {
            return
                !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, pos, false) ||
                !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(pos).Add(0, Math.Min(1, stepHeight), 0), false)
            ;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (moveFarAnimation != null)
                entity.AnimManager.StopAnimation(moveFarAnimation);

            if ( moveNearAnimation != null )
                entity.AnimManager.StopAnimation(moveNearAnimation);
        }

        protected void OnStuck()
        {
            stuck = true;

            if ( allowTeleport )
                AiUtility.TryTeleportToEntity(entity, guardedEntity);

            pathTraverser.Stop();
        }

        public void OnPathFailed()
        {
            stopNow = true;

            if (allowTeleport)
                AiUtility.TryTeleportToEntity(entity, guardedEntity);

            pathTraverser.Stop();
        }

        protected void OnGoalReached()
        {
            stopNow = true;
            pathTraverser.Stop();
        }

        public bool TrySendGuardedEntityAttackedNotfications( Entity attacker)
        {
            //Don't aggro if our guard target was injured by friendly fire.
            if ( attacker is EntityAgent )
            {
                EntityAgent agentAttacker = attacker as EntityAgent;
                if (agentAttacker.HerdId == entity.HerdId)
                    return false;
            }

            entity.Notify("entityAttackedGuardedEntity", attacker);

            return true;
        }

        public bool SendGuardChaseStopNotfications()
        {
            entity.Notify("guardChaseStop", null);

            return true;
        }

        public override bool Notify(string key, object data)
        {

            if (key == "haltMovement")
            {
                //If another task has requested we halt, stop moving to guard target.
                if (entity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if (key == "clearTargetHistory")
            {
                ClearTargetHistory();
                return false;
            }

            return false;
        }
    }
}

