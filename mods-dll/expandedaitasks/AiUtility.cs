using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.Threading.Tasks;

namespace ExpandedAiTasks
{
    public struct EntityTargetPairing
    {

        Entity _entityTargeting;
        Entity _targetEntity;

        public Entity entityTargeting
        {
            get { return _entityTargeting; }
            private set { _entityTargeting = value; }
        }

        public Entity targetEntity
        {
            get { return _targetEntity; }
            set { _targetEntity = value; }
        }

        public EntityTargetPairing( Entity entityTargeting, Entity targetEntity)
        {
            _entityTargeting = entityTargeting;
            _targetEntity = targetEntity;
        }
    }

    public static class AiUtility
    {
        //To Do: Make these attributes we can read from an AI file.
        private const double AI_HEARING_AWARENESS_SNEAK_MODIFIER = 0.0;
        private const double AI_HEARING_AWARENESS_STANDNG_MODIFIER = 1.0;
        private const double AI_HEARING_AWARENESS_WALK_MODIFIER = 1.2;
        private const double AI_HEARING_AWARENESS_SPRINT_MODIFIER = 2.0;

        private const double AI_VISION_AWARENESS_SNEAK_MODIFIER = 0.20;
        private const double AI_VISION_AWARENESS_STANDNG_MODIFIER = 0.5;
        private const double AI_VISION_AWARENESS_WALK_MODIFIER = 1.0;
        private const double AI_VISION_AWARENESS_SPRINT_MODIFIER = 1.0;

        //To Do: Consider adding smell. (See if there is a concept of wind direction.)

        private const double AI_HEARING_RANGE = 7.5;

        private const double MAX_LIGHT_LEVEL = 22;//12;
        private const double MIN_LIGHT_LEVEL = 4;
        private const double MAX_LIGHT_LEVEL_DETECTION_DIST = 60;
        private const double MIN_LIGHT_LEVEL_DETECTION_DIST = 2;

        private const float HERD_ALERT_RANGE = 30;

        //To Do: Consider narrowing AI FOV at night.
        private const float AI_VISION_FOV = 120;

        public static string GetAiTaskName( AiTaskBase task )
        {
            return AiTaskRegistry.TaskCodes[task.GetType()];
        }

        public static void SetGuardedEntity(Entity ent, Entity entToGuarded)
        {
            if (entToGuarded is EntityPlayer)
            {
                EntityPlayer guardedPlayer = entToGuarded as EntityPlayer;
                ent.WatchedAttributes.SetString("guardedPlayerUid", guardedPlayer.PlayerUID);
                ent.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
            }

            ent.WatchedAttributes.SetLong("guardedEntityId", entToGuarded.EntityId);
            ent.WatchedAttributes.MarkPathDirty("guardedEntityId");
        }

        public static void SetLastAttacker( Entity ent, DamageSource damageSource)
        {

            Entity attacker = damageSource.SourceEntity;

            if (attacker is EntityProjectile && damageSource.CauseEntity != null)
            {
                attacker = damageSource.CauseEntity;
            }

            if (attacker is EntityPlayer)
            {
                ent.Attributes.SetString("lastPlayerAttackerUid", (attacker as EntityPlayer).PlayerUID);
                ent.Attributes.SetDouble("lastTimeAttackedMs", ent.World.ElapsedMilliseconds);

                if (ent.Attributes.HasAttribute("lastEntAttackerEntityId"))
                    ent.Attributes.RemoveAttribute("lastEntAttackerEntityId");
            }
            else if (attacker != null)
            {
                ent.Attributes.SetLong("lastEntAttackerEntityId", attacker.EntityId);
                ent.Attributes.SetDouble("lastTimeAttackedMs", ent.World.ElapsedMilliseconds);

                if (ent.Attributes.HasAttribute("lastPlayerAttackerUid"))
                    ent.Attributes.RemoveAttribute("lastPlayerAttackerUid");
            }

            ent.Attributes.MarkAllDirty();
        }

        public static Entity GetLastAttacker( Entity ent )
        {
            string Uid = ent.Attributes.GetString("lastPlayerAttackerUid");
            if (Uid != null)
            {
                return ent.World.PlayerByUid(Uid)?.Entity;
            }

            long entId = ent.Attributes.GetLong("lastEntAttackerEntityId", 0L);
            return ent.World.GetEntityById(entId);
        }

        public static double GetLastTimeAttackedMs( Entity ent)
        {
            //There's an issue where where lastTimeAttackedMs this is saved on the ent, which we don't want.
            //Until we can find a way to store and update this at runtime without native behavior saving it,
            //we have to manually zero out the loaded bad value. 
            double lastAttackedMs = ent.Attributes.GetDouble("lastTimeAttackedMs");
            if ( lastAttackedMs > ent.World.ElapsedMilliseconds)
            {
                ent.Attributes.SetDouble("lastTimeAttackedMs", 0);
                lastAttackedMs = 0;
            }

            return lastAttackedMs;
        }

        public static bool AttackWasFromProjectile( DamageSource damageSource )
        {
            return damageSource.SourceEntity is EntityProjectile;
        }

        public static void UpdateLastTimeEntityInCombatMs( Entity ent )
        {
            ent.Attributes.SetDouble("lastTimeInCombatMs", ent.World.ElapsedMilliseconds);
        }

        public static double GetLastTimeEntityInCombatMs(Entity ent)
        {
            //There's an issue where where lastTimeInCombatMs this is saved on the ent, which we don't want.
            //Until we can find a way to store and update this at runtime without native behavior saving it,
            //we have to manually zero out the loaded bad value. 
            double lastInCombatMs = ent.Attributes.GetDouble("lastTimeInCombatMs");
            if (lastInCombatMs > ent.World.ElapsedMilliseconds)
            {
                ent.Attributes.SetDouble("lastTimeInCombatMs", 0);
                lastInCombatMs = 0;
            }

            return lastInCombatMs;
        }

        public static void SetMasterHerdList( Entity ent, List<Entity> herdList )
        {
            List<long> herdListEntIds = new List<long>();
            foreach( Entity agent in herdList )
            {
                if (agent != null)
                    herdListEntIds.Add(agent.EntityId);
            }

            long[] herdEntIdArray = herdListEntIds.ToArray();
            ent.Attributes.SetBytes("herdMembers", SerializerUtil.Serialize(herdEntIdArray));
        }

        public static List<Entity> GetMasterHerdList( Entity ent )
        {
            List<Entity> herdMembers = new List<Entity>();
            if ( ent.Attributes.HasAttribute("herdMembers") )
            {
                long[] herdEntIdArray = SerializerUtil.Deserialize<long[]>(ent.Attributes.GetBytes("herdMembers"));

                foreach( long id in herdEntIdArray)
                {
                    Entity herdMember = ent.World.GetEntityById(id);

                    if ( herdMember != null )
                        herdMembers.Add( herdMember );
                }
            }

            return herdMembers;
        }

        public static void JoinSameHerdAsEntity( Entity newMember, Entity currentMember )
        {
            Debug.Assert(newMember is EntityAgent, "Only entity agents can join a herd.");
            Debug.Assert(currentMember is EntityAgent, "Entity " + currentMember + " is not a member of a herd");

            EntityAgent newMemberAgent = newMember as EntityAgent;
            EntityAgent currentMemberAgent = currentMember as EntityAgent;

            newMemberAgent.HerdId = currentMemberAgent.HerdId;

            //Remove me from my old herd.
            List<Entity> oldHerdMembers = GetMasterHerdList(newMember);
            oldHerdMembers.Remove(newMember);

            //Inform members of my old herd.
            foreach (Entity herdMember in oldHerdMembers)
                SetMasterHerdList(herdMember, oldHerdMembers);

            //Add me to the new herd.
            List<Entity> newHerdMembers = GetMasterHerdList(currentMember);
            newHerdMembers.Add(newMember);

            //Inform members of my new herd.
            foreach (Entity herdMember in newHerdMembers)
                SetMasterHerdList(herdMember, newHerdMembers);
        }

        public static List<Entity> GetHerdMembersInRangeOfPos( Entity ent, Vec3d pos, float range )
        {
            List<Entity> allHerdMembers = GetMasterHerdList(ent);
            List<Entity> membersInRange = new List<Entity>();
            foreach( Entity member in allHerdMembers)
            {
                double distSqr = member.ServerPos.XYZ.SquareDistanceTo(pos);
                if ( distSqr <= range * range )
                    membersInRange.Add(member);
            }

            return membersInRange;
            
        }

        public static void TryNotifyHerdMembersToAttack(EntityAgent alertEntity, Entity targetEntity, float alertRange, bool requireAwarenessToNotify )
        {
            if ( alertEntity.HerdId > 0 )
            {
                List<Entity> membersInRange = GetHerdMembersInRangeOfPos( alertEntity, alertEntity.ServerPos.XYZ, alertRange );
                foreach (EntityAgent herdMember in membersInRange)
                {
                    if (herdMember.EntityId != alertEntity.EntityId && herdMember.HerdId == alertEntity.HerdId)
                    {
                        if ( !requireAwarenessToNotify || IsAwareOfTarget(herdMember, alertEntity, alertRange, alertRange) )
                        {
                            EntityTargetPairing targetPairing = new EntityTargetPairing(alertEntity, targetEntity);
                            herdMember.Notify("attackEntity", targetPairing);
                        } 
                    }
                }
            }
        }

        public static float GetHerdAlertRangeForEntity( Entity ent )
        {
            //We can replace this with a Attribute in the future if we want.
            return HERD_ALERT_RANGE;
        }

        public static bool IsInCombat( Entity ent )
        {
            if (ent is EntityPlayer)
                return false;

            if ( ent is EntityAgent)
            {
                AiTaskManager taskManager = ent.GetBehavior<EntityBehaviorTaskAI>().TaskManager;

                if (taskManager != null)
                {
                    List<IAiTask> tasks = taskManager.AllTasks;
                    foreach (IAiTask task in tasks)
                    {
                        if (task is AiTaskBaseTargetable)
                        {
                            AiTaskBaseTargetable baseTargetable = (AiTaskBaseTargetable)task;

                            //If we are fleeing, we are in combat. (Not the same as morale)
                            if (task is AiTaskFleeEntity && taskManager.IsTaskActive( task.Id ) )
                                return true;

                            //If not an agressive action.
                            if (!baseTargetable.AggressiveTargeting)
                                continue;

                            //If we have a target entity and hostile intent, then we are in combat.
                            if (baseTargetable.TargetEntity != null && baseTargetable.TargetEntity.Alive && !AreMembersOfSameHerd(ent, baseTargetable.TargetEntity))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsRoutingFromBattle(Entity ent)
        {
            if (ent is EntityPlayer)
                return false;

            if (ent is EntityAgent)
            {
                AiTaskManager taskManager = ent.GetBehavior<EntityBehaviorTaskAI>().TaskManager;

                if (taskManager != null)
                {
                    List<IAiTask> tasks = taskManager.AllTasks;
                    foreach (IAiTask task in tasks)
                    {
                        if (task is AiTaskMorale)
                        {
                            if (taskManager.IsTaskActive(task.Id))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        public static double CalculateInjuryRatio( Entity ent )
        {
            ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");

            if (treeAttribute != null)
            {
                double currentHealth = treeAttribute.GetFloat("currenthealth"); ;
                double maxHealth = treeAttribute.GetFloat("maxhealth"); ;

                return (maxHealth - currentHealth) / maxHealth;
            }

            return 0.0;
        }

        public static double CalculateHerdInjuryRatio(List<Entity> herdMembers)
        {
            if (herdMembers.Count == 0)
                return 0;

            double totalCurrentHealth = 0f;
            double totalMaxHealth = 0f;
            foreach (Entity herdMember in herdMembers)
            {
                ITreeAttribute treeAttribute = herdMember.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute != null)
                {
                    totalCurrentHealth += treeAttribute.GetFloat("currenthealth"); ;
                    totalMaxHealth += treeAttribute.GetFloat("maxhealth"); ;
                }
            }

            return (totalMaxHealth - totalCurrentHealth) / totalMaxHealth;
        }

        public static double PercentOfHerdAlive(List<Entity> herdMembers)
        {
            if (herdMembers.Count == 0)
                return 0;

            int aliveCount = 0;
            foreach (Entity herdMember in herdMembers)
            {
                if (herdMember.Alive)
                    aliveCount++;
            }

            double percentLiving = (double)aliveCount / (double)herdMembers.Count;
            return percentLiving;
        }

        public static bool AreMembersOfSameHerd( Entity ent1, Entity ent2 )
        {
            if (!(ent1 is EntityAgent))
                return false;

            if (!(ent2 is EntityAgent))
                return false;

            EntityAgent agent1 = ent1 as EntityAgent;
            EntityAgent agent2 = ent2 as EntityAgent;

            return agent1.HerdId == agent2.HerdId;
        }

        public static List<Entity> GetHerdMembersInRangeOfPos( List<Entity> herdMembers, Vec3d pos, float range )
        {
            List<Entity> herdMembersInRange = new List<Entity>();
            foreach( Entity herdMember in herdMembers)
            {
                double distSqr = herdMember.ServerPos.XYZ.SquareDistanceTo(pos);
                
                if (distSqr <= range * range)
                    herdMembersInRange.Add(herdMember);
            }
            return herdMembersInRange;
        }

        public static bool IsPlayerWithinRangeOfPos(EntityPlayer player, Vec3d pos, float range)
        {
            double distSqr = player.ServerPos.XYZ.SquareDistanceTo(pos);
            if (distSqr <= range * range)
                return true;

            return false;
        }

        public static bool IsAnyPlayerWithinRangeOfPos(Vec3d pos, float range, IWorldAccessor world)
        {
            IPlayer[] playersOnline = world.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                EntityPlayer playerEnt = player.Entity;
                if (IsPlayerWithinRangeOfPos(playerEnt, pos, range))
                    return true;
            }

            return false;
        }

        public static bool CanAnyPlayerSeePos( Vec3d pos, float autoPassRange, IWorldAccessor world, Entity[] ignoreEnts = null )
        {
            IPlayer[] playersOnline = world.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                EntityPlayer playerEnt = player.Entity;

                if (IsPlayerWithinRangeOfPos(playerEnt, pos, autoPassRange))
                {
                    if (CanEntSeePos(playerEnt, pos, 160, ignoreEnts))
                        return true;
                }
            }

            return false;
        }

        public static bool CanAnyPlayerSeeMe( Entity ent, float autoPassRange, Entity[] ignoreEnts = null)
        {
            Vec3d myEyePos = ent.ServerPos.XYZ.Add(0, ent.LocalEyePos.Y, 0);
            return CanAnyPlayerSeePos( myEyePos, autoPassRange, ent.World, ignoreEnts);
        }

        private static BlockSelection blockSel = null;
        private static EntitySelection entitySel = null;
        private static Entity losTraceSourceEnt = null;
        private static Entity[] ignoreEnts = null;

        public static bool CanEntSeePos( Entity ent, Vec3d pos, float fov, Entity[] entsToIgnore = null)
        {
            blockSel = null;
            entitySel = null;
            losTraceSourceEnt = ent;
            ignoreEnts = entsToIgnore;

            Vec3d entEyePos = ent.ServerPos.XYZ.Add(0, ent.LocalEyePos.Y, 0);
            Vec3d entViewForward = GetEntityForwardViewVector(ent, pos);

            Vec3d entToPos = pos - entEyePos;
            entToPos = entToPos.Normalize();

            double maxViewDot = Math.Cos( (fov / 2) * (Math.PI / 180));
            double dot = entViewForward.Dot(entToPos);

            if (dot > maxViewDot)
            {
                ent.World.RayTraceForSelection(entEyePos, pos, ref blockSel, ref entitySel, CanEntSeePos_BlockFilter, CanEntSeePos_EntityFilter);

                if (blockSel == null && entitySel == null)
                    return true;
            }

            return false;
        }

        private static bool CanEntSeePos_BlockFilter(BlockPos pos, Block block)
        {
            //Leaves block visability
            if (block.BlockMaterial == EnumBlockMaterial.Leaves)
                return false;

            //Plants block visability
            if (block.BlockMaterial == EnumBlockMaterial.Plant)
                return false;

            //Liquid Blocks visability
            if (block.BlockMaterial == EnumBlockMaterial.Liquid)
                return false;

            return true;
        }

        private static bool CanEntSeePos_EntityFilter(Entity ent)
        {
            if (ent == losTraceSourceEnt)
                return false;

            if ( ignoreEnts != null )
            {
                if (ignoreEnts.Contains(ent))
                    return false;
            }

            return true;
        }

        public static Vec3d GetCenterMass( Entity ent)
        {
            if (ent.SelectionBox.Empty)
                return ent.SidedPos.XYZ;

            float heightOffset = ent.SelectionBox.Y2 - ent.SelectionBox.Y1;
            return ent.SidedPos.XYZ.Add(0, heightOffset, 0);
        }

        public static Vec3d GetEntityForwardViewVector(Entity ent, Vec3d pitchPoint)
        {
            if ( ent is EntityPlayer)
                return GetPlayerForwardViewVector(ent);

            return GetAiForwardViewVectorWithPitchTowardsPoint(ent, pitchPoint);
        }

        public static Vec3d GetPlayerForwardViewVector( Entity player)
        {
            Debug.Assert ( player is EntityPlayer );

            Vec3d playerEyePos = player.ServerPos.XYZ.Add(0, player.LocalEyePos.Y, 0);
            Vec3d playerAheadPos = playerEyePos.AheadCopy(1, player.ServerPos.Pitch, player.ServerPos.Yaw);
            return (playerAheadPos - playerEyePos).Normalize();
        }

        public static Vec3d GetAiForwardViewVectorWithPitchTowardsPoint(Entity ent, Vec3d pitchPoint)
        {
            //WORK AROUND FOR VS ENGINE BUG:
            //This split in the view vector function is to adress more VS core engine badness.
            //With ents other than the player, their forward vector is offset by 90 degrees in yaw to the right of their forward, i.e. you get their right vector, not their forward.
            //It's really messy and bad, but we're correcting for it here because it's not clear how deep the issue goes and we can't modify the core engine to fix it.

            //View Forward Issue for Non-Players
            //Non-player entities currentlyhave their pitch locked to the horizon. We need to calculate pitch as if the Ai Is looking above or below the horizon,
            //and only account for Yaw when calculating view forward.

            Vec3d entEyePos = ent.ServerPos.XYZ.Add(0, ent.LocalEyePos.Y, 0);

            double opposite = (pitchPoint.Y - entEyePos.Y);
            int dirScalar = opposite < 0 ? -1 : 1;
            double oppositeSqr = (opposite * opposite) * dirScalar;

            Vec3d dirFromEntToPoint2D = new Vec3d( pitchPoint.X-entEyePos.X, 0, pitchPoint.Z - entEyePos.Z);

            //Try to save the square
            double adjacentSqr = dirFromEntToPoint2D.LengthSq();

            double pitch = Math.Atan2(oppositeSqr, adjacentSqr);

            double pitchDeg = pitch / (Math.PI / 180);

            Vec3d eyePos = ent.ServerPos.XYZ.Add(0, ent.LocalEyePos.Y, 0);
            Vec3d aheadPos = eyePos.AheadCopy(1, pitch, ent.ServerPos.Yaw + (90 * (Math.PI / 180)));
            return (aheadPos - eyePos).Normalize();
        }

        public static double GetAiHearingAwarenessScalarForPlayerMovementType(EntityPlayer playerEnt)
        {
            if (playerEnt.Controls.Sneak && playerEnt.OnGround)
            {
                return AI_HEARING_AWARENESS_SNEAK_MODIFIER;
            }
            else if (playerEnt.Controls.Sprint && playerEnt.OnGround)
            {
                return AI_HEARING_AWARENESS_SPRINT_MODIFIER;
            }
            else if (playerEnt.Controls.TriesToMove && playerEnt.OnGround)
            {
                return AI_HEARING_AWARENESS_WALK_MODIFIER;
            }

            return AI_HEARING_AWARENESS_STANDNG_MODIFIER;
        }

        public static double GetAiVisionAwarenessScalarForPlayerMovementType(EntityPlayer playerEnt)
        {
            if (playerEnt.Controls.Sneak && playerEnt.OnGround)
            {
                return AI_VISION_AWARENESS_SNEAK_MODIFIER;
            }
            else if (playerEnt.Controls.Sprint && playerEnt.OnGround)
            {
                return AI_VISION_AWARENESS_SPRINT_MODIFIER;
            }
            else if (playerEnt.Controls.TriesToMove && playerEnt.OnGround)
            {
                return AI_VISION_AWARENESS_WALK_MODIFIER;
            }

            return AI_VISION_AWARENESS_STANDNG_MODIFIER;
        }

        public static bool EntityHasNightVison( Entity entity )
        {
            if ( entity.Properties.Attributes.KeyExists("hasNightVision") )
            {
                return entity.Properties.Attributes["hasNightVision"].AsBool();
            }

            return false;
        }

        //TO DO: OPTIMIZE THE LIGHT CHECKING PORTION OF THIS FUNCTION SO IT ONLY RUNS ONCE PER FRAME, IF POSSIBLE.
        //1. We shouldn't run the light check multiple times per frame. Because we are running n number of light checks per n number of HasLOSContactWithTarget calls per frame.
        //2. We should make sure this function is only called where it needs to be called, it is called by the melee function and HasDirectContact may be a better option there.
        //3. This function does many similar things to CanSense, but gets called seperately, we need to determine whether the two should remain seperate.
        public static bool IsAwareOfTarget(Entity searchingEntity, Entity targetEntity, float maxDist, float maxVerDist)
        {

            //We cannot percieve ourself as a target.
            if (searchingEntity == targetEntity)
                return false;

            //If no players are within a reasonable range, don't spot anything just return true to save overhead.
            if (!AiUtility.IsAnyPlayerWithinRangeOfPos(targetEntity.ServerPos.XYZ, 250, targetEntity.World))
                return false;

            Cuboidd cuboidd = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d selectionBoxMidPoint = searchingEntity.ServerPos.XYZ.Add(0.0, searchingEntity.SelectionBox.Y2 / 2f, 0.0).Ahead(searchingEntity.SelectionBox.XSize / 2f, 0f, searchingEntity.ServerPos.Yaw);
            double shortestDist = cuboidd.ShortestDistanceFrom(selectionBoxMidPoint);
            double shortestVertDist = Math.Abs(cuboidd.ShortestVerticalDistanceFrom(selectionBoxMidPoint.Y));

            //////////////////////////
            ///BASIC DISTANCE CHECK///
            //////////////////////////

            //Scale Ai Awareness Based on How the player is Moving;
            double aiAwarenessHearingScalar = 1.0;
            double aiAwarenessVisionScalar = 1.0;

            if (targetEntity is EntityPlayer)
            {
                aiAwarenessHearingScalar = GetAiHearingAwarenessScalarForPlayerMovementType((EntityPlayer)targetEntity);
                aiAwarenessVisionScalar = GetAiVisionAwarenessScalarForPlayerMovementType((EntityPlayer)targetEntity);
            }

            double shortestHearingDist = shortestDist * aiAwarenessHearingScalar;
            double shortestHearingVertDist = shortestVertDist * aiAwarenessHearingScalar;
            double shortestVisionDist = shortestDist * aiAwarenessVisionScalar;
            double shortestVisionVertDist = shortestVertDist * aiAwarenessVisionScalar;

            if (shortestDist >= (double)maxDist || shortestVertDist >= (double)maxVerDist)
                return false;

            ///////////////////
            ///HEARING CHECK///
            ///////////////////

            //if we can hear the target moving, enage 
            double aiHearingRange = AI_HEARING_RANGE * aiAwarenessHearingScalar;
            if (shortestDist <= aiHearingRange && targetEntity.ServerPos.Motion.LengthSq() > 0)
                return true;

            //////////////////////////
            ///EYE-TO-EYE LOS CHECK///
            //////////////////////////
            //If we don't have direct line of sight to the target's eyes.
            Entity[] ignoreEnts = { targetEntity };
            if (!AiUtility.CanEntSeePos(searchingEntity, targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0), AI_VISION_FOV, ignoreEnts))
                return false;

            /////////////////
            ///LIGHT CHECK///
            /////////////////

            //If this Ai can see in the dark, we don't need to check lights.
            if (EntityHasNightVison( searchingEntity ) )
                return true;

            //If no players are within a close range, don't bother with illumination checks.
            if (!AiUtility.IsAnyPlayerWithinRangeOfPos(targetEntity.ServerPos.XYZ, 60, targetEntity.World))
                return true;

            //This ensures we only run one full illumination update every 500ms.
            int lightLevel = IlluminationManager.GetIlluminationLevelForEntity(targetEntity);
            double lightLevelDist = MathUtility.GraphClampedValue(MIN_LIGHT_LEVEL, MAX_LIGHT_LEVEL, MIN_LIGHT_LEVEL_DETECTION_DIST, MAX_LIGHT_LEVEL_DETECTION_DIST, (double)lightLevel);
            double lightLevelVisualAwarenessDist = lightLevelDist * aiAwarenessVisionScalar;

            if (shortestDist <= lightLevelVisualAwarenessDist)
                return true;

            return false;
        }
    }
}