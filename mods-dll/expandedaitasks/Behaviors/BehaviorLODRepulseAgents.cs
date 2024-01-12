using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedAiTasks.Behaviors
{
    //This is a modified version of repulse agents that has a level of detail behavior based on player distance.
    //The farther the closest player is away, the less often and less detailed the repulse will be.
    //When an entity with behavior dies, it unflags itself as repulseable and stops running its own checks.
    public class EntityBehaviorLODRepulseAgents : EntityBehavior
    {
        Vec3d pushVector = new Vec3d();
        EntityPartitioning partitionUtil;
        bool movable = true;
        bool ignorePlayers = false;

        private const float LOD_DIST_REPULSE_OFF = 30;

        private const float LOD_NEAR_DIST = 10;
        private const float LOD_FAR_DIST = LOD_DIST_REPULSE_OFF;

        private const float LOD_NEAR_DIST_TICK_INTERVAL = 0;
        private const float LOD_FAR_DIST_TICK_INTERVAL = 1000;

        private const float PUSH_FORCE_DIVISOR_NEAR = 30;
        private const float PUSH_FORCE_DIVISOR_FAR = 5;

        private double lastUpdateTick = 0;

        public EntityBehaviorLODRepulseAgents(Entity entity) : base(entity)
        {
            entity.hasRepulseBehavior = true;
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            movable = attributes["movable"].AsBool(true);
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
            ignorePlayers = entity is EntityPlayer && entity.World.Config.GetAsBool("player2PlayerCollisions", true);
        }

        double ownPosRepulseX, ownPosRepulseY, ownPosRepulseZ;
        float mySize;

        public override void OnGameTick(float deltaTime)
        {
            if (entity.State == EnumEntityState.Inactive || !entity.IsInteractable || !movable) 
                return;

            if (!entity.Alive)
            {
                entity.hasRepulseBehavior = false;
                return;
            }

            if (entity.minRangeToClient > LOD_DIST_REPULSE_OFF)
                return;

            if (entity.World.ElapsedMilliseconds < 2000) 
                return;

            double tickInterval = MathUtility.GraphClampedValue(LOD_NEAR_DIST, LOD_FAR_DIST, LOD_NEAR_DIST_TICK_INTERVAL, LOD_FAR_DIST_TICK_INTERVAL, entity.minRangeToClient);

            if (lastUpdateTick + tickInterval > entity.World.ElapsedMilliseconds)
                return;

            double touchdist = entity.SelectionBox.XSize / 2;

            pushVector.Set(0, 0, 0);

            ownPosRepulseX = entity.ownPosRepulse.X;
            ownPosRepulseY = entity.ownPosRepulse.Y;
            ownPosRepulseZ = entity.ownPosRepulse.Z;
            mySize = entity.SelectionBox.Length * entity.SelectionBox.Height;

            partitionUtil.WalkEntityPartitions(entity.ownPosRepulse, touchdist + partitionUtil.LargestTouchDistance + 0.1, WalkEntity);

            pushVector.X = GameMath.Clamp(pushVector.X, -3, 3);
            pushVector.Y = GameMath.Clamp(pushVector.Y, -3, 3);
            pushVector.Z = GameMath.Clamp(pushVector.Z, -3, 3);

            double pushForceDivisor = MathUtility.GraphClampedValue(LOD_NEAR_DIST, LOD_FAR_DIST, PUSH_FORCE_DIVISOR_NEAR, PUSH_FORCE_DIVISOR_FAR, entity.minRangeToClient);

            entity.SidedPos.Motion.Add(pushVector.X / pushForceDivisor, pushVector.Y / pushForceDivisor, pushVector.Z / pushForceDivisor);

            lastUpdateTick = entity.World.ElapsedMilliseconds;
        }


        private bool WalkEntity(Entity e)
        {
            if (!e.hasRepulseBehavior || !e.IsInteractable || e == entity || (ignorePlayers && e is EntityPlayer) || !e.Alive) return true;

            double dx = ownPosRepulseX - e.ownPosRepulse.X;
            double dy = ownPosRepulseY - e.ownPosRepulse.Y;
            double dz = ownPosRepulseZ - e.ownPosRepulse.Z;

            double distSq = dx * dx + dy * dy + dz * dz;
            double minDistSq = entity.touchDistanceSq + e.touchDistanceSq;

            if (distSq >= minDistSq) return true;

            double pushForce = (1 - distSq / minDistSq) / Math.Max(0.001f, GameMath.Sqrt(distSq));
            double px = dx * pushForce;
            double py = dy * pushForce;
            double pz = dz * pushForce;

            float hisSize = e.SelectionBox.Length * e.SelectionBox.Height;
            float pushDiff = GameMath.Clamp(hisSize / mySize, 0, 1);

            pushVector.Add(px * pushDiff, py * pushDiff, pz * pushDiff);

            return true;
        }


        public override string PropertyName()
        {
            return "lodrepulseagents";
        }
    }
}
