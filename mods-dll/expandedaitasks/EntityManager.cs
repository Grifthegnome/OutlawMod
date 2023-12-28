using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace ExpandedAiTasks
{
    public static class EntityManager
    {
        //Look at optimizing this down to projectiles that are in flight, by removing stuck projectiles.
        private static ManagedEntityArray _meaEntityProjectiles = new ManagedEntityArray();
        private static ManagedEntityArray _meaEntityProjectilesInFlight = new ManagedEntityArray();
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

        public static List<EntityProjectile> GetAllEntityProjectilesWithinRangeOfPos( Vec3d pos, float range )
        {
            List<EntityProjectile> projectiles = entityProjectiles;
            List<EntityProjectile> projectilesInRange = new List<EntityProjectile>();
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
            List<EntityProjectile> projectilesInFlightInRange = new List<EntityProjectile>();
            foreach (EntityProjectile projectile in projectiles)
            {
                if (projectile.ServerPos.SquareDistanceTo(pos) <= range * range)
                {
                    projectilesInFlightInRange.Add(projectile);
                }
            }

            return projectilesInFlightInRange;
        }
    }
}
