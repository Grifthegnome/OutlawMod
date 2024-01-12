using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using ExpandedAiTasks.Managers;

namespace ExpandedAiTasks
{
    public class AiTaskExpandedMeleeAttack : AiTaskBaseExpandedTargetable
    {
        protected long lastCheckOrAttackMs;

        protected float damage = 2f;
        protected float knockbackStrength = 1f;
        protected float minDist = 1.5f;
        protected float minVerDist = 1f;


        protected bool damageInflicted = false;

        protected int attackDurationMs = 1500;
        protected int damagePlayerAtMs = 500;

        public EnumDamageType damageType = EnumDamageType.BluntAttack;
        public int damageTier = 0;

        Entity guardTargetAttackedByEntity = null;

        bool stopNow;

        public AiTaskExpandedMeleeAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            this.damage = taskConfig["damage"].AsFloat(2);
            this.knockbackStrength = taskConfig["knockbackStrength"].AsFloat(GameMath.Sqrt(damage / 2f));
            this.attackDurationMs = taskConfig["attackDurationMs"].AsInt(1500);
            this.damagePlayerAtMs = taskConfig["damagePlayerAtMs"].AsInt(1000);

            this.minDist = taskConfig["minDist"].AsFloat(2f);
            this.minVerDist = taskConfig["minVerDist"].AsFloat(1f);

            string strdt = taskConfig["damageType"].AsString();
            if (strdt != null)
            {
                this.damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), strdt, true);
            }
            this.damageTier = taskConfig["damageTier"].AsInt(0);

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("extraInfoText");
            tree.SetString("dmgTier", Lang.Get("Damage tier: {0}", damageTier));
        }

        public override bool ShouldExecute()
        {
            long ellapsedMs = entity.World.ElapsedMilliseconds;

            if (ellapsedMs - lastCheckOrAttackMs < attackDurationMs || cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) 
                return false;
            
            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) 
                return false;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            targetEntity = null;

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && IsTargetableEntity(attackedByEntity, minDist, true) && AwarenessManager.IsAwareOfTarget(entity, attackedByEntity, minDist, minVerDist))
            {
                targetEntity = attackedByEntity;
            }
            else if (guardTargetAttackedByEntity != null && guardTargetAttackedByEntity.Alive)
            {
                if (IsTargetableEntity(guardTargetAttackedByEntity, minDist, true) && AwarenessManager.IsAwareOfTarget(entity, guardTargetAttackedByEntity, minDist, minVerDist))
                    targetEntity = guardTargetAttackedByEntity;
            }
            else
            {
                guardTargetAttackedByEntity = null;
            }

            if (targetEntity == null || !targetEntity.Alive )
            {
                targetEntity = entity.World.GetNearestEntity(pos, minDist, minVerDist, (e) =>
                {
                    return IsTargetableEntity(e, minDist, false) && AwarenessManager.IsAwareOfTarget(entity, e, minDist, minVerDist);
                });
            }

            lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;
            damageInflicted = false;

            if (targetEntity != null)
                return IsInMeleeRange(targetEntity);

            return false;
        }


        float curTurnRadPerSec;
        bool didStartAnim;

        public override void StartExecute()
        {
            didStartAnim = false;
            stopNow = false;
            curTurnRadPerSec = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser.curTurnRadPerSec;
            entity.PlayEntitySound("melee", null, true);
        }

        public override bool ContinueExecute(float dt)
        {
            AiUtility.UpdateLastTimeEntityInCombatMs(entity);

            if (targetEntity == null)
                return false;

            EntityPos own = entity.ServerPos;
            EntityPos his = targetEntity.ServerPos;

            float desiredYaw = (float)Math.Atan2(his.X - own.X, his.Z - own.Z);
            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier, curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            bool correctYaw = Math.Abs(yawDist) < 20 * GameMath.DEG2RAD;
            if (correctYaw && !didStartAnim)
            {
                didStartAnim = true;
                base.StartExecute();
            }

            if (lastCheckOrAttackMs + damagePlayerAtMs > entity.World.ElapsedMilliseconds) 
                return true;

            if (!damageInflicted && correctYaw && IsInMeleeRange(targetEntity))
            {
                //To do: We should test if this check is really needed anymore.
                if (!IsTargetableEntity(targetEntity, minDist, true) || !AwarenessManager.IsAwareOfTarget(entity, targetEntity, minDist, minVerDist)) 
                    return false;

                bool alive = targetEntity.Alive;

                targetEntity.ReceiveDamage(
                    new DamageSource()
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = damageType,
                        DamageTier = damageTier,
                        KnockbackStrength = knockbackStrength
                    },
                    damage * GlobalConstants.CreatureDamageModifier
                );

                if (alive && !targetEntity.Alive)
                {
                    bhEmo?.TryTriggerState("saturated", targetEntity.EntityId);
                }

                damageInflicted = true;
            }

            if (lastCheckOrAttackMs + attackDurationMs > entity.World.ElapsedMilliseconds) 
                return true && !stopNow;
            
            return false;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            targetEntity = null;
        }

        protected bool IsInMeleeRange( Entity targetEnt )
        {
            bool inHorizontalRange = entity.ServerPos.SquareHorDistanceTo(targetEnt.ServerPos.XYZ) <= minDist * minDist;

            double verticalDist = entity.ServerPos.XYZ.Y - targetEnt.ServerPos.XYZ.Y;
            verticalDist = verticalDist < 0 ? -verticalDist : verticalDist;

            bool inVerticalRange = (verticalDist <= minVerDist);

            return inHorizontalRange && inVerticalRange;
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