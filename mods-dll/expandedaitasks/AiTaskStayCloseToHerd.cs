﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskStayCloseToHerd : AiTaskBase
    {
        protected Entity herdLeaderEntity;
        protected List<Entity> herdEnts;
        protected float moveSpeed = 0.03f;
        protected float range = 8f;
        protected float maxDistance = 3f;
        protected bool stuck = false;
        protected bool allowTeleport;
        protected float teleportAfterRange;

        protected Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToHerd(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            range = taskConfig["searchRange"].AsFloat(8f);
            maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            allowTeleport = taskConfig["allowTeleport"].AsBool();
            teleportAfterRange = taskConfig["teleportAfterRange"].AsFloat(30f);
        }


        public override bool ShouldExecute()
        {
            if (herdLeaderEntity == null || !herdLeaderEntity.Alive)
            {
                //Get all herd members.
                herdEnts = new List<Entity>();
                entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, (e) =>
                {
                    EntityAgent agent = e as EntityAgent;
                    if (e.EntityId != entity.EntityId && agent != null && agent.Alive && agent.HerdId == entity.HerdId)
                    {
                        herdEnts.Add(agent);
                    }

                    return false;
                });

                //Determine who the herd leader is
                long bestEntityId = entity.EntityId;
                Entity bestCanidate = entity;
                foreach ( Entity herdMember in herdEnts )
                {
                    if ( herdMember.EntityId < bestEntityId)
                    {
                        bestEntityId = herdMember.EntityId;
                        bestCanidate = herdMember;
                    }
                }

                //Set herd leader
                if (bestCanidate != null)
                    herdLeaderEntity = bestCanidate;

            }

            //If we are the herd leader, then we lead the herd.
            if (entity == herdLeaderEntity)
                return false;

            if (herdLeaderEntity != null && (!herdLeaderEntity.Alive || herdLeaderEntity.ShouldDespawn)) 
                herdLeaderEntity = null;
            
            if (herdLeaderEntity == null) 
                return false;

            if (pathTraverser.Active == true)
                return false;

            double x = herdLeaderEntity.ServerPos.X;
            double y = herdLeaderEntity.ServerPos.Y;
            double z = herdLeaderEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = herdLeaderEntity.SelectionBox.XSize;

            pathTraverser.NavigateTo(herdLeaderEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, false, 1000, true);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            double x = herdLeaderEntity.ServerPos.X + targetOffset.X;
            double y = herdLeaderEntity.ServerPos.Y;
            double z = herdLeaderEntity.ServerPos.Z + targetOffset.Z;

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            float dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            if (dist < 3 * 3)
            {
                pathTraverser.Stop();
                return false;
            }

            if (allowTeleport && dist > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05)
            {
                tryTeleport();
            }

            return !stuck && pathTraverser.Active;
        }

        private Vec3d findDecentTeleportPos()
        {
            var ba = entity.World.BlockAccessor;
            var rnd = entity.World.Rand;

            Vec3d pos = new Vec3d();
            BlockPos bpos = new BlockPos();
            for (int i = 0; i < 20; i++)
            {
                double rndx = rnd.NextDouble() * 10 - 5;
                double rndz = rnd.NextDouble() * 10 - 5;
                pos.Set(herdLeaderEntity.ServerPos.X + rndx, herdLeaderEntity.ServerPos.Y, herdLeaderEntity.ServerPos.Z + rndz);

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


        protected void tryTeleport()
        {
            if (!allowTeleport) return;
            Vec3d pos = findDecentTeleportPos();
            if (pos != null) entity.TeleportTo(pos);
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
        }

        protected void OnStuck()
        {
            stuck = true;
            tryTeleport();
            pathTraverser.Stop();
        }

        public override void OnNoPath(Vec3d target)
        {
            tryTeleport();
            pathTraverser.Stop();
        }

        protected void OnGoalReached()
        {
            pathTraverser.Stop();
        }
    }
}