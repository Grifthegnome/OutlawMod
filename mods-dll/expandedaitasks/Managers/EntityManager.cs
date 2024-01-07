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
using ExpandedAiTasks.DataTypes;

namespace ExpandedAiTasks.Managers
{
 /*
  _____       _   _ _           _             _                     
 | ____|_ __ | |_(_) |_ _   _  | |    ___  __| | __ _  ___ _ __ ___ 
 |  _| | '_ \| __| | __| | | | | |   / _ \/ _` |/ _` |/ _ \ '__/ __|
 | |___| | | | |_| | |_| |_| | | |__|  __/ (_| | (_| |  __/ |  \__ \
 |_____|_| |_|\__|_|\__|\__, | |_____\___|\__,_|\__, |\___|_|  |___/
                        |___/                   |___/       
 */

    public struct EntityLedger
    {
        private Dictionary<string, EntityLedgerEntry> ledgerEntries;
        long lastEntIDAdded = -1;
        long lastEntItemIDAdded = -1;

        public EntityLedger() 
        { 
            ledgerEntries = new Dictionary<string, EntityLedgerEntry>();
        }

        public void AddEntityToLedger( Entity entity )
        {
            //Hack: It appears that the Vintage Story API might be firing OnEntitySpawned twice on the server.
            //Until I can talk to the VS Devs about why that might be happning, we have to early out if we encounter
            //The same ent id twice in a row.
            if (entity.EntityId == lastEntIDAdded)
                return;

            string codeStart = entity.FirstCodePart();
            AssetLocation searchCode = entity.Code;

            if (ledgerEntries.ContainsKey( codeStart ) )
            {
                ledgerEntries[ codeStart ].AddEntity(searchCode, entity );
            }
            else
            {
                ledgerEntries.Add(codeStart, new EntityLedgerEntry(searchCode, entity));
            }

            lastEntIDAdded = entity.EntityId;
        }

        public void AddEntityItemToLedger( Entity entity )
        {
            Debug.Assert(entity is EntityItem);
            Debug.Assert(entity.EntityId != lastEntItemIDAdded);

            EntityItem item = (EntityItem)entity;
            Debug.Assert(item.Itemstack != null);

            string codeStart = item.Itemstack.Block != null ? item.Itemstack.Block.Code.FirstCodePart() : item.Itemstack.Item.Code.FirstCodePart();
            AssetLocation searchCode = item.Itemstack.Block != null ? item.Itemstack.Block.Code : item.Itemstack.Item.Code;

            if (ledgerEntries.ContainsKey(codeStart))
            {
                ledgerEntries[codeStart].AddEntity(searchCode, entity);
            }
            else
            {
                ledgerEntries.Add(codeStart, new EntityLedgerEntry(searchCode, entity));
            }

            lastEntItemIDAdded = entity.EntityId;
        }

        public List<Entity> GetAllEntitiesMatchingCode( AssetLocation assetLocation, EntityLedgerEntrySearchData searchData )
        {
            string codeStart = assetLocation.FirstCodePart();

            if ( ledgerEntries.ContainsKey( codeStart ) )
            {
                return ledgerEntries[codeStart].GetAllEntitiesMatchingCode(assetLocation, searchData);
            }

            return EntityManager.emptyList;
        }
    }

    public struct EntityLedgerEntry
    {
        private AssetLocation assetLocation;
        private ManagedEntityArray meaEntityLedgerEntry;
        private Dictionary<string, EntityLedgerEntry> subLedgers = new Dictionary<string, EntityLedgerEntry>();
        private EntityLedgerEntrySearchData searchData = new EntityLedgerEntrySearchData();
        public EntityLedgerEntry( AssetLocation assetLocation, Entity entity, int pathLimit = -1 ) 
        {
            string[] codeVariants = GetAllCodeVariantsForAssetLocation(assetLocation, pathLimit);
            int pathDepth = codeVariants.Length - 1;

            //Create Our Ledger Entry
            this.assetLocation = new AssetLocation(codeVariants[0]);
            meaEntityLedgerEntry = new ManagedEntityArray();
            meaEntityLedgerEntry.AddEntity(entity);

            if ( codeVariants.Length > 1 ) 
            {
                subLedgers.Add(codeVariants[1], new EntityLedgerEntry(new AssetLocation(codeVariants[codeVariants.Length - 1]), entity, pathDepth - 1));
            }
        }

        public void AddEntity( AssetLocation assetLocation, Entity entity, int pathLimit = -1 )
        {
            string[] codeVariants = GetAllCodeVariantsForAssetLocation( assetLocation, pathLimit );
            int pathDepth = codeVariants.Length - 1;

            //Create Our Ledger Entry
            meaEntityLedgerEntry.AddEntity(entity);

            if (codeVariants.Length > 1)
            {
                if ( subLedgers.ContainsKey(codeVariants[1] ) )
                    subLedgers[codeVariants[1]].AddEntity(new AssetLocation(codeVariants[codeVariants.Length - 1]), entity, pathDepth - 1);
                else
                    subLedgers.Add(codeVariants[1], new EntityLedgerEntry(new AssetLocation(codeVariants[codeVariants.Length - 1]), entity, pathDepth - 1));
            }
        }

        public List<Entity> GetAllEntitiesMatchingCode(AssetLocation assetLocation, EntityLedgerEntrySearchData searchData)
        {
            string shortString = assetLocation.ToShortString();
            int pathDepth = shortString.Count(x => x == '-');
            string[] codeVariants = GetAllCodeVariantsForAssetLocation(assetLocation, -1);

            //Configure our static search data before beginning our search.
            searchData.assetLocation = assetLocation;
            searchData.codeVariants = codeVariants;
            searchData.pathDepthMax = pathDepth + 1;
            searchData.pathDepthCurrent = 0;
            return CrawlLedgerEntryTree( searchData );
        }

        public List<Entity> CrawlLedgerEntryTree(EntityLedgerEntrySearchData searchData)
        {
            if ( searchData.assetLocation == assetLocation )
            {
                return meaEntityLedgerEntry.GetManagedList();
            }
            else
            {
                searchData.pathDepthCurrent++;

                if (searchData.pathDepthMax == searchData.pathDepthCurrent)
                    return EntityManager.emptyList;

                if (subLedgers.ContainsKey(searchData.codeVariants[searchData.pathDepthCurrent]))
                    return subLedgers[searchData.codeVariants[searchData.pathDepthCurrent]].CrawlLedgerEntryTree(searchData);
                else
                    return EntityManager.emptyList;
            }
        }

        private string[] GetAllCodeVariantsForAssetLocation( AssetLocation assetLocation, int pathLimit = -1 ) 
        {
            string shortString = assetLocation.ToShortString();

            int pathDepth = shortString.Count(x => x == '-');

            if (pathLimit > -1)
                pathDepth = Math.Min(pathDepth, pathLimit);

            AssetLocation currentPath = assetLocation.Clone();
            currentPath.Domain = ""; //Clear Domain;

            //Generate the path name at every potential wildcard.
            string[] codeVariants = new string[pathDepth + 1];
            for (int i = pathDepth; i >= 0; i--)
            {
                codeVariants[i] = currentPath.Path;

                if (i < pathDepth)
                    codeVariants[i] += "-*";

                string endVariant = currentPath.EndVariant();
                if (endVariant != "")
                    currentPath = currentPath.WithoutPathAppendix("-" + endVariant);
            }

            return codeVariants;
        }
    }
    
    public struct EntityLedgerEntrySearchData
    {
        public AssetLocation assetLocation;
        public string[] codeVariants;
        public int pathDepthMax;
        public int pathDepthCurrent;
    }
 
 /*   
  _____       _   _ _           __  __                                   
 | ____|_ __ | |_(_) |_ _   _  |  \/  | __ _ _ __   __ _  __ _  ___ _ __ 
 |  _| | '_ \| __| | __| | | | | |\/| |/ _` | '_ \ / _` |/ _` |/ _ \ '__|
 | |___| | | | |_| | |_| |_| | | |  | | (_| | | | | (_| | (_| |  __/ |   
 |_____|_| |_|\__|_|\__|\__, | |_|  |_|\__,_|_| |_|\__,_|\__, |\___|_|   
                        |___/                            |___/            
 */

    //////////////////
    //ENTITY MANAGER//
    //////////////////
    public static class EntityManager
    {
        private static EntityLedger entityLedger = new EntityLedger();
        private static EntityLedger itemLedger = new EntityLedger();
        private static EntityLedgerEntrySearchData searchData = new EntityLedgerEntrySearchData();
        public  static List<Entity> emptyList = new List<Entity>();


        //Look at optimizing this down to projectiles that are in flight, by removing stuck projectiles.
        private static ManagedEntityArray _meaEntityProjectiles = new ManagedEntityArray();
        private static ManagedEntityArray _meaEntityProjectilesInFlight = new ManagedEntityArray();
        private static ManagedEntityArray _meaEntityDead = new ManagedEntityArray();

        private static long lastProjectileEntIDAdded = -1;
        private static long lastDeadEntIDAdded = -1;
        
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

        public static void RegisterEntityWithEntityLedger( Entity entity )
        {
            if ( entity.Code.ToString() == "game:item" )
            {
                itemLedger.AddEntityItemToLedger( entity );
            }
            else
            {
                entityLedger.AddEntityToLedger(entity);
            }
            
        }

        public static List<Entity> GetAllEntitiesMatchingCode( AssetLocation assetLocation )
        {
            return entityLedger.GetAllEntitiesMatchingCode(assetLocation, searchData);
        }

        public static List<Entity> GetAllEntityItemsMatchingCode( AssetLocation assetLocation ) 
        {
            return itemLedger.GetAllEntitiesMatchingCode(assetLocation, searchData);
        }

        public static Entity GetNearestEntityItemMatchingCodes(Vec3d position, double radius, AssetLocation[] codes, ActionConsumable<Entity> matches = null)
        {
            Entity nearestEntityItem = null;
            double radiusSqr = radius * radius;
            double nearestDistanceSqr = radiusSqr;
            
            List<Entity>[] entityItemListsMatchingCodes = new List<Entity>[codes.Length];
            for ( int i = 0; i < codes.Length; i++ ) 
            {
                entityItemListsMatchingCodes[i] = GetAllEntityItemsMatchingCode(codes[i]);
            }

            foreach( List<Entity> entityList in entityItemListsMatchingCodes ) 
            { 
                foreach( Entity entity in entityList ) 
                { 
                    double distanceSqr = entity.SidedPos.SquareDistanceTo(position);
                    if ( distanceSqr < nearestDistanceSqr && matches(entity) ) 
                    {
                        nearestDistanceSqr = distanceSqr;
                        nearestEntityItem = entity;
                    }
                }
            }

            return nearestEntityItem;
        }

        public static void OnEntityProjectileSpawn(Entity entity)
        {
            if ( entity is EntityProjectile )
            {
                RegisterEntityProjectile(entity);
            }
                
        }

        public static void RegisterEntityProjectile( Entity entity )
        {
            Debug.Assert(entity is EntityProjectile);
            Debug.Assert(entity.EntityId != lastProjectileEntIDAdded);

            _meaEntityProjectiles.AddEntity(entity);
            _meaEntityProjectilesInFlight.AddEntity(entity);

            lastProjectileEntIDAdded = entity.EntityId;
        }

        public static void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            RegisterDeadEntity(entity);
        }

        public static void RegisterDeadEntity( Entity entity)
        {
            Debug.Assert(!entity.Alive);

            if (entity.ShouldDespawn)
                return;

            Debug.Assert(entity.EntityId != lastDeadEntIDAdded);

            _meaEntityDead.AddEntity(entity);
            lastDeadEntIDAdded = entity.EntityId;
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
