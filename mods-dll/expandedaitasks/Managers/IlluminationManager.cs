using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedAiTasks.Managers
{
    public struct EntIlluminationData
    {
        public EntIlluminationData( int lightLevel, double lastComputationTime )
        {
            this.lightLevel = lightLevel;
            this.lastComputationTime = lastComputationTime;
        }

        public int lightLevel;
        public double lastComputationTime;
    }
    public static class IlluminationManager
    {
        private const float LIGHT_LEVEL_RECOMPUTE_TIME_MS = 500f;
        private const float MAX_DYNAMIC_LIGHT_SEARCH_DIST = 12.0f;

        private static Dictionary<long, EntIlluminationData> illuminationData = new Dictionary<long, EntIlluminationData>();

        private static EntityPartitioning partitionUtil;

        //We need to clear the dictionary at regular intervals so we don't build up null entries over time.

        public static void Init( ICoreServerAPI sapi )
        {
            partitionUtil = sapi.ModLoader.GetModSystem<EntityPartitioning>();
        }

        public static void ShutdownCleanup()
        {
            illuminationData.Clear();
        }

        public static void OnDespawn( Entity ent, EntityDespawnData despawnData )
        {
            if (illuminationData.ContainsKey( ent.EntityId ) )
                illuminationData.Remove( ent.EntityId);
        }

        public static int GetIlluminationLevelForEntity( Entity ent )
        {
            bool isNewKey = true;
            if (illuminationData.ContainsKey(ent.EntityId))
            {
                isNewKey = false;
                if ( ent.World.ElapsedMilliseconds <= illuminationData[ent.EntityId].lastComputationTime + LIGHT_LEVEL_RECOMPUTE_TIME_MS )
                    return illuminationData[ent.EntityId].lightLevel;
            }

            //Compute the light level, store it, and return it. 
            //See if target is hidden in darkness.
            BlockPos targetBlockPosition = new BlockPos((int)ent.ServerPos.X, (int)ent.ServerPos.Y, (int)ent.ServerPos.Z);
            int lightLevel = ent.World.BlockAccessor.GetLightLevel(targetBlockPosition, EnumLightLevelType.MaxTimeOfDayLight);

            /////////////////////////
            ///DYNAMIC LIGHT CHECK///
            /////////////////////////

            //////////////////////////////////////////////////////
            ///CHECK BLOCK ITEMS IN PLAYERS HANDS FOR HSV GLOWS///
            //////////////////////////////////////////////////////

            //If the player is holding a glowing object, see if it is brighter than the ambient environment.
            if (ent is EntityPlayer)
            {
                EntityPlayer entPlayer = ent as EntityPlayer;

                ItemSlot rightSlot = entPlayer.RightHandItemSlot;
                if (rightSlot.Itemstack != null)
                {
                    if (rightSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = rightSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > lightLevel)
                            lightLevel = lightHsv[2];

                    }
                }

                ItemSlot leftSlot = entPlayer.LeftHandItemSlot;
                if (leftSlot.Itemstack != null)
                {
                    if (leftSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = leftSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > lightLevel)
                            lightLevel = lightHsv[2];
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////////
            ///CHECK WORLD FOR DYNAMIC LIGHTS (EXPENSIVE)(NEEDS TO BE OPTIMIZED)///
            ///////////////////////////////////////////////////////////////////////

            if (IsLitByDynamicLight(ent, lightLevel))
                lightLevel = brightestDynamicLightLevel;

            if (isNewKey)
            {
                EntIlluminationData illumData = new EntIlluminationData( lightLevel, ent.World.ElapsedMilliseconds );
                illuminationData.Add( ent.EntityId, illumData );
            }
            else
            {
                illuminationData.Remove(ent.EntityId);
                EntIlluminationData illumData = new EntIlluminationData(lightLevel, ent.World.ElapsedMilliseconds);
                illuminationData.Add(ent.EntityId, illumData);
            }
                
            return lightLevel;
        }

        private static int brightestDynamicLightLevel = 0;

        private static bool IsLitByDynamicLight(Entity ent, int ambientLightLevel)
        {
            brightestDynamicLightLevel = ambientLightLevel;
            ent.World.GetEntitiesAround(ent.ServerPos.XYZ, MAX_DYNAMIC_LIGHT_SEARCH_DIST, MAX_DYNAMIC_LIGHT_SEARCH_DIST, GetBrightestDynamicLightLevel);
            return brightestDynamicLightLevel > ambientLightLevel;
        }

        private static bool GetBrightestDynamicLightLevel(Entity ent)
        {
            if (ent is EntityItem)
            {
                EntityItem itemEnt = (EntityItem)ent;

                if (itemEnt.Itemstack.Block != null)
                {
                    if (itemEnt.Itemstack.Block.LightHsv[2] > brightestDynamicLightLevel)
                    {
                        brightestDynamicLightLevel = itemEnt.Itemstack.Block.LightHsv[2];
                    }
                }

                return true;
            }

            //If the player is holding a glowing object, see if it is brighter than the ambient environment.
            if (ent is EntityPlayer)
            {
                EntityPlayer targetPlayer = ent as EntityPlayer;

                ItemSlot rightSlot = targetPlayer.RightHandItemSlot;
                if (rightSlot.Itemstack != null)
                {
                    if (rightSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = rightSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > brightestDynamicLightLevel)
                            brightestDynamicLightLevel = lightHsv[2];
                    }
                }

                ItemSlot leftSlot = targetPlayer.LeftHandItemSlot;
                if (leftSlot.Itemstack != null)
                {
                    if (leftSlot.Itemstack.Block != null)
                    {
                        byte[] lightHsv = leftSlot.Itemstack.Block.LightHsv;

                        if (lightHsv[2] > brightestDynamicLightLevel)
                            brightestDynamicLightLevel = lightHsv[2];
                    }
                }
            }

            return true;
        }
    }
}