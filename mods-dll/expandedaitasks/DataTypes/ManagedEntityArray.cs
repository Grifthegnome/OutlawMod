using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ExpandedAiTasks.DataTypes
{
    public struct ManagedEntityArray
    {
        private List<Entity>  _managedEntityArray;

        public ManagedEntityArray()
        {
            _managedEntityArray = new List<Entity>();
        }

        public List<Entity> GetManagedList()
        {
            List<Entity> compactedList = new List<Entity>();
            foreach (Entity entity in _managedEntityArray)
            {
                if (entity != null && !entity.ShouldDespawn)
                    compactedList.Add(entity);
            }

            _managedEntityArray = compactedList;

            return _managedEntityArray;
        }

        public void AddEntity( Entity entity )
        {
            _managedEntityArray.Add( entity );
        }

        public void RemoveEntity( Entity entity ) 
        {
            Debug.Assert( _managedEntityArray.Contains( entity ) );
            _managedEntityArray.Remove( entity );
        }

        public void Clear() 
        {
            _managedEntityArray.Clear();
        }

        public bool Contains( Entity entity ) 
        {
            return _managedEntityArray.Contains( entity );
        }

        public int Count()
        {
            List<Entity> compactedList = new List<Entity>();
            foreach (Entity entity in _managedEntityArray)
            {
                if (entity != null && !entity.ShouldDespawn)
                    compactedList.Add(entity);
            }

            _managedEntityArray = compactedList;

            return _managedEntityArray.Count();
        }

        public void Extend( List<Entity> list )
        {
            _managedEntityArray.AddRange(list);
        }

        public void FilterByCheckResult( ActionBoolReturn<Entity> check )
        {
            List<Entity> compactedList = new List<Entity>();
            foreach (Entity entity in _managedEntityArray)
            {
                if (entity != null && !entity.ShouldDespawn)
                {
                    if ( check(entity) )
                    {
                        compactedList.Add(entity);
                    }   
                }
            }

            _managedEntityArray = compactedList;
        }
    }
}
