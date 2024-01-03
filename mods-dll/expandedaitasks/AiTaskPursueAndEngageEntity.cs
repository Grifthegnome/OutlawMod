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
using Vintagestory.API.Client;

namespace ExpandedAiTasks
{
    public class AiTaskPursueAndEngageEntity : AiTaskBaseExpandedTargetable
    {
        protected Vec3d targetPos;
        protected Vec3d lastPathUpdatePos = new Vec3d();
        protected Vec3d lastKnownPos;
        protected Vec3d lastKnownMotion;
        protected Vec3d withdrawPos = new Vec3d();

        Entity guardTargetAttackedByEntity = null;

        //Json Fields
        protected float pursueSpeed = 0.2f;
        protected float pursueRange = 25f;
        protected string pursueAnimation = "run";
        protected float engageSpeed = 0.1f;
        protected float engageRange = 5f;
        protected string engageAnimation = "walk";

        protected float arriveRange = 1.0f;
        protected float arriveVerticalRange = 1.0f;

        protected float maxFollowTime = 60;
        protected float maxTargetHealth = -1.0f;

        protected bool withdrawIfNoPath = false;
        protected float withdrawDist = 20.0f;
        protected float withdrawDistDamaged = 30.0f;
        protected float withdrawEndTime = 30.0f;
        protected string withdrawAnimation = "idle";

        protected string swimAnimation = "swim";

        protected bool alarmHerd = false;
        protected bool packHunting = false; //Each individual herd member's maxTargetHealth value will equal maxTargetHealth * number of herd members.
        protected bool pursueLastKnownPosition = true;
        protected float noLOSTimeoutMs = 5000.0f;

        protected float lastNewTargetCheckTime = 0;
        protected float checkForNewTargetInterval = 500.0f;

        protected int lastKnownPositionExtendCount = 0;
        protected int lastKnownPositionExtendMax = 10;

        bool updatedByTeammateLastFrame = false;

        //State Vars
        protected bool stopNow = false;

        protected float currentFollowTime = 0;
        protected float currentWithdrawTime = 0;
        protected float lastTimeSawTarget = 0;
        protected float withdrawTargetMoveDistBeforeEncroaching = 0.0f;

        protected long finishedMs;

        protected long lastSearchTotalMs;

        //protected EntityPartitioning partitionUtil;
        protected float extraTargetDistance = 0f;

        float healthLastFrame = 0;

        protected bool lowTempMode;

        protected int searchWaitMs = 250;

        private eInternalMovementState internalMovementState = eInternalMovementState.Pursuing;

        const float NO_AGRESSIVE_PERSUIT_AFTER_ROUTE_MS = 30000;

        private enum eInternalMovementState
        {
            Pursuing,
            Engaging,
            Arrived,
            Withdrawn
        }

        bool hasPath = false;
        bool hadPathLastFrame = false;
        private int consecutivePathFailCount = 0;

        float stepHeight;
        Vec3d tmpVec = new Vec3d();
        Vec3d collTmpVec = new Vec3d();

        protected float minTurnAnglePerSec;
        protected float maxTurnAnglePerSec;
        protected float curTurnRadPerSec;

        public AiTaskPursueAndEngageEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            pursueSpeed = taskConfig["pursueSpeed"].AsFloat(0.02f);
            pursueRange = taskConfig["pursueRange"].AsFloat(25f);
            pursueAnimation = taskConfig["pursueAnimation"].AsString("run");
            engageSpeed = taskConfig["engageSpeed"].AsFloat(0.01f);
            engageRange = taskConfig["engageRange"].AsFloat(5f);
            engageAnimation = taskConfig["engageAnimation"].AsString("walk");

            arriveRange = taskConfig["arriveRange"].AsFloat(1.0f);
            arriveVerticalRange = taskConfig["arriveVerticalRange"].AsFloat(1.0f);

            maxTargetHealth = taskConfig["maxTargetHealth"].AsFloat(-1.0f);

            withdrawIfNoPath = taskConfig["withdrawIfNoPath"].AsBool(false);
            withdrawDist = taskConfig["withdrawDist"].AsFloat(20.0f);
            withdrawDistDamaged = taskConfig["withdrawDistDamaged"].AsFloat(30.0f);
            withdrawEndTime = taskConfig["withdrawEndTime"].AsFloat(30.0f);
            withdrawAnimation = taskConfig["withdrawAnimation"].AsString("idle");

            swimAnimation = taskConfig["swimAnimation"].AsString("swim");

            extraTargetDistance = taskConfig["extraTargetDistance"].AsFloat(0f);
            maxFollowTime = taskConfig["maxFollowTime"].AsFloat(60);
            
            alarmHerd = taskConfig["alarmHerd"].AsBool(false);
            packHunting = taskConfig["packHunting"].AsBool(false);
            pursueLastKnownPosition = taskConfig["pursueLastKnownPosition"].AsBool(true);
            noLOSTimeoutMs = taskConfig["noLOSTimeout"].AsFloat(15000.0f);

            retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);



            Debug.Assert(pursueRange > engageRange, "pursueRange must be a greater value to engageRange.");

            //Get Turning Speed Values
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
        }


        public override bool ShouldExecute()
        {

            if (whenInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenInEmotionState) == false)
                    return false;
            }

            if ( whenNotInEmotionState != null )
            { 
                if (bhEmo?.IsInEmotionState(whenNotInEmotionState) == true)
                    return false;
            }
                            
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) 
                return false;

            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) 
                return false;


            float range = pursueRange;
            targetEntity = null;
            targetPos = null;
            lastPathUpdatePos = null;
            lastKnownPos = null;
            
            lastKnownMotion = null;
            lastTimeSawTarget = 0;
            lastKnownPositionExtendCount = 0;
            updatedByTeammateLastFrame = false;

            ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("health");
            if (treeAttribute != null)
                healthLastFrame = treeAttribute.GetFloat("currenthealth");

            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 5000 )
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5 && IsTargetableEntity(attackedByEntity, range, true))
            {
                targetEntity = attackedByEntity;
            }
            else if (guardTargetAttackedByEntity != null && guardTargetAttackedByEntity.Alive)
            {
                targetEntity = guardTargetAttackedByEntity;
            }
            else
            {
                guardTargetAttackedByEntity = null;
            }

            if (packHunting || alarmHerd)
            {
                UpdateHerdCount();
            }

            //Aquire a target if we don't have one.
            if ( targetEntity == null || !targetEntity.Alive )
            {

                //If we recently routed due to poor morale, don't go chasing people unless they attacked us.
                if (AiUtility.GetLastTimeEntityFailedMoraleMs(entity) + NO_AGRESSIVE_PERSUIT_AFTER_ROUTE_MS >= entity.World.ElapsedMilliseconds)
                    return false;

                targetEntity = AquireNewTarget();
            }
                

            if (targetEntity != null)
            {
                TryAlarmHerd();
                
                targetPos = targetEntity.ServerPos.XYZ;
                lastPathUpdatePos = targetEntity.ServerPos.XYZ;
                lastKnownPos = targetEntity.ServerPos.XYZ;
                lastKnownMotion = targetEntity.ServerPos.Motion.Clone();
                lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                lastKnownPositionExtendCount = 0;
                withdrawPos = targetPos.Clone();
                withdrawTargetMoveDistBeforeEncroaching = Math.Max(1.0f, withdrawDist / 4);

                return true;
            }

            return false;
        }

        private bool IsEntityTargetableByPack(Entity ent, float range, bool ignoreEntityCode = false)
        {
            if (!(ent is EntityAgent))
                return false;

            //If we are pack hunting.
            if (packHunting)
            {
                float packTargetMaxHealth = maxTargetHealth * herdMembers.Count;

                ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute == null)
                    return false;

                float targetHealth = treeAttribute.GetFloat("currenthealth");

                if ( packTargetMaxHealth < targetHealth )
                    return false;

            }
            else
            {
                if ( maxTargetHealth > 0 )
                {
                    ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");
                    
                    if (treeAttribute == null)
                        return false;
                    
                    float targetHealth = treeAttribute.GetFloat("currenthealth");                   

                    if (maxTargetHealth < targetHealth)
                        return false;
                }
            }

            if (!IsTargetableEntity(ent, range, ignoreEntityCode))
                return false;

            return AiUtility.IsAwareOfTarget(entity, ent, range, range);
        }

        public float MinDistanceToTarget()
        {
            return extraTargetDistance + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2 + entity.SelectionBox.XSize / 4);
        }

        public override void StartExecute()
        {
            base.StartExecute();

            consecutivePathFailCount = 0;
            stopNow = false;

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            stepHeight = bh == null ? 0.6f : bh.stepHeight;

            bool giveUpWhenNoPath = targetPos.SquareDistanceTo(entity.Pos.XYZ) < 12 * 12;
            int searchDepth = 3500;
            // 1 in 20 times we do an expensive search
            if (world.Rand.NextDouble() < 0.05)
            {
                searchDepth = 10000;
            }

            float moveSpeed = GetMovementSpeedForState(internalMovementState);

            hasPath = pathTraverser.NavigateTo_Async(targetPos.Clone(), GetMovementSpeedForState(internalMovementState), MinDistanceToTarget(), OnGoalReached, OnStuck, OnPathFailed, searchDepth );

            if (!hasPath)
            {
                UpdateWithdrawPosition();
                bool witdrawOk = pathTraverser.NavigateTo(withdrawPos.Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth );

                stopNow = !witdrawOk;
            }

            currentFollowTime = 0;
            currentWithdrawTime = 0;
            consecutivePathFailCount = 0;

            if ( !stopNow )
            {
                //play a sound associated with this action.
                entity.PlayEntitySound("engageentity", null, true);
            }
            
        }

        public override bool CanContinueExecute()
        {
            return pathTraverser.Ready;
        }

        float lastPathUpdateSeconds;
        bool reachedWithdrawPosition = false;
        Entity targetLastUpdate = null;
        public override bool ContinueExecute(float dt)
        {
            AiUtility.UpdateLastTimeEntityInCombatMs(entity);

            if (targetEntity == null)
                return false;

            if (!targetEntity.Alive)
                return false;

            //This covers the case where we get hit by a projectile and then the shooter is auto-merged into our herd.
            if ( AiUtility.AreMembersOfSameHerd( entity, targetEntity ) )
                return false;

            currentFollowTime += dt;
            lastPathUpdateSeconds += dt;

            bool canSeeTarget = true;

            if (pursueLastKnownPosition)
                canSeeTarget = AiUtility.IsAwareOfTarget(entity,targetEntity, pursueRange, pursueRange);

            Vec3d pathToPos = !pursueLastKnownPosition || canSeeTarget ? targetPos : lastKnownPos;
            Vec3d clampedPathPos = AiUtility.ClampPositionToGround(world, pathToPos, 15);

            eInternalMovementState lastMovementState = internalMovementState;
            UpdateMovementState(clampedPathPos);

            //Depending on whether we are pursuing or engaging, determine the distance our target has to move for us to recompute our path.
            //When we are engaging (close range follow) we need to recompute more often so we can say on our target.
            float minRecomputeNavDistance = internalMovementState == eInternalMovementState.Engaging ? 1 * 1 : 1.5f * 1.5f;
            bool activelyMoving = lastPathUpdatePos.SquareDistanceTo(targetEntity.ServerPos.XYZ) >= minRecomputeNavDistance;

            if ( activelyMoving )
            {
                targetPos.Set(targetEntity.ServerPos.X + (targetEntity.ServerPos.Motion.X * 10), targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + (targetEntity.ServerPos.Motion.Z * 10));

                if (canSeeTarget)
                {
                    lastKnownMotion = targetEntity.ServerPos.Motion.Clone();
                    lastKnownPos.Set(targetEntity.ServerPos.X + (lastKnownMotion.X * 10), targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + (lastKnownMotion.Z * 10));
                    lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                    lastKnownPositionExtendCount = 0;

                    //If we see the enemy, update our squad.
                    TryAlarmHerd();
                }
            }
            else
            {
                targetPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);

                if (canSeeTarget)
                {
                    lastKnownMotion = targetEntity.ServerPos.Motion.Clone();
                    lastKnownPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
                    lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                    lastKnownPositionExtendCount = 0;
                    
                    //If we see the enemy, update our squad.
                    TryAlarmHerd();
                }
            }
            
            if (activelyMoving || internalMovementState != lastMovementState || targetEntity != targetLastUpdate || updatedByTeammateLastFrame)
            {
                lastPathUpdatePos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
                //If the target is in liquid, walk at the target.
                //To Do: Make sure Ai don't get stuck while walking towards water.
                if (AiUtility.LocationInLiquid(world, clampedPathPos) || entity.Swimming || entity.FeetInLiquid )
                {
                    Vec3d steeringPosition = UpdateSteeringToPosition(clampedPathPos.Clone());
                    hasPath = pathTraverser.WalkTowards(steeringPosition, GetMovementSpeedForState(internalMovementState), MinDistanceToTarget(), OnGoalReached, OnStuck);
                }   
                else
                {
                    int searchDepth = 3500;
                    // 1 in 20 times we do an expensive search
                    if (world.Rand.NextDouble() < 0.05)
                    {
                        searchDepth = 10000;
                    }

                    hasPath = pathTraverser.NavigateTo_Async(clampedPathPos.Clone(), GetMovementSpeedForState(internalMovementState), MinDistanceToTarget(), OnGoalReached, OnStuck, OnPathFailed, searchDepth);
                }

                updatedByTeammateLastFrame = false;
                lastPathUpdateSeconds = 0;            
            }

            //Update our target this update.
            targetLastUpdate = targetEntity;

            double distToTargetEntitySqr = !pursueLastKnownPosition || canSeeTarget ? entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) : entity.ServerPos.SquareDistanceTo(lastKnownPos);

            if ( hasPath || !withdrawIfNoPath )
            {
                currentWithdrawTime = 0.0f;
                reachedWithdrawPosition = false;

                //If we have LOS to our target
                if ( pursueLastKnownPosition && canSeeTarget)
                {
                    pathTraverser.CurrentTarget.X = clampedPathPos.X;
                    pathTraverser.CurrentTarget.Y = clampedPathPos.Y;
                    pathTraverser.CurrentTarget.Z = clampedPathPos.Z;
                }
                //If we can't see our target.
                if (pursueLastKnownPosition && !canSeeTarget)
                {
                    pathTraverser.CurrentTarget.X = clampedPathPos.X;
                    pathTraverser.CurrentTarget.Y = clampedPathPos.Y;
                    pathTraverser.CurrentTarget.Z = clampedPathPos.Z;
                }
                //If we magically always know where our target is.
                else if ( !pursueLastKnownPosition)
                {
                    lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                    
                    pathTraverser.CurrentTarget.X = clampedPathPos.X;
                    pathTraverser.CurrentTarget.Y = clampedPathPos.Y;
                    pathTraverser.CurrentTarget.Z = clampedPathPos.Z;
                }

                UpdateMovementAnims( internalMovementState );

            }
            else if ( withdrawIfNoPath )
            {
                currentWithdrawTime += dt;

                //Try to withdraw based on our health level.
                bool injured = false;
                ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute != null)
                {
                    float currentHealth = treeAttribute.GetFloat("currenthealth");
                    float maxHealth = treeAttribute.GetFloat("maxhealth");

                    if (currentHealth != healthLastFrame)
                        reachedWithdrawPosition = false;

                    //If we are below half health or recently damaged, retreat farther.
                    if ( currentHealth < maxHealth * 0.5 || attackedByEntityMs < 15000 && attackedByEntity != null )
                    {
                        injured = true;
                    }
                }

                float withdrawRange = injured ? withdrawDistDamaged : withdrawDist;
                double encroachRange = withdrawRange - withdrawTargetMoveDistBeforeEncroaching;
                bool targetEncroaching = distToTargetEntitySqr <= encroachRange * encroachRange;


                Vec3d targetPos = !pursueLastKnownPosition || canSeeTarget ? TargetEntity.ServerPos.XYZ : lastKnownPos;

                //Scale withdraw distance based on how high above us our target is.
                double horizontalDist = entity.ServerPos.HorDistanceTo( targetPos );
                double verticalDist = targetPos.Y - entity.ServerPos.Y;

                double withdrawScalar = 0.0;

                if (injured)
                    withdrawScalar = 1.0;
                else if (verticalDist <= 0)
                    withdrawScalar = 0.0;
                else
                    withdrawScalar = verticalDist == 0 ? 0.0 : verticalDist / horizontalDist;

                double scaledWithdrawRange = MathUtility.GraphClampedValue(0.0, 1.0, 0.0, withdrawRange, withdrawScalar);

                //Withdraw till we reach our withdraw range, otherwise, only move if the target encroaches (moves closer while we still have no path).
                if (!reachedWithdrawPosition && distToTargetEntitySqr <= scaledWithdrawRange * scaledWithdrawRange || targetEncroaching )
                {
                    UpdateWithdrawPosition();
                    UpdateMovementAnims(internalMovementState);
                    float size = targetEntity.SelectionBox.XSize;
                    pathTraverser.WalkTowards(withdrawPos.Clone(), GetMovementSpeedForState(internalMovementState), size + 0.2f, OnGoalReached, OnStuck);
                }
                else
                {

                    reachedWithdrawPosition = true;

                    pathTraverser.Stop();

                    UpdateMovementAnims(eInternalMovementState.Withdrawn);

                    //Turn to face target.
                    Vec3f targetVec = new Vec3f();

                    Vec3d facePosition = pursueLastKnownPosition ? lastKnownPos : targetPos;

                    targetVec.Set(
                        (float)(facePosition.X - entity.ServerPos.X),
                        (float)(facePosition.Y - entity.ServerPos.Y),
                        (float)(facePosition.Z - entity.ServerPos.Z)
                    );

                    targetVec.Normalize();

                    float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

                    float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
                    entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
                    entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

                    //If we have withdraw to a safe location, try to alert others in our group to come help us.
                    TryAlarmHerd();
                }
            }

            //DebugUtility.DebugTargetPositionAndLaskKnownPositionandCurrentNavPositionBlockLocation(world, targetPos, lastKnownPos, pathTraverser);

            //if we have reached our target for the time being.
            if (internalMovementState == eInternalMovementState.Arrived)
            {
                pathTraverser.Stop();

                //Turn to face target.
                Vec3f targetVec = new Vec3f();

                Vec3d facePosition = pursueLastKnownPosition ? lastKnownPos : targetPos;

                targetVec.Set(
                    (float)(facePosition.X - entity.ServerPos.X),
                    (float)(facePosition.Y - entity.ServerPos.Y),
                    (float)(facePosition.Z - entity.ServerPos.Z)
                );

                targetVec.Normalize();

                float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

                float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
                entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
                entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
            }

            // If we have been attacked by a new target, try transitioning aggro without canceling our behavior
            // Do the same if our current target has started routing from the battle.
            if ( attackedByEntity != null && attackedByEntity.Alive && attackedByEntity != targetEntity || AiUtility.IsRoutingFromBattle(targetEntity))
            {
                Entity newTarget = AquireNewTarget();
                if (newTarget != null && newTarget != targetEntity)
                {
                    targetEntity = newTarget;
                    TryAlarmHerd();
                }
                    
            }
            //See if we can get a new target if it presents itself.
            else if ( (!hasPath || !canSeeTarget) && entity.World.ElapsedMilliseconds >= lastNewTargetCheckTime + checkForNewTargetInterval )
            {
                Entity newTarget = AquireNewTarget();
                if (newTarget != null && newTarget != targetEntity)
                {
                    stopNow = true;
                }
            }
                
            Cuboidd targetBox = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            bool inCreativeMode = (targetEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

            float range = pursueRange;

            //If we reach the location and can't seem to find the enemy, start moving the direction they went.
            if (pursueLastKnownPosition && !canSeeTarget && entity.ServerPos.XYZ.SquareDistanceTo(lastKnownPos) < (MinDistanceToTarget() + 3) * (MinDistanceToTarget() + 3) )
            {
                lastKnownPos = AiUtility.MovePositionByBlockInDirectionOfVector(pathTraverser.CurrentTarget.Clone(), lastKnownMotion.Clone().Normalize() * engageRange);
                lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                lastKnownPositionExtendCount++;
            }

            ITreeAttribute attribute = entity.WatchedAttributes.GetTreeAttribute("health");
            if (attribute != null)
                healthLastFrame = attribute.GetFloat("currenthealth");

            return ( lastTimeSawTarget + noLOSTimeoutMs >= entity.World.ElapsedMilliseconds && lastKnownPositionExtendCount < lastKnownPositionExtendMax || !pursueLastKnownPosition) &&
                currentFollowTime < maxFollowTime &&
                currentWithdrawTime < withdrawEndTime &&
                //distance < range &&
                targetEntity.Alive &&
                !inCreativeMode &&
                !stopNow
            ;
                
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            finishedMs = entity.World.ElapsedMilliseconds;

            if ( engageAnimation != null )
                entity.AnimManager.StopAnimation(engageAnimation);
            
            if ( pursueAnimation != null)
                entity.AnimManager.StopAnimation(pursueAnimation);

            targetEntity = null;
            lastKnownMotion = null;
            lastKnownPos = null;
            lastTimeSawTarget = 0;
            pathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            if (key == "pursueEntity" || key == "attackEntity")
            {

                //If we are in range of our ally, respond.
                EntityTargetPairing targetPairing = (EntityTargetPairing)data;
                Entity herdMember = targetPairing.entityTargeting;
                Entity newTarget = targetPairing.targetEntity;

                bool infoUpdateFromTeammate = targetEntity == newTarget;

                //If we don't have a target, assist our group.
                if (targetEntity == null || infoUpdateFromTeammate)
                {

                    if (newTarget == null)
                        return false;

                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(herdMember.ServerPos.XYZ);
                    if ( distSqr <= pursueRange * pursueRange )
                    {
                        targetEntity = newTarget;
                        targetPos = targetEntity.ServerPos.XYZ;
                        lastPathUpdatePos = targetEntity.ServerPos.XYZ;
                        lastKnownPos = targetEntity.ServerPos.XYZ;
                        lastKnownMotion = targetEntity.ServerPos.Motion.Clone();
                        lastTimeSawTarget = entity.World.ElapsedMilliseconds;

                        if (infoUpdateFromTeammate)
                        {
                            updatedByTeammateLastFrame = true;
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                            
                    }
                }
            }
            else if ( key == "entityRouted")
            {
                //If our target has routed, stop pursuing.
                if (targetEntity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if ( key == "haltMovement")
            {
                //If another task has requested we halt, stop pursuing.
                if (entity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if (key == "entityAttackedGuardedEntity")
            {
                //If a guard task tells us our guard target has been attacked, pursue and engage the attacker.
                if ((Entity)data != null && guardTargetAttackedByEntity != (Entity)data)
                {
                    guardTargetAttackedByEntity = (Entity)data;
                    targetEntity = guardTargetAttackedByEntity;
                    targetPos = targetEntity.ServerPos.XYZ;
                    lastPathUpdatePos = targetEntity.ServerPos.XYZ;
                    lastKnownPos = targetEntity.ServerPos.XYZ;
                    lastKnownMotion = targetEntity.ServerPos.Motion.Clone();
                    lastTimeSawTarget = entity.World.ElapsedMilliseconds;
                    return true;
                }
            }
            //Clear the entity that attacked our guard target.
            else if (key == "guardChaseStop")
            {
                if (targetEntity == guardTargetAttackedByEntity)
                    stopNow = true;

                guardTargetAttackedByEntity = null;
                return false;
            }
            else if (key == "clearTargetHistory")
            {
                ClearTargetHistory();
                return false;
            }

            return false;
        }

        private void OnStuck()
        {
            if ( withdrawIfNoPath )
            {
                hasPath = false;
            }
            else
            {
                stopNow = true;
            }
            
        }

        private void OnPathFailed()
        {
            if (withdrawIfNoPath)
            {
                hasPath = false;
            }
            else
            {
                stopNow = true;
            }
        }

        private void OnGoalReached()
        {
            pathTraverser.Retarget();
        }

        private void UpdateMovementState( Vec3d positionToUse )
        {
            Vec3d entityVertical = new Vec3d(0, this.entity.ServerPos.XYZ.Y, 0);
            Vec3d targetVertical = new Vec3d(0, positionToUse.Y, 0);

            double distSqr = this.entity.ServerPos.SquareDistanceTo(positionToUse);
            double distSqrVertical = targetVertical.SquareDistanceTo(entityVertical);

            if ( distSqr <= arriveRange * arriveRange && distSqrVertical <= arriveVerticalRange * arriveVerticalRange)
            {
                internalMovementState = eInternalMovementState.Arrived;
            }
            else if (distSqr <= engageRange * engageRange && entity.ServerPos.Motion.Length() > 0.0 )
            {
                //Engage State
                internalMovementState = eInternalMovementState.Engaging;                
            }
            else if ( entity.ServerPos.Motion.Length() > 0.0 )
            {
                //Pursue State
                internalMovementState = eInternalMovementState.Pursuing;                
            }
        }

        private void UpdateMovementAnims( eInternalMovementState animState )
        {
            if (animState == eInternalMovementState.Withdrawn)
            {
                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (swimAnimation != null)
                    entity.AnimManager.StopAnimation(swimAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = withdrawAnimation, Code = withdrawAnimation }.Init());
            }
            else if (animState == eInternalMovementState.Arrived || entity.ServerPos.Motion.Length() < 0.0125)
            {
                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);

                if (swimAnimation != null && !entity.Swimming)
                    entity.AnimManager.StopAnimation(swimAnimation);
            }
            else if (animState == eInternalMovementState.Engaging)
            {
                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);

                if (entity.Swimming)
                {
                    if (swimAnimation != null)
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = swimAnimation, Code = swimAnimation }.Init());

                    if (engageAnimation != null)
                        entity.AnimManager.StopAnimation(engageAnimation);
                }
                else
                {
                    if (engageAnimation != null)
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = engageAnimation, Code = engageAnimation }.Init());
                }

            }
            else if (animState == eInternalMovementState.Pursuing)
            {
                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);

                if (entity.Swimming)
                {
                    if (swimAnimation != null)
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = swimAnimation, Code = swimAnimation }.Init());

                    if (pursueAnimation != null)
                        entity.AnimManager.StopAnimation(pursueAnimation);
                }
                else
                {
                    if (pursueAnimation != null)
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = pursueAnimation, Code = pursueAnimation }.Init());

                    if (swimAnimation != null)
                        entity.AnimManager.StopAnimation(swimAnimation);
                }

            }
        }



        private float GetMovementSpeedForState( eInternalMovementState movementState )
        {
            switch (movementState)
            {
                case eInternalMovementState.Arrived:
                    return 0.0f;
                case eInternalMovementState.Engaging:
                    return engageSpeed;
                case eInternalMovementState.Pursuing:
                    return pursueSpeed;
            }

            Debug.Assert(false, "Invalid intermal move state.");
            return 0.0f;
        }

        private Vec3d UpdateSteeringToPosition( Vec3d steeringTarget ) 
        {
            float yaw = (float)Math.Atan2(entity.ServerPos.X - steeringTarget.X, entity.ServerPos.Z - steeringTarget.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (IsTraversable(tmpVec))
            {
                steeringTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return steeringTarget;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (IsTraversable(tmpVec))
            {
                steeringTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return steeringTarget;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (IsTraversable(tmpVec))
            {
                steeringTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return steeringTarget;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            steeringTarget.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);
            return steeringTarget;

        }

        private void UpdateWithdrawPosition()
        {
            float yaw = (float)Math.Atan2(targetEntity.ServerPos.X - entity.ServerPos.X, targetEntity.ServerPos.Z - entity.ServerPos.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);

        }

        bool IsTraversable(Vec3d pos)
        {
            return
                !entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.SelectionBox, pos, false) ||
                !entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.SelectionBox, collTmpVec.Set(pos).Add(0, Math.Min(1, stepHeight), 0), false)
            ;
        }

        private List<Entity> potentialTargets = new List<Entity>();
        private List<Entity> potentialRoutingTargets = new List<Entity>();

        private Entity AquireNewTarget()
        {
            lastNewTargetCheckTime = entity.World.ElapsedMilliseconds;
            float range = pursueRange;
            potentialTargets.Clear();
            potentialRoutingTargets.Clear();
            partitionUtil.WalkEntityPartitions(entity.ServerPos.XYZ, range, (e) => BucketTargetBasedOnCombatState(e, range));

            Entity target = null;
            if ( potentialTargets.Count > 0 )
            {
                Entity bestTarget = potentialTargets[0];
                double bestDistSqr = entity.ServerPos.XYZ.SquareDistanceTo(bestTarget.ServerPos.XYZ);
                foreach (Entity ent in potentialTargets )
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(ent.ServerPos.XYZ);
                    if ( distSqr <= bestDistSqr)
                    {
                        bestTarget = ent;
                        bestDistSqr = distSqr;
                    }    
                }

                target = bestTarget;
            }
            else if( potentialRoutingTargets.Count > 0 )
            {
                Entity bestTarget = potentialRoutingTargets[0];
                double bestDistSqr = entity.ServerPos.XYZ.SquareDistanceTo(bestTarget.ServerPos.XYZ);
                foreach (Entity ent in potentialRoutingTargets)
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(ent.ServerPos.XYZ);
                    if (distSqr <= bestDistSqr)
                    {
                        bestTarget = ent;
                        bestDistSqr = distSqr;
                    }
                }

                target = bestTarget;
            }

            return target;
        }

        private bool BucketTargetBasedOnCombatState( Entity ent, float range )
        {
            if (!IsEntityTargetableByPack(ent, range))
                return true;

            if (!AiUtility.IsAwareOfTarget(entity, ent, pursueRange, pursueRange))
                return true;

            //Don't Chase Ai that are already routing.
            if (AiUtility.IsRoutingFromBattle(ent))
            {
                potentialRoutingTargets.Add(ent);
            }
            else
            {
                potentialTargets.Add(ent);
            }

            return true;
        }

        private void TryAlarmHerd()
        {
            AiUtility.TryNotifyHerdMembersToAttack(entity, targetEntity, AiUtility.GetHerdAlertRangeForEntity(entity), true);
        }
    }
}
