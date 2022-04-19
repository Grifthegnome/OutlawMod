﻿using System;
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
    public class AiTaskShootProjectileAtEntity : AiTaskBaseExpandedTargetable
    {
        int durationMs;
        int releaseAtMs;

        float minDist = 3f;
        float maxDist = 15f;
        float maxVertDist = 10f;

        //Accuracy Vars
        float minRangeDistOffTarget = 0.0f; //this is the number of blocks off target a projectile will stray at min range.
        float maxRangeDistOffTarget = 0.0f; //this is the number of blocks off target a projectile will stray at max range.
        float maxVelocity = 1.0f;

        float newTargetDistOffTarget = 0.0f;
        float newTargetZeroingTime = 0.0f;

        //Damage and Damage Falloff Vars
        float damage = 1.0f;
        float damageFalloffPercent = 0.0f;      //Percentage reduction do base damage when falloff distance hits max.
        float damageFalloffStartDist = -1.0f;   //Distance in blocks where damage falloff begins.
        float damageFalloffEndDist = -1.0f;     //Distance in blocks where damage falloff hits full percent value.

        string projectileItem = "arrow-copper";
        bool projectileRemainsInWorld = false;
        float projectileBreakOnImpactChance = 0.0f;

        bool stopIfPredictFriendlyFire = false;

        Entity targetLastFrame = null;
        double dtSinceTargetAquired = 0.0f;

        EntityPartitioning partitionUtil;

        float accum = 0;
        bool didShoot;
        bool stopNow;

        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;

        Random rnd;

        Entity guardTargetAttackedByEntity = null;

        public AiTaskShootProjectileAtEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            this.rnd = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));

            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            this.durationMs = taskConfig["durationMs"].AsInt(1500);
            this.releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            this.minDist = taskConfig["minDist"].AsFloat(3f);
            this.maxDist = taskConfig["maxDist"].AsFloat(15f);
            this.maxVertDist = taskConfig["maxVertDist"].AsFloat(this.maxDist * 0.75f);
            this.minRangeDistOffTarget = taskConfig["minRangeDistOffTarget"].AsFloat(0.0f);
            this.maxRangeDistOffTarget = taskConfig["maxRangeDistOffTarget"].AsFloat(0.0f);
            this.maxVelocity = taskConfig["maxVelocity"].AsFloat(1.0f);
            this.newTargetDistOffTarget = taskConfig["newTargetDistOffTarget"].AsFloat(0.0f);
            this.newTargetZeroingTime = taskConfig["newTargetZeroingTime"].AsFloat(0.0f);
            this.damage = taskConfig["damage"].AsFloat(1.0f);
            this.damageFalloffPercent = taskConfig["damageFalloffPercent"].AsFloat(0.0f);
            this.damageFalloffStartDist = taskConfig["damageFalloffStartDist"].AsFloat(-1.0f);
            this.damageFalloffEndDist = taskConfig["damageFalloffEndDist"].AsFloat(-1.0f);
            this.projectileItem = taskConfig["projectileItem"].AsString("arrow-copper");
            this.projectileRemainsInWorld = taskConfig["projectileRemainsInWorld"].AsBool(false);
            this.projectileBreakOnImpactChance = taskConfig[ "projectileBreakOnImpactChance"].AsFloat(0.0f);
            this.stopIfPredictFriendlyFire = taskConfig["stopIfPredictFriendlyFire"].AsBool(false);

            //Error checking for bad json values.
            Debug.Assert(damageFalloffPercent >= 0.0f && damageFalloffPercent <= 1.0f, "AiTaskValue damageFalloffPercent must be a 0.0 to 1.0 value.");
            Debug.Assert(damageFalloffStartDist < damageFalloffEndDist || damageFalloffEndDist < 0.0f, "AiTaskValue damageFalloffStartDist: " + damageFalloffStartDist + " cannot be greater than damageFalloffEndDist: " + damageFalloffEndDist);
        }


        public override bool ShouldExecute()
        {
            // React immediately on hurt, otherwise only 1/10 chance of execution
            //if (rand.NextDouble() > 0.1f && (whenInEmotionState == null || bhEmo?.IsInEmotionState(whenInEmotionState) != true)) 
             //   return false;

            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) 
                return false;

            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) 
                return false;
            
            if (whenInEmotionState == null && rand.NextDouble() > 0.5f) 
                return false;
            
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) 
                return false;


            float range = maxDist;
            float vertRange = maxVertDist;
            targetEntity = null;

            if (stopIfPredictFriendlyFire)
            {
                UpdateHerdCount();
            }

            Vec3d ownPos = entity.ServerPos.XYZ;

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && IsTargetableEntity(attackedByEntity, 15, true) && hasDirectContact(attackedByEntity, range, vertRange))
            {
                targetEntity = attackedByEntity;
            }
            else if (guardTargetAttackedByEntity != null && guardTargetAttackedByEntity.Alive)
            {
                if ( hasDirectContact(guardTargetAttackedByEntity, range, range / 2f))
                    targetEntity = guardTargetAttackedByEntity;
            }
            else
            {
                guardTargetAttackedByEntity = null;
            }

            if (targetEntity == null || !targetEntity.Alive)
            {
                targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range) && hasDirectContact(e, range, vertRange));
            }

            if ( targetEntity != targetLastFrame)
                dtSinceTargetAquired = 0.0f;

             targetLastFrame = targetEntity;

            
            if ( targetEntity != null)
            {
                //If the target is too close to fire upon.
                if( ownPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) <= minDist * minDist)
                    return false;

                Vec3d shotStartPosition = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
                Vec3d shotTargetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0);

                //If we care about shooting friendlies and we are going to shoot a friendly, early out.
                if (stopIfPredictFriendlyFire && WillFriendlyFire(shotStartPosition, shotTargetPos))
                    return false;
            }

            return targetEntity != null;
        }

        public override void StartExecute()
        {
            accum = 0;
            didShoot = false;
            stopNow = false;

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

            entity.Notify("haltMovement", entity);
            entity.PlayEntitySound("shootatentity", null, true);
        }



        public override bool ContinueExecute(float dt)
        {
            AiUtility.UpdateLastTimeEntityInCombatMs(entity);

            Vec3f targetVec = new Vec3f();

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            if (Math.Abs(yawDist) > 0.02) 
                return true;

            if (animMeta != null)
            {
                animMeta.EaseInSpeed = 1f;
                animMeta.EaseOutSpeed = 1f;
                entity.AnimManager.StartAnimation(animMeta);
            }

            accum += dt;
            dtSinceTargetAquired += dt;

            //Extra: We should look at what it would take to have a json bool, movingResetsAccuracy. That would force an AI's accuracy to reset if it is force to move from it's firing position.

            //If the target is too close to fire upon, cancel the attack.
            if (targetEntity != null && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) <= minDist * minDist)
                return false;

            if (accum > releaseAtMs / 1000f && !didShoot)
            {
                didShoot = true;

                double pitchDir = 1.0;
                double yawDir = 1.0;

                double pitchCoinToss = rnd.NextDouble();
                double yawCoinToss = rnd.NextDouble();

                if (pitchCoinToss > 0.5f)
                    pitchDir = -1.0f;

                if ( yawCoinToss > 0.5f )
                    yawDir = -1.0f;

                Vec3d shotStartPosition = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
                Vec3d shotTargetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0);
                
                //Todo: Get this working with target velocity so we can lead our targets.
                shotTargetPos = shotTargetPos.Add( targetEntity.ServerPos.Motion );

                double accuracyDistOffTarget = 0.0f;
                if (newTargetZeroingTime > 0 && newTargetDistOffTarget > 0)
                    accuracyDistOffTarget = MathUtility.GraphClampedValue(0.0, newTargetZeroingTime, newTargetDistOffTarget, 0.0, dtSinceTargetAquired);

                float distToTargetSqr = shotStartPosition.SquareDistanceTo(shotTargetPos);
                double distanceOffTarget = MathUtility.GraphClampedValue(minDist * minDist, maxDist * maxDist, minRangeDistOffTarget, maxRangeDistOffTarget, distToTargetSqr);

                double rndPitch = ((rnd.NextDouble() * distanceOffTarget) + accuracyDistOffTarget) * pitchDir;
                double rndYaw = ((rnd.NextDouble() * distanceOffTarget) + accuracyDistOffTarget) * yawDir;

                Vec3d shotDriftDirection = new Vec3d(0.0f, rndPitch, rndYaw);
                Vec3d shotTargetPosWithDrift = shotTargetPos.Add( shotDriftDirection.X, shotDriftDirection.Y, shotDriftDirection.Z );

                double distf = Math.Pow(shotStartPosition.SquareDistanceTo(shotTargetPosWithDrift), 0.1);

                Vec3d velocity = (shotTargetPosWithDrift - shotStartPosition).Normalize() * GameMath.Clamp(distf, 0.1f, maxVelocity);
                Vec3d firePos = entity.SidedPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y, 0);

                //If we care about shooting friendlies and we are going to shoot a friendly, early out.
                if (stopIfPredictFriendlyFire && WillFriendlyFire(firePos.Clone(), shotTargetPosWithDrift.Clone()))
                    return false;

                float projectileDamage = GetProjectileDamageAfterFalloff( distToTargetSqr );

                int durability = 0;
                bool survivedImpact = true;
                    
                if ( projectileBreakOnImpactChance < 1.0 )
                {
                    double breakChance = rand.NextDouble();
                    survivedImpact = breakChance > projectileBreakOnImpactChance; 
                }
                                       

                if (projectileRemainsInWorld && survivedImpact)
                    durability = 1;

                EntityProperties type = entity.World.GetEntityType(new AssetLocation(projectileItem));
                Entity projectile = entity.World.ClassRegistry.CreateEntity(type);
                ((EntityProjectile)projectile).FiredBy = entity;
                ((EntityProjectile)projectile).Damage = projectileDamage;
                ((EntityProjectile)projectile).ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation(projectileItem)));

                if ( durability == 0 )
                    ((EntityProjectile)projectile).ProjectileStack.Attributes.SetInt("durability", durability);
                
                ((EntityProjectile)projectile).DropOnImpactChance = projectileBreakOnImpactChance;
                ((EntityProjectile)projectile).Weight = 0.0f;

                projectile.ServerPos.SetPos(firePos);
                projectile.ServerPos.Motion.Set(velocity);

                projectile.Pos.SetFrom(entity.ServerPos);
                projectile.World = entity.World;
                ((EntityProjectile)projectile).SetRotation();

                entity.World.SpawnEntity(projectile);
            }
            else if (stopIfPredictFriendlyFire && !didShoot)
            {

                Vec3d shotStartPosition = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
                Vec3d shotTargetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0);

                //If we care about shooting friendlies and we are going to shoot a friendly, early out.
                if (WillFriendlyFire(shotStartPosition, shotTargetPos))
                    return false;
            }

            return accum < durationMs / 1000f && !stopNow;
        }


        private float GetProjectileDamageAfterFalloff( float distToTargetSqr )
        {
            if (damageFalloffStartDist < 0.0f)
                damageFalloffStartDist = maxDist;

            if (damageFalloffEndDist < 0.0f)
                damageFalloffEndDist = maxDist;

            float currentFalloffPercentile = (float)MathUtility.GraphClampedValue(damageFalloffStartDist * damageFalloffStartDist, damageFalloffEndDist * damageFalloffEndDist, 0.0f, damageFalloffPercent, distToTargetSqr);

            return damage - (damage * currentFalloffPercentile);
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            targetEntity = null;
        }

        public bool WillFriendlyFire( Vec3d firePos, Vec3d shotTargetPos)
        {
            Vec3d shooterToTarget = shotTargetPos - firePos;
            shooterToTarget = shooterToTarget.Normalize();

            foreach(Entity herdMember in herdMembers)
            {
                if (!herdMember.Alive)
                    continue;

                Vec3d shooterToHerdMember = ( herdMember.ServerPos.XYZ.Add(0, herdMember.LocalEyePos.Y, 0) - firePos);
                shooterToHerdMember = shooterToHerdMember.Normalize();
                double dot = shooterToHerdMember.Dot(shooterToTarget);

                double distToFriend = firePos.SquareDistanceTo( herdMember.ServerPos.XYZ );

                //If we are really bunched up, don't fire;
                if (distToFriend <= 1.5 * 1.5)
                    return true;

                double friendlyFireDot = Math.Cos(15 * (Math.PI / 180));
                //If our ally is in our field of fire.
                if (dot >= friendlyFireDot)
                {
                    double distToTargetSqr = firePos.SquareDistanceTo(shotTargetPos);
                    double distToFriendSqr = firePos.SquareDistanceTo(herdMember.ServerPos.XYZ);

                    //If our friend seems to be between us and our target, don't fire.
                    if ( distToTargetSqr > distToFriendSqr)
                        return true;
                }
                    
            }

            return false;
        }

        public override bool Notify(string key, object data)
        {

            if (key == "entityAttackedGuardedEntity")
            {
                //If a guard task tells us our guard target has been attacked, engage the target as if they attacked us.
                if ((Entity)data != null && guardTargetAttackedByEntity != (Entity)data)
                {
                    guardTargetAttackedByEntity = (Entity)data;
                    targetEntity = guardTargetAttackedByEntity;
                    return false;
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
    }
}