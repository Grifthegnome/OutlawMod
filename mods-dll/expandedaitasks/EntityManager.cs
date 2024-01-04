using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace ExpandedAiTasks
{
    public static class EntityManager
    {
        //Look at optimizing this down to projectiles that are in flight, by removing stuck projectiles.
        private static ManagedEntityArray _meaEntityProjectiles = new ManagedEntityArray();
        private static ManagedEntityArray _meaEntityProjectilesInFlight = new ManagedEntityArray();
        private static ManagedEntityArray _meaEntityDead = new ManagedEntityArray();
        public static List<EntityProjectile> entityProjectiles
        {
            get
            {
                List<EntityProjectile> entityProjectiles = new List<EntityProjectile>();
                foreach ( Entity projectile in _meaEntityProjectiles.GetManagedList() )
                {
                    entityProjectiles.Add( projectile as EntityProjectile );
                }

                return entityProjectiles;
            }
        }

        public static List<EntityProjectile> entityProjectilesInFlight
        {
            get
            {
                List<EntityProjectile> entityProjectilesInFlight = new List<EntityProjectile>();
                _meaEntityProjectilesInFlight.FilterByCheckResult(ProjectileIsInFlight);
                foreach( Entity projectile in _meaEntityProjectilesInFlight.GetManagedList() )
                {
                    entityProjectilesInFlight.Add( projectile as EntityProjectile );
                }

                return entityProjectilesInFlight;
            }
        }

        public static List<Entity> deadEntities
        {
            get
            {
                return _meaEntityDead.GetManagedList();
            }
        }

        private static bool ProjectileIsInFlight( Entity entity )
        {
            return entity.ApplyGravity;
        }

        public static void OnEntityProjectileSpawn(Entity entity)
        {
            if ( entity is EntityProjectile )
            {
                _meaEntityProjectiles.AddEntity(entity);
                _meaEntityProjectilesInFlight.AddEntity(entity);
            }
                
        }

        public static void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            Debug.Assert( !entity.Alive );

            if (entity.ShouldDespawn)
                return;

            _meaEntityDead.AddEntity(entity);
        }

        private static List<EntityProjectile> projectilesInRange = new List<EntityProjectile>();
        public static List<EntityProjectile> GetAllEntityProjectilesWithinRangeOfPos( Vec3d pos, float range )
        {
            List<EntityProjectile> projectiles = entityProjectiles;
            projectilesInRange.Clear();
            foreach ( EntityProjectile projectile in projectiles )
            {
                if( projectile.ServerPos.SquareDistanceTo( pos ) <= range * range )
                {
                    projectilesInRange.Add(projectile);
                }
            }

            return projectilesInRange;
        }

        public static List<EntityProjectile> GetAllEntityProjectilesInFlightWithinRangeOfPos(Vec3d pos, float range)
        {
            List<EntityProjectile> projectiles = entityProjectilesInFlight ;
            projectilesInRange.Clear();
            foreach (EntityProjectile projectile in projectiles)
            {
                if (projectile.ServerPos.SquareDistanceTo(pos) <= range * range)
                {
                    projectilesInRange.Add(projectile);
                }
            }

            return projectilesInRange;
        }

        private static List<Entity> EntitiesInRange = new List<Entity>();
        public static List<Entity> GetAllDeadEntitiesRangeOfPos(Vec3d pos, float range)
        {
            EntitiesInRange.Clear();
            foreach (Entity entity in deadEntities)
            {
                if (entity.ServerPos.SquareDistanceTo(pos) <= range * range)
                {
                    EntitiesInRange.Add(entity);
                }
            }

            return EntitiesInRange;
        }

        public static Entity GetNearestEntity(List<Entity> entities, Vec3d position, double radius, ActionConsumable<Entity> matches = null)
        {
            Entity nearestEntity = null;
            double radiusSqr = radius * radius;
            double nearestDistanceSqr = radiusSqr;

            foreach (Entity entity in entities)
            {
                double distSqr = entity.SidedPos.SquareDistanceTo(position);

                if (distSqr < nearestDistanceSqr && matches(entity))
                {
                    nearestDistanceSqr = distSqr;
                    nearestEntity = entity;
                }
            }

            return nearestEntity;
        }
    }
}
