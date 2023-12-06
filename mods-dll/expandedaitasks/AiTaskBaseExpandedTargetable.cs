using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    //This class is derrived inherits from AiTaskBaseTargetable and adds functionality and fixes that we want in many of our expanded Ai Tasks.
 
    public class AiTaskBaseExpandedTargetable : AiTaskBaseTargetable
    {
        private const float HERD_SEARCH_RANGE_DEFAULT = 60f;

        private const double AI_AWARENESS_SNEAK_MODIFIER = 0.20;
        private const double AI_AWARENESS_STANDNG_MODIFIER = 1.0;
        private const double AI_AWARENESS_WALK_MODIFIER = 1.2;
        private const double AI_AWARENESS_SPRINT_MODIFIER = 2.0;

        private const double AI_HEARING_RANGE = 5;

        private const double MAX_LIGHT_LEVEL = 12;
        private const double MIN_LIGHT_LEVEL = 1;
        private const double MAX_LIGHT_LEVEL_DETECTION_DIST = 20;
        private const double MIN_LIGHT_LEVEL_DETECTION_DIST = 2;

        private const float MAX_DYNAMIC_LIGHT_SEARCH_DIST = 12.0f;

        protected const float AI_VISION_FOV = 120;

        protected bool hasNightVision = false;

        protected List<Entity> herdMembers = new List<Entity>();

    public AiTaskBaseExpandedTargetable(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            hasNightVision = taskConfig["hasNightVision"].AsBool(false);
        }

        protected double GetAiAwarenessForPlayerMovementType( EntityPlayer playerEnt)
        {
            if(playerEnt.Controls.Sneak && playerEnt.OnGround)
            {
                return AI_AWARENESS_SNEAK_MODIFIER;
            }
            else if (playerEnt.Controls.Sprint && playerEnt.OnGround)
            {
                return AI_AWARENESS_SPRINT_MODIFIER;
            }
            else if (playerEnt.Controls.TriesToMove && playerEnt.OnGround)
            {
                return AI_AWARENESS_WALK_MODIFIER;
            }

            return AI_AWARENESS_STANDNG_MODIFIER;
        }

        //TO DO: OPTIMIZE THE LIGHT CHECKING PORTION OF THIS FUNCTION SO IT ONLY RUNS ONCE PER FRAME, IF POSSIBLE.
        //1. We shouldn't run the light check multiple times per frame. Because we are running n number of light checks per n number of HasLOSContactWithTarget calls per frame.
        //2. We should make sure this function is only called where it needs to be called, it is called by the melee function and HasDirectContact may be a better option there.
        //3. This function does many similar things to CanSense, but gets called seperately, we need to determine whether the two should remain seperate.
        protected bool IsAwareOfTarget(Entity targetEntity, float minDist, float minVerDist)
        {
            Cuboidd cuboidd = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d selectionBoxMidPoint = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
            double shortestDist = cuboidd.ShortestDistanceFrom(selectionBoxMidPoint);
            double shortestVertDist = Math.Abs(cuboidd.ShortestVerticalDistanceFrom(selectionBoxMidPoint.Y));

            //////////////////////////
            ///BASIC DISTANCE CHECK///
            //////////////////////////

            //Scale Ai Awareness Based on How the player is Moving;
            double aiAwarenessScalar = 1.0;
            if (targetEntity is EntityPlayer)
                aiAwarenessScalar = GetAiAwarenessForPlayerMovementType((EntityPlayer)targetEntity);

            shortestDist *= aiAwarenessScalar;
            shortestVertDist *= aiAwarenessScalar;

            if (shortestDist >= (double)minDist || shortestVertDist >= (double)minVerDist)
                return false;

            //If no players are within a reasonable range, just return true to save overhead.
            if (!AiUtility.IsAnyPlayerWithinRangeOfPos(targetEntity.ServerPos.XYZ, 60, targetEntity.World))
                return true;

            ///////////////////
            ///HEARING CHECK///
            ///////////////////

            //if we can hear the target moving, enage 
            double aiHearingRange = AI_HEARING_RANGE * aiAwarenessScalar;
            if (aiHearingRange >= shortestDist && targetEntity.ServerPos.Motion.LengthSq() > 0)
                return true;

            //To do: Handle case where player is sprinting up behind Ai.
            //To Do: Make it so that a player is detectable when moving quickly behind an AI at close range. (i.e. a hearing check).

            //////////////////////////
            ///EYE-TO-EYE LOS CHECK///
            //////////////////////////
            //If we don't have direct line of sight to the target's eyes.
            Entity[] ignoreEnts = { targetEntity };
            if (!AiUtility.CanEntSeePos(entity, targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0), AI_VISION_FOV, ignoreEnts))
                return false;

            /////////////////
            ///LIGHT CHECK///
            /////////////////

            //If this Ai can see in the dark, we don't need to check lights.
            if (hasNightVision)
                return true;

            //This ensures we only run one full illumination update every 500ms.
            int lightLevel = IlluminationManager.GetIlluminationLevelForEntity(targetEntity);
            double lightLevelDist = MathUtility.GraphClampedValue(MIN_LIGHT_LEVEL, MAX_LIGHT_LEVEL, MIN_LIGHT_LEVEL_DETECTION_DIST, MAX_LIGHT_LEVEL_DETECTION_DIST, (double)lightLevel);

            if (lightLevelDist >= shortestDist)
                return true;

            return false;
        }

        private int brightestDynamicLightLevel = 0;

        protected bool IsLitByDynamicLight(Entity targetEntity, int ambientLightLevel)
        {
            brightestDynamicLightLevel = ambientLightLevel;
            
            EntityPartitioning partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
            partitionUtil.WalkEntityPartitions(targetEntity.ServerPos.XYZ, MAX_DYNAMIC_LIGHT_SEARCH_DIST, (ent) => GetBrightestDynamicLightLevel(ent, MAX_DYNAMIC_LIGHT_SEARCH_DIST));

            return brightestDynamicLightLevel > ambientLightLevel;
        }

        protected void GetBrightestDynamicLightLevel(Entity ent, float range)
        {
            //if we've already found a light source that is bright enough to ruin our stealth, skip all others.
            if (brightestDynamicLightLevel > MAX_LIGHT_LEVEL)
                return;

            if ( ent is EntityItem )
            {
                EntityItem itemEnt = (EntityItem)ent;

                if ( itemEnt.Itemstack.Block != null )
                {
                    if (itemEnt.Itemstack.Block.LightHsv[2] > brightestDynamicLightLevel)
                    {
                        brightestDynamicLightLevel = itemEnt.Itemstack.Block.LightHsv[2];
                    }    
                }

                return;
            }

            //If the player is holding a glowing object, see if it is brighter than the ambient environment.
            if (ent is EntityPlayer)
            {
                EntityPlayer targetPlayer = ent as EntityPlayer;

                ItemSlot rightSlot = targetPlayer.RightHandItemSlot;
                if (rightSlot.Itemstack != null)
                {
                    if (rightSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = rightSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > brightestDynamicLightLevel)
                            brightestDynamicLightLevel = lightHsv[2];
                    }
                }

                ItemSlot leftSlot = targetPlayer.LeftHandItemSlot;
                if (leftSlot.Itemstack != null)
                {
                    if (leftSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = leftSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > brightestDynamicLightLevel)
                            brightestDynamicLightLevel = lightHsv[2];
                    }
                }
            }
        }

        public override bool ShouldExecute()
        {
            Debug.Assert(false, "This function needs to be overriden and should never be called.");
            return false;
        }

        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            //We can never target our friends.
            if ( e != null && e is EntityAgent )
            {
                if (((EntityAgent)e).HerdId == entity.HerdId)
                    return false;
            }

            return base.IsTargetableEntity(e, range, ignoreEntityCode);
        }

        protected virtual void UpdateHerdCount(float range = HERD_SEARCH_RANGE_DEFAULT)
        {
            //Try to get herd ents from saved master list.
            herdMembers = AiUtility.GetMasterHerdList(entity);

            if (herdMembers.Count == 0)
            {
                //Get all herd members.
                herdMembers = new List<Entity>();
                entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, (ent) =>
                {
                    if (ent is EntityAgent)
                    {
                        EntityAgent agent = ent as EntityAgent;
                        if (agent.Alive && agent.HerdId == entity.HerdId)
                            herdMembers.Add(agent);
                    }

                    return false;
                });

                //Set new master list.
                AiUtility.SetMasterHerdList(entity, herdMembers);
            }
        }

        //This is an override for the default OnEntityHurt func that prevents Ai from aggoing on friendly herd members.
        public override void OnEntityHurt(DamageSource source, float damage)
        {
            if (source.SourceEntity is EntityAgent)
            {
                EntityAgent attacker = source.SourceEntity as EntityAgent;
                if ( attacker.HerdId != entity.HerdId)
                {
                    attackedByEntity = source.SourceEntity;
                    attackedByEntityMs = entity.World.ElapsedMilliseconds;
                }     
            }
            else
            {
                attackedByEntity = source.SourceEntity;
                attackedByEntityMs = entity.World.ElapsedMilliseconds;
            }
        }

        public virtual void ClearTargetHistory()
        {
            targetEntity = null;
            attackedByEntity = null;
            attackedByEntityMs = 0;
        }
    }
}