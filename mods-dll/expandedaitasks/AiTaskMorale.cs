using System;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskMorale : AiTaskBaseExpandedTargetable
    {
        ICoreServerAPI sapi;
        Vec3d targetPos = new Vec3d();
        
        float moveSpeed = 0.02f;
        float routDistance = 31f;
        float rallyTimeAfterRoutMs = 5000;
        bool cancelOnHurt = false;

        double minMorale = 0.5f;
        double maxMorale = 0.8f;
        float moraleRange = 15f;

        //Data for fear caused by entities in morale range.
        TreeAttribute[] entitySourcesOfFear = new TreeAttribute[0];
        Dictionary<string, double> entitySourcesOfFearWeightsByCodeExact = new Dictionary<string, double>();
        Dictionary<string, double> entitySourcesOfFearWeightsByCodePartial = new Dictionary<string, double>();

        //Data for fear caused by item and block stacks in morale range.
        TreeAttribute[] itemStackSourcesOfFear = new TreeAttribute[0];
        Dictionary<string, double> itemStackSourcesOfFearWeightsByCodeExact = new Dictionary<string, double>();
        Dictionary<string, double> itemStackSourcesOfFearWeightsByCodePartial = new Dictionary<string, double>();

        //Data for points of intrest in morale range.
        TreeAttribute[] poiSourcesOfFear = new TreeAttribute[0];
        Dictionary<string,double> poiSourcesOfFearWeightsByType = new Dictionary<string,double>();

        bool useGroupMorale = false;
        bool deathsImpactMorale = false;
        bool canRoutFromAnyEnemy = false;

        double moraleLevel = 0.0f;
        double fearLevel = 0.0f;
        
        long fleeStartMs;
        bool stuck;
        
        static POIRegistry poiregistry;

        double entitySourceOfFearTotalWeight = 0;
        double itemStackSourceOfFearTotalWeight = 0;
        double poiSourceOfFearTotalWeight = 0;

        float stepHeight;

        //EntityPartitioning partitionUtil;

        Vec3d tmpVec = new Vec3d();
        Vec3d collTmpVec = new Vec3d();

        bool cancelNow;
        public override bool AggressiveTargeting => false;


        public AiTaskMorale(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            cancelOnHurt = taskConfig["cancelOnHurt"].AsBool(false);
            routDistance = taskConfig["routDistance"].AsFloat(31);
            rallyTimeAfterRoutMs = taskConfig["rallyTimeAfterRouteMs"].AsInt(9000);

            minMorale = taskConfig["minMorale"].AsDouble(0.5f);
            maxMorale = taskConfig["maxMorale"].AsDouble(0.8f);
            moraleRange = taskConfig["moraleRange"].AsFloat(15f);

            useGroupMorale = taskConfig["useGroupMorale"].AsBool(false);
            deathsImpactMorale = taskConfig["deathsImpactMorale"].AsBool(false);
            canRoutFromAnyEnemy = taskConfig["canRoutFromAnyEnemy"].AsBool(false);

            //To Do: Once we build our data dictionaries from these trees, do we really need to save them on the entity?
            //To Do: Do we want to build and store these tables unqiuely for every entity, or do we want to store them statically per entity type?

            //Read Data from Entity Fear Tree.
            IAttribute entitySourcesOfFearData = taskConfig["entitySourcesOfFear"].ToAttribute();
            TreeAttribute[] entitySourcesOfFearAsTree = entitySourcesOfFearData?.GetValue() as TreeAttribute[];

            if (entitySourcesOfFearAsTree != null)
                entitySourcesOfFear = entitySourcesOfFearAsTree;

            //Read Data from Item Stack Fear Tree.
            IAttribute itemStackSourcesOfFearData = taskConfig["itemStackSourcesOfFear"].ToAttribute();
            TreeAttribute[] itemStackSourcesOfFearAsTree = itemStackSourcesOfFearData?.GetValue() as TreeAttribute[];

            if ( itemStackSourcesOfFearAsTree != null)
                itemStackSourcesOfFear = itemStackSourcesOfFearAsTree;

            //Read Data From Poi Fear Tree.
            IAttribute poiSourcesOfFearData = taskConfig["poiSourcesOfFear"].ToAttribute();
            TreeAttribute[] poiSourcesOfFearAsTree = poiSourcesOfFearData?.GetValue() as TreeAttribute[];

            if (poiSourcesOfFearAsTree != null)
                poiSourcesOfFear = poiSourcesOfFearAsTree;

            Debug.Assert(minMorale <= maxMorale, "minMorale must be less than or equal to maxMorale.");

            //Determin this Ai's morale level.
            moraleLevel = minMorale + ( ( maxMorale - minMorale ) * entity.World.Rand.NextDouble() );

            //Build data tables that relate the things the ai is afraid to fear weight values.
            BuildSourceOfFearTables();

            if (entity.Api.Side == EnumAppSide.Server)
            {
                sapi = entity.Api as ICoreServerAPI;
                poiregistry = sapi.ModLoader.GetModSystem<POIRegistry>();
            }
        }


        public override bool ShouldExecute()
        {
            soundChance = Math.Min(1.01f, soundChance + 1 / 500f);

            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) 
                return false;

            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) 
                return false;

            if (useGroupMorale)
            {
                UpdateHerdCount();
            }

            fearLevel = 0;

            //We need to calculate the Ai's fear level and route if we are too scared.
            double injuryRatio = 0.0;
            if (useGroupMorale)
                injuryRatio = AiUtility.CalculateHerdInjuryRatio( AiUtility.GetHerdMembersInRangeOfPos(herdMembers, entity.ServerPos.XYZ, moraleRange) );
            else
                injuryRatio = AiUtility.CalculateInjuryRatio( entity );

            Vec3d ownPos = entity.ServerPos.XYZ;

            entitySourceOfFearTotalWeight = 0;
            itemStackSourceOfFearTotalWeight = 0;
            poiSourceOfFearTotalWeight = 0;

            //targetEntity = partitionUtil.GetNearestInteractableEntity(ownPos, moraleRange, (e) => IsValidMoraleTarget(e, moraleRange, canRoutFromAnyEnemy));
            targetEntity = entity.World.GetNearestEntity(ownPos, moraleRange, moraleRange, IsValidMoraleTarget);

            if (targetEntity != null)
            {
                //Take the target's injury ratio into account.
                double targetInjuryRatio = AiUtility.CalculateInjuryRatio(targetEntity);
                double poiSourcesOfFearWeight = GetTotalPoiSourceOfFearWeight();

                //We should be less scared if we're winning.
                fearLevel = Math.Max(injuryRatio - targetInjuryRatio, 0) + entitySourceOfFearTotalWeight + itemStackSourceOfFearTotalWeight + poiSourcesOfFearWeight;

                if ( fearLevel >= moraleLevel)
                {
                    targetEntity.Notify("entityRouted", entity);
                    UpdateTargetPos();
                    return true;
                }
            }

            return false;
        }

        private bool IsTargetableItem(Entity ent)
        {
            //If this entity is an item, we should only count it as a target if we are afraid of it.
            if ( ent is EntityItem )
            {
                ItemStack itemStack = ((EntityItem)ent).Itemstack;
                string path = "";
                if (itemStack.Item != null)
                    path = itemStack.Item.Code.Path;
                else if (itemStack.Block != null)
                    path = itemStack.Block.Code.Path;
                
                if ( path != "" )
                {
                    //Try to match exact entity codes.
                    if (itemStackSourcesOfFearWeightsByCodeExact.ContainsKey(path))
                    {
                        return true;
                    }

                    //Try to match partials entity codes.
                    foreach (var codePartial in itemStackSourcesOfFearWeightsByCodePartial)
                    {
                        if (path.StartsWithFast(codePartial.Key))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsValidMoraleTarget(Entity ent)
        {

            //Handle case where our target is an enemy entity.
            if ( ent is EntityAgent )
            {
                EntityAgent agent = ent as EntityAgent;
                if (entitySourcesOfFearWeightsByCodeExact.Count > 0 || entitySourcesOfFearWeightsByCodePartial.Count > 0 || ent.Code.Path == "player")
                    entitySourceOfFearTotalWeight += GetEntitySourceOfFearWeight(ent);

                //Dead things can contribute to morale but not be the cause of a route.
                if (!ent.Alive)
                    return false;

                bool ignoreEntityCode = canRoutFromAnyEnemy;
                if (!IsTargetableEntity(ent, moraleRange, ignoreEntityCode) || !AiUtility.IsAwareOfTarget(entity, ent, moraleRange, moraleRange))
                    return false;

                //Don't be scared of our friends.
                if (agent.HerdId == entity.HerdId)
                    return false;
            }
            else
            {
                //Handle case where our target could be an item or block.
                if ( ent is EntityItem )
                {
                    EntityItem item = ent as EntityItem;
                    if ( itemStackSourcesOfFearWeightsByCodeExact.Count > 0 || itemStackSourcesOfFearWeightsByCodePartial.Count > 0 )
                        itemStackSourceOfFearTotalWeight += item.Itemstack != null ? GetItemStackSourceOfFearWeight(item.Itemstack) : 0;

                    //Don't target items if they don't scare us.
                    if (!IsTargetableItem(ent))
                        return false;
                }
            }

            //Currently, projectiles cannot scare us.
            if (ent is EntityProjectile)
                return false;

            return true;

        }
        protected override void UpdateHerdCount(float range = 60f)
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

        public override void StartExecute()
        {
            base.StartExecute();

            cancelNow = false;

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            stepHeight = bh == null ? 0.6f : bh.stepHeight;

            soundChance = Math.Max(0.025f, soundChance - 0.2f);

            float size = targetEntity.SelectionBox.XSize;

            pathTraverser.WalkTowards(targetPos, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);

            fleeStartMs = entity.World.ElapsedMilliseconds;
            stuck = false;

            entity.PlayEntitySound("morale", null, true);
        }


        public override bool ContinueExecute(float dt)
        {
            AiUtility.UpdateLastTimeEntityInCombatMs(entity);

            if (targetEntity == null)
                return false;

            if (world.Rand.NextDouble() < 0.2)
            {
                UpdateTargetPos();
                pathTraverser.CurrentTarget.X = targetPos.X;
                pathTraverser.CurrentTarget.Y = targetPos.Y;
                pathTraverser.CurrentTarget.Z = targetPos.Z;
            }

            if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) > routDistance * routDistance)
            {
                return false;
            }

            return !stuck && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeStartMs < rallyTimeAfterRoutMs) && !cancelNow && pathTraverser.Active;
        }



        private void UpdateTargetPos()
        {
            float yaw = (float)Math.Atan2(targetEntity.ServerPos.X - entity.ServerPos.X, targetEntity.ServerPos.Z - entity.ServerPos.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (Traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (Traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (Traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);
        }


        public override void OnEntityHurt(DamageSource source, float damage)
        {
            base.OnEntityHurt(source, damage);

            if (cancelOnHurt) cancelNow = true;
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
            pathTraverser.Stop();
            targetEntity = null;

            //Clear are whole target history, so we don't attempt to re-engage pre-rout targets.
            //To Do: Right now, we are using morale to make archers withdraw to a safe-firing distance, we do not want them to reset their target in this case.
            entity.Notify("clearTargetHistory", entity);
        }


        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
            pathTraverser.Retarget();
        }

        private void BuildSourceOfFearTables()
        {
          
            //Build dictionaries for entities that scare the Ai from our tree.
            if (entitySourcesOfFear != null)
            {
                for (int i = 0; i < entitySourcesOfFear.Length; i++)
                {
                    Debug.Assert(entitySourcesOfFear[i].HasAttribute("code"), "entitySourcesOfFear for " + entity.Code.Path + " is missing code: at entry " + i);
                    Debug.Assert(entitySourcesOfFear[i].HasAttribute("fearWeight"), "entitySourcesOfFear for " + entity.Code.Path + " is missing fearWeight: at entry " + i);

                    string code = entitySourcesOfFear[i].GetString("code");
                    double weight = entitySourcesOfFear[i].GetDouble("fearWeight");

                    if (code.EndsWith("*"))
                    {
                        //Handle Partial Entity Code
                        entitySourcesOfFearWeightsByCodePartial.Add(code.Substring(0, code.Length - 1), weight);
                    }
                    else
                    {
                        //Handle Exact Entity Code
                        entitySourcesOfFearWeightsByCodeExact.Add(code, weight);
                    }
                }
            }

            //Build dictionaries for item stacks that scare the Ai from our tree.
            if (itemStackSourcesOfFear != null)
            {
                for (int i = 0; i < itemStackSourcesOfFear.Length; i++)
                {
                    Debug.Assert(itemStackSourcesOfFear[i].HasAttribute("code"), "itemStackSourcesOfFear for " + entity.Code.Path + " is missing code: at entry " + i);
                    Debug.Assert(itemStackSourcesOfFear[i].HasAttribute("fearWeight"), "itemStackSourcesOfFear for " + entity.Code.Path + " is missing fearWeight: at entry " + i);

                    string code = itemStackSourcesOfFear[i].GetString("code");
                    double weight = itemStackSourcesOfFear[i].GetDouble("fearWeight");

                    if (code.EndsWith("*"))
                    {
                        //Handle Partial Entity Code
                        itemStackSourcesOfFearWeightsByCodePartial.Add(code.Substring(0, code.Length - 1), weight);
                    }
                    else
                    {
                        //Handle Exact Entity Code
                        itemStackSourcesOfFearWeightsByCodeExact.Add(code, weight);
                    }
                }
            }

            //Build table for pois that scare the Ai.
            if ( poiSourcesOfFear != null)
            {
                for (int i = 0; i < poiSourcesOfFear.Length; i++)
                {
                    Debug.Assert(poiSourcesOfFear[i].HasAttribute("poiType"), "poiSourcesOfFear for " + entity.Code.Path + " is missing poiType: at entry " + i);
                    Debug.Assert(poiSourcesOfFear[i].HasAttribute("fearWeight"), "poiSourcesOfFear for " + entity.Code.Path + " is missing fearWeight: at entry " + i);

                    string poiType = poiSourcesOfFear[i].GetString("poiType");
                    double weight = poiSourcesOfFear[i].GetDouble("fearWeight");
                    poiSourcesOfFearWeightsByType.Add(poiType, weight);
                }
            }
        }

        private double GetTotalPoiSourceOfFearWeight()
        {
            poiSourceOfFearTotalWeight = 0;

            //We have to overshoot our morale bounds so that we can enclude all the chunks that might fall anywhere within our search.
            poiregistry.WalkPois(entity.ServerPos.XYZ, moraleRange + entity.World.BlockAccessor.ChunkSize, PoiSourceOfFearMatcher);

            return poiSourceOfFearTotalWeight;
        }

        public bool PoiSourceOfFearMatcher(IPointOfInterest poi)
        {  
            if (poiSourcesOfFearWeightsByType.ContainsKey(poi.Type) )
            {
                //We have to do the morale range search in here because of the way chunk searching works.
                if ( poi.Position.SquareDistanceTo( entity.ServerPos.XYZ ) <= moraleRange * moraleRange )
                {
                    poiSourceOfFearTotalWeight += poiSourcesOfFearWeightsByType[poi.Type];
                    return true;
                }  
            }
            
            return false;
        }

        private double GetEntitySourceOfFearWeight( Entity ent)
        {
            //Note: When we count entities in terms of moral we are allowing entities to count themselves, this is to balance situations where the ai is fighting
            //a single player and the combat numbers are equal. (In the longterm, we may need a more robust solution).

            //We want entities such as items to be able to scare Ai, however in the case of living Ai, we only want them to scare the Ai if
            //They are alive.
            if ( ent is EntityAgent)
            {
                if ( (!ent.Alive && !deathsImpactMorale) || !ent.IsInteractable || !CanSense(ent, moraleRange))
                    return 0;
            }           

            //If the entity is a player, see if they have anything in their active hand slots we're scared of.
            if (ent is EntityPlayer)
            {
                EntityPlayer player = ent as EntityPlayer;
                ItemSlot rightSlot = player.RightHandItemSlot;
                if (rightSlot.Itemstack != null)
                    itemStackSourceOfFearTotalWeight += GetItemStackSourceOfFearWeight(rightSlot.Itemstack);

                ItemSlot leftSlot = player.LeftHandItemSlot;
                if ( leftSlot.Itemstack != null )
                    itemStackSourceOfFearTotalWeight += GetItemStackSourceOfFearWeight(leftSlot.Itemstack);
            }
               
            //Try to match exact.
            if (entitySourcesOfFearWeightsByCodeExact.ContainsKey(ent.Code.Path))
            {
                //If this ent is dead and we care about who's dead.
                //It has the opposite fear effect on us than when it's alive.
                float livingScalar = 1.0f;
                if (deathsImpactMorale)
                    livingScalar = ent.Alive ? 1.0f : -1.0f;

                return entitySourcesOfFearWeightsByCodeExact[ent.Code.Path] * livingScalar;
            }

            //Try to match partials.
            foreach (var codePartial in entitySourcesOfFearWeightsByCodePartial)
            {
                //If this ent is dead and we care about who's dead.
                //It has the opposite fear effect on us than when it's alive.
                float livingScalar = 1.0f;
                if (deathsImpactMorale)
                    livingScalar = ent.Alive ? 1.0f : -1.0f;

                if (ent.Code.Path.StartsWithFast(codePartial.Key))
                    return codePartial.Value * livingScalar;
            }

            return 0;
        }

        private double GetItemStackSourceOfFearWeight(ItemStack itemStack)
        {

            string path = "";
            if (itemStack.Item != null)
                path = itemStack.Item.Code.Path;
            else if (itemStack.Block != null)
                path = itemStack.Block.Code.Path;
            else
                return 0;

            //Try to match exact entity codes.
            if (itemStackSourcesOfFearWeightsByCodeExact.ContainsKey(path))
            {
                return itemStackSourcesOfFearWeightsByCodeExact[path];
            }

            //Try to match partials entity codes.
            foreach (var codePartial in itemStackSourcesOfFearWeightsByCodePartial)
            {
                if (path.StartsWithFast(codePartial.Key))
                {
                    return codePartial.Value;
                }

            }

            return 0;
        }
    }
}
