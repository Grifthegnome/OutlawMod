using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ExpandedAiTasks
{
    public struct AwarenessData
    {
        private bool _isAware;
        private double _lastComputationTime;

        public AwarenessData(bool isAware, double lastComputationTime)
        {
            _isAware = isAware;
            _lastComputationTime = lastComputationTime;
        }

        public bool isAware
        {
            get 
            { 
                return _isAware; 
            }
            set
            {
                _isAware = value;
            }
        }

        public double lastComputationTime
        {
            get
            {
                return _lastComputationTime;
            }

            set 
            { 
                _lastComputationTime = value;
            }
        }

    }
    public static class AwarenessManager
    {
        private static Dictionary<long, Dictionary<long, AwarenessData>> awarenessData = new Dictionary<long, Dictionary<long, AwarenessData>>();
    
        public static bool EntityHasAwarenessEntry( Entity ent )
        {
            return awarenessData.ContainsKey(ent.EntityId);
        }

        public static bool EntityHasAwarenessEntryForTargetEntity(Entity ent, Entity targetEnt)
        {
            return awarenessData[ent.EntityId].ContainsKey(targetEnt.EntityId);
        }

        public static bool EntityAwarenessEntryForTargetEntityIsStale( Entity ent, Entity targetEnt)
        {
            return ent.World.ElapsedMilliseconds > awarenessData[ent.EntityId][targetEnt.EntityId].lastComputationTime;
        }

        public static bool EntityIsAwareOfTargetEntity( Entity ent, Entity targetEnt ) 
        {
            return awarenessData[ent.EntityId][targetEnt.EntityId].isAware;
        }

        public static void UpdateOrCreateEntityAwarenessEntryForTargetEntity( Entity ent, Entity targetEnt, bool isAware )
        {
            if ( !EntityHasAwarenessEntry( ent ) )
            {
                awarenessData.Add( ent.EntityId, new Dictionary<long, AwarenessData>() );
                awarenessData[ent.EntityId].Add(targetEnt.EntityId, new AwarenessData(isAware, ent.World.ElapsedMilliseconds));
            }
            else if ( !EntityHasAwarenessEntryForTargetEntity(ent, targetEnt) )
            {
                awarenessData[ent.EntityId].Add( targetEnt.EntityId, new AwarenessData( isAware, ent.World.ElapsedMilliseconds ) );
            }
            else
            {
                AwarenessData targetData = awarenessData[ent.EntityId][targetEnt.EntityId];

                targetData.isAware = isAware;
                targetData.lastComputationTime = ent.World.ElapsedMilliseconds;

                awarenessData[ent.EntityId][targetEnt.EntityId] = targetData;
            }
        }

        public static bool GetEntityAwarenessForTargetEntity(Entity ent, Entity targetEnt ) 
        {
            return awarenessData[ent.EntityId][targetEnt.EntityId].isAware;
        }

        public static void OnDespawn(Entity entity, EntityDespawnData despawnData)
        {
            CleanUpEntries(entity);
        }

        public static void OnDeath(Entity entity, DamageSource damageSource)
        {
            CleanUpEntries(entity);
        }

        private static void CleanUpEntries( Entity entToCleanup )
        { 
            //remove all target entries.
            foreach( long entID in awarenessData.Keys )
            {
                if (awarenessData[ entID ].ContainsKey(entToCleanup.EntityId) )
                {
                    awarenessData[ entID ].Remove(entToCleanup.EntityId);
                }
            }

            if (awarenessData.ContainsKey(entToCleanup.EntityId))
                awarenessData.Remove(entToCleanup.EntityId);
        }
    }
}
