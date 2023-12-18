using System;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskEatDeadEntities : AiTaskBaseTargetable
    {
        protected Vec3d targetPos = new Vec3d();

        //Json Fields
        protected float moveSpeed = 0.06f;
        protected string moveAnimation = "walk";
        protected float minDist = 15.0f;
        protected float maxDist = 30.0f;

        protected float eatDuration = 3.0f;
        protected string eatAnimation = "eat";
        protected float eatAnimMinInterval = 1.0f;
        protected float eatAnimMaxInterval = 1.5f;

        protected bool eatEveryting = false;
        protected bool allowCannibalism = false;

        //State Vars
        protected bool stopNow = false;

        protected long finishedMs;

        protected long lastSearchTotalMs;

        protected EntityPartitioning partitionUtil;

        protected int searchWaitMs = 4000;

        protected float minTurnAnglePerSec;
        protected float maxTurnAnglePerSec;
        protected float curTurnRadPerSec;

        protected float currentEatingTime = 0.0f;
        protected float nextEatAnimTime = 0.0f;
        protected eInternalTaskState internalTaskState = eInternalTaskState.Moving;

        Dictionary<Entity, double> targetTimesHistory = new Dictionary<Entity, double>();
        protected enum eInternalTaskState
        {
            Moving,
            Eating
        }

        protected readonly float[] xPathSearchOffset        = { 0.0f, 1.0f, -1.0f };
        protected readonly float[] zPathSearchOffset        = { 0.0f, 1.0f, -1.0f };
        protected readonly float[] pathSearchOffsetHeight   = { 0.0f, 1.0f, -1.0f };
        protected int[] pathSearchOffsetIndices             = { 0, 0, 0 };
        protected bool allPathsFailed = false;

        protected float nextTargetHistoryClearTime = 0.0f;

        private const float EATING_RANGE = 0.5f;
        private const float TARGET_RETRY_MS_TIME = 3000.0f;
        private const float TARGET_HISTORY_CLEAR_MS_INTERVAL = 60000;

        public AiTaskEatDeadEntities(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["moveSpeed"].AsFloat(0.06f);
            moveAnimation = taskConfig["animation"].AsString("walk");
            minDist = taskConfig["minDist"].AsFloat(15.0f);
            maxDist = taskConfig["maxDist"].AsFloat(30.0f);

            eatDuration = taskConfig["eatDuration"].AsFloat(3.0f);
            eatAnimation = taskConfig["eatAnimation"].AsString("eat");
            eatAnimMinInterval = taskConfig["eatAnimMinInterval"].AsFloat(1.0f);
            eatAnimMaxInterval = taskConfig["eatAnimMaxInterval"].AsFloat(1.5f);

            eatEveryting = taskConfig["eatEveryting"].AsBool(false);
            allowCannibalism = taskConfig["allowCannibalism"].AsBool(false);

            Debug.Assert(eatAnimMinInterval <= eatAnimMaxInterval, "eatAnimMinInterval must be less than or equal to eatAnimMaxInterval.");
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

            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds)
                return false;

            if (cooldownUntilMs > entity.World.ElapsedMilliseconds)
                return false;


            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }

            if (attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5)
            {
                return false;
            }

            float range = maxDist;


            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            //Clean our target history at regular intervals so we don't take too much memory.
            if (nextTargetHistoryClearTime <= entity.World.ElapsedMilliseconds)
            {
                CleanLastTargetTimeHistory();
                nextTargetHistoryClearTime = entity.World.ElapsedMilliseconds + TARGET_HISTORY_CLEAR_MS_INTERVAL;
            }


            //Aquire a dead target if we don't have one.
            if (targetEntity == null || targetEntity.Alive)
                targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsEntityTargetableForEating(e, range, eatEveryting));

            if (targetEntity != null)
            {
                SetEntityLastTargetTime(targetEntity, entity.World.ElapsedMilliseconds);

                double x = targetEntity.ServerPos.X + (double)targetEntity.SelectionBox.X1 - (double)targetEntity.OriginSelectionBox.X1;
                double y = targetEntity.ServerPos.Y + (double)targetEntity.SelectionBox.Y1 - (double)targetEntity.OriginSelectionBox.Y1;
                double z = targetEntity.ServerPos.Z + (double)targetEntity.SelectionBox.Z1 - (double)targetEntity.OriginSelectionBox.Z1;

                targetPos.X = x;
                targetPos.Y = y;
                targetPos.Z = z;

                return true;
            }

            return false;
        }

        private bool IsEntityTargetableForEating(Entity e, float range, bool ignoreEntityCode = false)
        {

            if (e == null)
                return false;

            if (e.Alive || !e.IsInteractable || e.EntityId == entity.EntityId || !CanSense(e, range))
            {
                return false;
            }

            if (e.GetBehavior<EntityBehaviorDeadDecay>() == null)
            {
                return false;
            }

            //if flag is true, the Ai Will eat their own dead.
            if (!allowCannibalism)
            {
                if (e.Code.Path == entity.Code.Path)
                    return false;
            }

            //Depending on how rotten the dead thing is we can detect it from farther away.
            double detectRange = GetDetectionRangeForEnt(e);
            if (entity.ServerPos.SquareDistanceTo(e.ServerPos) > detectRange * detectRange)
                return false;

            double lastTargetTime = GetLastTargetTime(e);
            if (lastTargetTime < entity.World.ElapsedMilliseconds + TARGET_RETRY_MS_TIME && lastTargetTime != -1.0)
                return false;

            if (ignoreEntityCode)
            {
                return true;
            }

            if (targetEntityCodesExact.Contains(e.Code.Path))
            {
                return true;
            }

            for (int i = 0; i < targetEntityCodesBeginsWith.Length; i++)
            {
                if (e.Code.Path.StartsWithFast(targetEntityCodesBeginsWith[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public float MinDistanceToTarget()
        {
            if (targetEntity == null)
                return 0.1f;

            return Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2 + entity.SelectionBox.XSize / 4);
        }

        public float MinEatingDistance()
        {
            if (targetEntity == null)
                return EATING_RANGE;

            return EATING_RANGE + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2);
        }

        public override void StartExecute()
        {
            base.StartExecute();

            ResetPathSearchOffset();
            
            stopNow = false;
            internalTaskState = eInternalTaskState.Moving;

            bool giveUpWhenNoPath = true;
            int searchDepth = 5000;

            if (targetEntity == null || attackedByEntity != null)
            {
                stopNow = true;
                return;
            }

            //Get Turning Speed
            if (entity?.Properties.Server?.Attributes != null)
            {
                minTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;

            if (!pathTraverser.NavigateTo( GetTargetPosWithPathOffset().Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth ) )
                FindNextPathSearchOffsetForPos(targetPos);

            currentEatingTime = 0.0f;
            nextEatAnimTime = 0.0f;
        }


        float lastPathUpdateSeconds;
        float nextCheckPathTime = 0.0f;
        public override bool ContinueExecute(float dt)
        {
            lastPathUpdateSeconds += dt;

            eInternalTaskState lastState = internalTaskState;

            if (targetEntity == null || attackedByEntity != null)
                return false;

            UpdateState();

            //Turn to face target.
            Vec3f targetVec = new Vec3f();

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            targetVec.Normalize();

            if (internalTaskState == eInternalTaskState.Eating && !stopNow)
            {
                currentEatingTime += dt;

                if (currentEatingTime >= nextEatAnimTime)
                {
                    //Play Eat Animation
                    if (eatAnimation != null)
                    {
                        entity.AnimManager.StopAnimation(eatAnimation);
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = eatAnimation, Code = eatAnimation }.Init());
                    }

                    float nextEatTimeRange = eatAnimMaxInterval - eatAnimMinInterval;
                    float nextEatTimeValue = (float)world.Rand.NextDouble() * nextEatTimeRange;
                    nextEatAnimTime = currentEatingTime + nextEatTimeValue;
                }

                float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

                float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
                entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
                entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

                if (currentEatingTime >= eatDuration)
                    ConsumeTargetEnt();

            }
            else
            {
                Vec3d searchOffset = GetCurrentPathSearchOffset();
                float minDist = MinDistanceToTarget();
                double x = (targetEntity.ServerPos.X + (double)targetEntity.SelectionBox.X1 - (double)targetEntity.OriginSelectionBox.X1) - (targetVec.X * minDist);
                double y = (targetEntity.ServerPos.Y + (double)targetEntity.SelectionBox.Y1 - (double)targetEntity.OriginSelectionBox.Y1);
                double z = (targetEntity.ServerPos.Z + (double)targetEntity.SelectionBox.Z1 - (double)targetEntity.OriginSelectionBox.Z1) - (targetVec.Z * minDist);

                pathTraverser.CurrentTarget.X = x + searchOffset.X;
                pathTraverser.CurrentTarget.Y = y + searchOffset.Y;
                pathTraverser.CurrentTarget.Z = z + searchOffset.Z;

                targetPos.X = x;
                targetPos.Y = y;
                targetPos.Z = z;

                //If we're out of range.
                double detectionRange = GetDetectionRangeForEnt(targetEntity);
                if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos) > detectionRange * detectionRange)
                {
                    stopNow = true;
                }

                //Make sure our path is still valid.
                if (lastPathUpdateSeconds >= nextCheckPathTime)
                {
                    bool giveUpWhenNoPath = true;
                    int searchDepth = 3500;

                    if (!pathTraverser.NavigateTo(GetTargetPosWithPathOffset().Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth ) )
                    {
                        if ( allPathsFailed )
                        {
                            stopNow = true;
                        }
                        else
                        {
                            FindNextPathSearchOffsetForPos(targetPos);
                        }
                                 
                    }

                    nextCheckPathTime = lastPathUpdateSeconds + 1.0f;
                }
            }

            bool inCreativeMode = (targetEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

            return
                targetEntity != null &&
                !targetEntity.Alive &&
                currentEatingTime < eatDuration &&
                !inCreativeMode &&
                !stopNow
            ;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            finishedMs = entity.World.ElapsedMilliseconds;

            if (moveAnimation != null)
                entity.AnimManager.StopAnimation(moveAnimation);

            if (eatAnimation != null)
                entity.AnimManager.StopAnimation(eatAnimation);

            targetEntity = null;
            pathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            //If entity finishes eating first.
            if (key == "doneEating")
            {
                if (targetEntity == (Entity)data)
                {
                    if (currentEatingTime >= (eatDuration * 0.75))
                    {
                        //We got to eat something.
                        if (entity.Alive && !targetEntity.Alive)
                        {
                            bhEmo?.TryTriggerState("saturated", targetEntity.EntityId);
                        }
                    }

                    RemoveEntityLastTargetTime(targetEntity);
                    targetEntity = null;
                    stopNow = true;
                }

                return false;
            }

            return false;
        }


        private void OnStuck()
        {
            stopNow = true;
        }

        private void OnGoalReached()
        {
            if (targetEntity != null)
            {
                UpdateState();
            }
        }

        private void UpdateState()
        {
            if (this.entity.Pos.SquareDistanceTo(GetTargetPosWithPathOffset()) <= MinEatingDistance() * MinEatingDistance() ||
                (this.entity.Pos.SquareHorDistanceTo(GetTargetPosWithPathOffset()) <= MinEatingDistance() * MinEatingDistance() && (this.entity.Pos.Y - targetPos.Y < 2 || targetPos.Y - this.entity.Pos.Y < 2)))
            {
                //Eating State
                internalTaskState = eInternalTaskState.Eating;

                if (moveAnimation != null)
                    entity.AnimManager.StopAnimation(moveAnimation);
            }
            else
            {
                //Move State
                internalTaskState = eInternalTaskState.Moving;

                if (eatAnimation != null)
                    entity.AnimManager.StopAnimation(eatAnimation);

                if (moveAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = moveAnimation, Code = moveAnimation }.Init());

                pathTraverser.Retarget();
            }
        }

        private void ConsumeTargetEnt()
        {
            if (targetEntity == null)
                return;

            if (targetEntity.Alive)
                return;

            if (targetEntity.GetBehavior<EntityBehaviorDeadDecay>() != null)
            {
                targetEntity.GetBehavior<EntityBehaviorDeadDecay>().DecayNow();

                //Notify anyting else eating this ent we are done eating.
                entity.World.GetNearestEntity(entity.ServerPos.XYZ, 20, 20, (ent) =>
                {
                    if ( ent is EntityAgent)  
                    {
                        EntityAgent agent = ent as EntityAgent;
                        if ( ent.EntityId != entity.EntityId && agent.Alive)
                            agent.Notify("doneEating", targetEntity);
                    }

                    return false;
                });

            }

            //We got to eat something.
            if (entity.Alive && !targetEntity.Alive)
            {
                bhEmo?.TryTriggerState("saturated", targetEntity.EntityId);
            }

            RemoveEntityLastTargetTime(targetEntity);
            targetEntity = null;
            stopNow = true;
        }

        private double GetDetectionRangeForEnt(Entity ent)
        {

            if (ent.GetBehavior<EntityBehaviorDeadDecay>() == null)
                return minDist;

            double hoursToDecay = (double)ent.GetBehavior<EntityBehaviorDeadDecay>().HoursToDecay;
            double hoursDead = ent.World.Calendar.TotalHours - ent.GetBehavior<EntityBehaviorDeadDecay>().TotalHoursDead;

            return MathUtility.GraphClampedValue(0.0, hoursToDecay, minDist, maxDist, hoursDead);
        }

        private void SetEntityLastTargetTime(Entity ent, double time)
        {
            if (targetTimesHistory.ContainsKey(ent))
                targetTimesHistory[ent] = time;
            else
                targetTimesHistory.Add(ent, time);
        }

        private double GetLastTargetTime(Entity ent)
        {
            if (targetTimesHistory.ContainsKey(ent))
                return targetTimesHistory[ent];

            return -1.0f;
        }

        private void RemoveEntityLastTargetTime(Entity ent)
        {
            if (targetTimesHistory.ContainsKey(ent))
                targetTimesHistory.Remove(ent);
        }

        private void CleanLastTargetTimeHistory()
        {
            targetTimesHistory.Clear();
        }

        private void FindNextPathSearchOffsetForPos( Vec3d pos )
        {
            while ( !allPathsFailed )
            {
                GoToNextPathSearchOffset();

                if (IsPathOffsetForPositionInSolid(pos))
                    continue;

                break;
            }
        }

        private void GoToNextPathSearchOffset()
        {
            pathSearchOffsetIndices[0] += 1;
            if (pathSearchOffsetIndices[0] >= xPathSearchOffset.Length)
            {
                pathSearchOffsetIndices[0] = 0;
                pathSearchOffsetIndices[1] += 1;
            }

            if (pathSearchOffsetIndices[1] >= zPathSearchOffset.Length)
            {
                pathSearchOffsetIndices[1] = 0;
                pathSearchOffsetIndices[2] += 1;
            }

            if( pathSearchOffsetIndices[2] >= pathSearchOffsetHeight.Length)
            {
                //we've reached the end of all the offsets we can try.
                pathSearchOffsetIndices[2] = 0;
                allPathsFailed = true;
            }

        }

        private Vec3d GetCurrentPathSearchOffset()
        {

            double x = xPathSearchOffset[pathSearchOffsetIndices[0]];
            double z = zPathSearchOffset[pathSearchOffsetIndices[1]];
            double height = pathSearchOffsetHeight[pathSearchOffsetIndices[2]];

            return new Vec3d(x, height, z);
        }

        private void ResetPathSearchOffset()
        {
            pathSearchOffsetIndices[0] = 0;
            pathSearchOffsetIndices[1] = 0;
            pathSearchOffsetIndices[2] = 0;
            allPathsFailed = false;

        }

        private Vec3d GetTargetPosWithPathOffset()
        {
            Vec3d searchOffset = GetCurrentPathSearchOffset();
            return targetPos + searchOffset;
        }

        private bool IsPathOffsetForPositionInSolid( Vec3d pos )
        {
            Vec3d pathOffset = GetCurrentPathSearchOffset();
            
            int x = (int) (pos.X + pathOffset.X);
            int y = (int) (pos.Y + pathOffset.Y);
            int z = (int) (pos.Z + pathOffset.Z);

            BlockPos blockPos = new BlockPos(x, y, z);
            IBlockAccessor blockAccessor = entity.World.BlockAccessor;
            Block pathEndBlock = blockAccessor.GetBlock(blockPos);

            bool solid = pathEndBlock.BlockMaterial != EnumBlockMaterial.Air;
            
            if ( solid )
            {
                bool confirmedSolid = false;
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    if (pathEndBlock.SideSolid[facing.Index] == true)
                    {
                        confirmedSolid = true;
                        break;
                    }
                        

                    BlockEntity blockEnt = blockAccessor.GetBlockEntity(blockPos);
                    if ( pathEndBlock is BlockMicroBlock)
                    { 
                        if (blockAccessor.GetBlockEntity(blockPos) is BlockEntityMicroBlock)
                        {
                            BlockEntityMicroBlock microBlockEnt = blockAccessor.GetBlockEntity(blockPos) as BlockEntityMicroBlock;
                            if (microBlockEnt.sideAlmostSolid[facing.Index] == true )
                            {
                                confirmedSolid = true;
                                break;
                            }
                        }
                    }
                }

                solid = confirmedSolid;
            }    
            
            return solid;
        }
    }
}
