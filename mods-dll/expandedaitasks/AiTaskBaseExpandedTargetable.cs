﻿using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    //This class is derrived inherits from AiTaskBaseTargetable and adds functionality and fixes that we want in many of our expanded Ai Tasks.
 
    public class AiTaskBaseExpandedTargetable : AiTaskBaseTargetable
    {
        private const float HERD_SEARCH_RANGE_DEFAULT = 60f;

        protected List<Entity> herdMembers = new List<Entity>();

    public AiTaskBaseExpandedTargetable(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
        }
        public override bool ShouldExecute()
        {
            Debug.Assert(false, "This function needs to be overriden and should never be called.");
            return false;
        }

        protected virtual void UpdateHerdCount(float range = HERD_SEARCH_RANGE_DEFAULT)
        {
            //Try to get herd ents from saved master list.
            herdMembers = AiUtility.GetMasterHerdList(entity, false);

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
    }
}