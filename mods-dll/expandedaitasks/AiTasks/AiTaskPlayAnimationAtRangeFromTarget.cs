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
using System.Text;
using Vintagestory.API.Client;

namespace ExpandedAiTasks
{
    public class AiTaskPlayAnimationAtRangeFromTarget : AiTaskBaseExpandedTargetable
    {
        protected string[] cancelAnimations = null;
        protected float minDist = 1.5f;
        protected float minVerDist = 1f;

        protected float easeIn = 0.0f;
        protected float easeOut = 0.0f;

        Entity guardTargetAttackedByEntity = null;

        bool stopNow;

        public AiTaskPlayAnimationAtRangeFromTarget(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            
            this.cancelAnimations = taskConfig["cancelAnimations"].AsArray<string>(new string[] { });
            this.minDist = taskConfig["minDist"].AsFloat(2f);
            this.minVerDist = taskConfig["minVerDist"].AsFloat(1f);
            this.easeIn = taskConfig["easeIn"].AsFloat(0.0f);
            this.easeOut = taskConfig["easeOut"].AsFloat(0.0f);

            animMeta.EaseInSpeed = this.easeIn;
            animMeta.EaseOutSpeed = this.easeOut;
            animMeta.BlendMode = EnumAnimationBlendMode.Average;
            animMeta.Weight = 0.1f;
        }

        public override bool ShouldExecute()
        {
            long ellapsedMs = entity.World.ElapsedMilliseconds;

            if ( cooldownUntilMs > ellapsedMs)
                return false;

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
                bestTarget = null;
                bestTargetDist = -1;
                partitionUtil.WalkEntityPartitions(entity.ServerPos.XYZ, minDist, (e) => GetBestTarget(e, minDist));
                targetEntity = bestTarget;
            }

            if (targetEntity != null)
            {
                return IsInRange(targetEntity);
            }
                

            return false;
        }

        Entity bestTarget = null;
        double bestTargetDist = -1;

        private bool GetBestTarget(Entity ent, float range)
        {
            double verticalDist = ent.ServerPos.Y - entity.ServerPos.Y;

            if (verticalDist < 0)
                verticalDist *= -1;

            if (verticalDist > minVerDist)
                return true;

            bool isTargetable = IsTargetableEntity(ent, minDist, false);
            bool isAware = AwarenessManager.IsAwareOfTarget(entity, ent, minDist, minVerDist);

            if ( isTargetable && isAware )
            {
                double distSqr = ent.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ);
                if ( bestTarget == null )
                {
                    bestTarget = ent;
                    bestTargetDist = distSqr;
                }
                else if (distSqr < bestTargetDist)
                {
                    bestTarget = ent;
                    bestTargetDist = distSqr;
                }
            }

            return true;
        }

        public override void StartExecute()
        {
            stopNow = false;

            //Play Anim, Etc.
            //base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetEntity == null)
                return false;

            if (!targetEntity.Alive)
                return false;

            bool animPaused = false;

            if (cancelAnimations != null)
            {
                foreach (string animation in cancelAnimations)
                {
                    if (entity.AnimManager.IsAnimationActive(animation))
                    {
                        for (int i = 0; i < entity.AnimManager.Animator.RunningAnimations.Length; i++)
                        {
                            if (entity.AnimManager.Animator.RunningAnimations[i].Animation.Code == animation)
                            {
                                float currentFrame = entity.AnimManager.Animator.RunningAnimations[i].CurrentFrame;
                                int totalFrames = entity.AnimManager.Animator.RunningAnimations[i].Animation.QuantityFrames;

                                //Check to see if we are more than five frames from ending the animation.
                                //This is to avoid a single frame pop to the default idle animation.
                                if (totalFrames - currentFrame > 5.0)
                                {
                                    entity.AnimManager.StopAnimation(animMeta.Code);
                                    animPaused = true;
                                    break;
                                }
                                    
                            }
                        }
                    }
                }
            }
            
            if (animPaused == false)
            {
                entity.AnimManager.StartAnimation(animMeta);
            }

            return IsInRange(targetEntity) && stopNow != true;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            targetEntity = null;
        }

        protected bool IsInRange( Entity targetEnt )
        {
            bool inHorizontalRange = entity.ServerPos.SquareHorDistanceTo(targetEnt.ServerPos.XYZ) <= minDist * minDist;

            double verticalDist = entity.ServerPos.XYZ.Y - targetEnt.ServerPos.XYZ.Y;
            verticalDist = verticalDist < 0 ? -verticalDist : verticalDist;

            bool inVerticalRange = (verticalDist <= minVerDist);

            return inHorizontalRange && inVerticalRange;
        }

        public override bool Notify(string key, object data)
        {
            if (!AiUtility.CanRespondToNotify(entity))
                return false;

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