using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TrailMod
{
    public struct TrailBlockPosEntry
    {
        private long _lastTouchTime = -1;
        private long _lastTouchEntID = -1;
        private EntityPos _lastTouchEntityPos = null;

        const double TRAIL_COOLDOWN_MS = 900000;

        public long lastTouchTime
        {
            get { return _lastTouchTime; }
            set { _lastTouchTime = value; }
        }

        public long lastTouchEntID
        {
            get { return _lastTouchEntID; }
            set { _lastTouchEntID = value; }
        }

        public EntityPos lastTouchEntityPos
        {
            get{ return _lastTouchEntityPos; } 
            set { _lastTouchEntityPos = value;}
        }

        public TrailBlockPosEntry( long newTouchEntID, EntityPos newTouchEntityPos, long newTouchTime )
        {
            lastTouchEntID= newTouchEntID;
            lastTouchTime = newTouchTime;
            lastTouchEntityPos= newTouchEntityPos;
        }

        public bool BlockTouched(long newTouchEntID, EntityPos newTouchEntityPos, long newTouchTime)
        {
            //Before setting lastTouchTime, determine if we need to decrement touchCount based on delta between current and previous touch time.
            long touchTimeDelta = newTouchTime - lastTouchTime;

            lastTouchEntID = newTouchEntID;
            lastTouchTime = newTouchTime;
            lastTouchEntityPos = newTouchEntityPos;

            if (touchTimeDelta > 500)
            {
                return true;
            }

            return false;
        
        }
    }

    public struct TrailTouchCallbackData
    {
        public readonly int blockIDTransformTo;
        TrailTouchCallbackData( int blockIDTransformTo ) 
        { 
            this.blockIDTransformTo = blockIDTransformTo;
        }
    }

    public struct TrailBlockTouchTransformData
    {
        public readonly AssetLocation code; //Readable Block Code; 
        public readonly double transformChance = -1;
        public readonly int transformBlockID = -1;
        public readonly bool transformByPlayerOnly = false;

        public TrailBlockTouchTransformData(AssetLocation code, double transformChance, int transformBlockID, bool transformByPlayerOnly )
        {
            this.code = code;
            this.transformChance = transformChance;
            this.transformBlockID = transformBlockID;
            this.transformByPlayerOnly = transformByPlayerOnly;
        }
    }

    public class TrailChunkManager
    {
        public const long TRAIL_POS_MONITOR_TIMEOUT = 15000;

        const string SOIL_CODE = "soil";
        const string SOIL_LOW_NONE_CODE = "soil-low-none";
        const string FOREST_FLOOR_CODE = "forestfloor";
        const string COB_CODE = "cob";
        const string PACKED_DIRT_CODE = "packeddirt";
        const string PACKED_DIRT_ARID_CODE = "drypackeddirt";
        const string STONE_PATH_CODE = "stonepath-free";

        const int FOREST_FLOOR_VARIATION_COUNT = 8;

        private static readonly string[] FERTILITY_VARIANTS = { "high", "compost", "medium", "low", "verylow" };
        private static readonly string[] GRASS_VARIANTS = { "normal", "sparse", "verysparse", "none" };

        private static IWorldAccessor worldAccessor;
        private static ICoreServerAPI serverApi;

        //Callbacks based on number of block touches, stored by block ID;
        private static Dictionary<int, TrailBlockTouchTransformData> trailBlockTouchTransforms = new Dictionary<int, TrailBlockTouchTransformData>();

        //Current World Trail Data Stored in Memory.
        private Dictionary<IWorldChunk, Dictionary<long, TrailBlockPosEntry>> trailChunkEntries = new Dictionary<IWorldChunk, Dictionary<long, TrailBlockPosEntry>>();

        public static TrailChunkManager trailChunkManagerSingleton;
        private TrailChunkManager()
        {

        }

        public static TrailChunkManager GetTrailChunkManager()
        {
            if ( trailChunkManagerSingleton == null)
                trailChunkManagerSingleton = new TrailChunkManager();
            
            return trailChunkManagerSingleton;
        }

        /*
          _____          _ _   ____        _        
         |_   _| __ __ _(_) | |  _ \  __ _| |_ __ _ 
           | || '__/ _` | | | | | | |/ _` | __/ _` |
           | || | | (_| | | | | |_| | (_| | || (_| |
           |_||_|  \__,_|_|_| |____/ \__,_|\__\__,_|

        */
        public void InitData( IWorldAccessor world, ICoreServerAPI sapi )
        {
            worldAccessor = world;
            serverApi = sapi;
            BuildAllTrailBlockData( world );
        }

        public void ShutdownCleanup()
        {
            trailChunkEntries.Clear();
            trailBlockTouchTransforms.Clear();
            trailChunkManagerSingleton = null;
        }


        private static List<long> timedOutBlocks = new List<long>();
        private static List<IWorldChunk> chunksToRemove = new List<IWorldChunk>();
        public void Clean( float dt )
        {

            chunksToRemove.Clear();
            foreach ( IWorldChunk chunk in trailChunkEntries.Keys )
            {

                timedOutBlocks.Clear();
                foreach ( long blockTrailID in trailChunkEntries[chunk].Keys )
                {
                    TrailBlockPosEntry blockPosEntry = trailChunkEntries[chunk].GetValueSafe( blockTrailID );

                    if( (worldAccessor.ElapsedMilliseconds - blockPosEntry.lastTouchTime) > TRAIL_POS_MONITOR_TIMEOUT)
                    {
                        timedOutBlocks.Add(blockTrailID);
                    }
                }

                //THIS FUNCTION IS NOT REMOVING TIMED OUT BLOCKS EVEN THOUGH IT IS TRYING.
                //I THINK THE NESTED DICTIONARY MAY BE CAUSING PROBLEMS FOR US. WE NEED TO SEE HOW WE CAN REFACTOR!

                Dictionary<long, TrailBlockPosEntry> trailEntries = trailChunkEntries.GetValueSafe(chunk);

                foreach ( long blockToRemove in timedOutBlocks )
                {
                    trailEntries.Remove( blockToRemove );
                }

                trailChunkEntries[chunk] = trailEntries;

                if (trailChunkEntries[chunk].Values.Count() == 0)
                    chunksToRemove.Add(chunk);
            }

            foreach( IWorldChunk chunk in chunksToRemove)
            {
                trailChunkEntries.Remove( chunk );
            }

            serverApi.Event.RegisterCallback(Clean, ((int)TRAIL_POS_MONITOR_TIMEOUT));
        }

        public void OnChunkUnloaded(Vec3i chunkCoord)
        {
            IWorldChunk chunk = worldAccessor.BlockAccessor.GetChunk(chunkCoord.X, chunkCoord.Y, chunkCoord.Z);

            if (trailChunkEntries.ContainsKey(chunk))
            {
                trailChunkEntries[chunk].Clear();
                trailChunkEntries.Remove(chunk);
            }

        }

        private string[] BuildSoilBlockVariants()
        {
            int variantIndex = 0;
            string[] soilVariants = new string[FERTILITY_VARIANTS.Count() * GRASS_VARIANTS.Count()];
            for ( int fertilityIndex = 0; fertilityIndex < FERTILITY_VARIANTS.Length; fertilityIndex++ ) 
            {
                for (int grassIndex = 0; grassIndex < GRASS_VARIANTS.Length; grassIndex++ )
                {
                    soilVariants[variantIndex] = SOIL_CODE + "-" + FERTILITY_VARIANTS[fertilityIndex] + "-" + GRASS_VARIANTS[grassIndex];
                    variantIndex++;
                }
            }

            return soilVariants;
        }

        private string[] BuildSoilBlockVariantsFertilityOnly()
        {
            string[] soilVariants = new string[FERTILITY_VARIANTS.Count()];
            for (int fertilityIndex = 0; fertilityIndex < FERTILITY_VARIANTS.Length; fertilityIndex++)
            {
                soilVariants[fertilityIndex] = SOIL_CODE + "-" + FERTILITY_VARIANTS[fertilityIndex];
            }

            return soilVariants;
        }

        private string[] BuildCobBlockVariants()
        {
            string[] cobVariants = new string[GRASS_VARIANTS.Count()];
            for (int grassIndex = 0; grassIndex < GRASS_VARIANTS.Length; grassIndex++ )
            {
                cobVariants[grassIndex] = COB_CODE + "-" + GRASS_VARIANTS[grassIndex];
            }

            return cobVariants;
        }

        private string[] BuildForestFloorVariants()
        {
            
            string[] forestFloorVariants = new string[FOREST_FLOOR_VARIATION_COUNT];
            for ( int i = 0; i < FOREST_FLOOR_VARIATION_COUNT; i++ )
            {
                forestFloorVariants[i] = FOREST_FLOOR_CODE + "-" + i;
            }

            return forestFloorVariants;
        }

        private void BuildAllTrailBlockData( IWorldAccessor world )
        {
            string[] soilBlockCodes = BuildSoilBlockVariants();
            string[] cobBlockCodes = BuildCobBlockVariants();
            string[] forestFloorCodes = BuildForestFloorVariants();

            ValidateTrailBlocks(world, soilBlockCodes);
            ValidateTrailBlocks(world, cobBlockCodes);
            ValidateTrailBlocks(world, forestFloorCodes);
            ValidateTrailBlocks(world, new string[] { PACKED_DIRT_CODE, PACKED_DIRT_ARID_CODE, STONE_PATH_CODE });

            //////////////////////////////////////////////////////////////////////////////////////////
            //SOIL                                                                                  //
            //We want soil to stay in it's fertility category, but to slowy strip the grass layer.  //
            //Once it is fully stripped it becomes packed dirt.                                     //
            //////////////////////////////////////////////////////////////////////////////////////////
            string[] soilFertilityBlockVariants = BuildSoilBlockVariantsFertilityOnly();

            double[] soilTransformChanceByVariant = new double[GRASS_VARIANTS.Length];
            bool[] soilTransformByPlayerOnlyByVariant = new bool[GRASS_VARIANTS.Length];
            for( int i = 0; i < soilTransformChanceByVariant.Length; i++ ) 
            {
                if ( i == 0 )
                { 
                    soilTransformChanceByVariant[i] = 1.0; //100% Chance to start.
                    soilTransformByPlayerOnlyByVariant[i] = false;
                }
                else if ( i == soilTransformChanceByVariant.Length - 1)
                {
                    soilTransformChanceByVariant[i] = 0.05; //05% Chance to become packed dirt.
                    soilTransformByPlayerOnlyByVariant[i] = true; //only players can make packed dirt trails.
                }
                else
                {
                    soilTransformChanceByVariant[i] = 0.2; //20% Chance to lose grass.
                    soilTransformByPlayerOnlyByVariant[i] = false;
                }   
            }

            for ( int soilFertilityVariantIndex = 0; soilFertilityVariantIndex < soilFertilityBlockVariants.Length; soilFertilityVariantIndex++ ) 
            {
                BuildTrailTouchBlockVariantProgression(world, soilFertilityBlockVariants[soilFertilityVariantIndex], GRASS_VARIANTS, soilTransformChanceByVariant, soilTransformByPlayerOnlyByVariant, PACKED_DIRT_CODE);
            }

            ////////////////////////////////////////////////////////////////////
            //COB                                                             //
            //We want cob to strip its grass layer, but to never change type. //
            ////////////////////////////////////////////////////////////////////
            double[] cobTransformChanceByVariants = new double[GRASS_VARIANTS.Length];
            bool[] cobTransformByPlayerOnlyByVariants = new bool[GRASS_VARIANTS.Length];
            for( int i = 0; i < cobTransformChanceByVariants.Length; i++)
            {
                cobTransformChanceByVariants[i] = 0.2; //20% Chance to lose grass.
                cobTransformByPlayerOnlyByVariants[i] = false;
            }

            BuildTrailTouchBlockVariantProgression(world, COB_CODE, GRASS_VARIANTS, cobTransformChanceByVariants, cobTransformByPlayerOnlyByVariants, "");

            /////////////////////////////////////////////////////////////////////
            //FOREST FLOOR                                                     //
            //We want forest floor to strip, then become low fertility dirt.   //
            /////////////////////////////////////////////////////////////////////
            string[] forestFloorVariants = new string[FOREST_FLOOR_VARIATION_COUNT];
            for ( int i = 0;i < forestFloorVariants.Length; i++)
            {
                forestFloorVariants[ (forestFloorVariants.Length - 1) - i] = i.ToString();
            }

            double[] forestFloorChanceByVariants = new double[FOREST_FLOOR_VARIATION_COUNT];
            bool[] forestFloorTransfromByPlayerOnlyByVariants = new bool[FOREST_FLOOR_VARIATION_COUNT];
            for ( int i = 0; i < forestFloorChanceByVariants.Length; i++)
            {
                if ( i == 0 )
                {
                    forestFloorChanceByVariants[i] = 1.0; //100% Chance to start.
                    forestFloorTransfromByPlayerOnlyByVariants[i] = false;
                }
                else if ( i == forestFloorChanceByVariants.Length - 1)
                {
                    forestFloorChanceByVariants[i] = 0.2; //20% Chance to become soil-low-normal.
                    forestFloorTransfromByPlayerOnlyByVariants[i] = false;
                }
                else
                {
                    forestFloorChanceByVariants[i] = 0.2; //20% Chance to lose grass.
                    forestFloorTransfromByPlayerOnlyByVariants[i] = false;
                } 
            }

            BuildTrailTouchBlockVariantProgression(world, FOREST_FLOOR_CODE, forestFloorVariants, forestFloorChanceByVariants, forestFloorTransfromByPlayerOnlyByVariants, SOIL_LOW_NONE_CODE);

            ////////////////////////////////////////////////////////////////
            //PACKED DIRT                                                 //
            //We want packed dirt to upgrade to a path after extended use.//
            ////////////////////////////////////////////////////////////////

            AssetLocation packedDirtAsset = new AssetLocation(PACKED_DIRT_CODE);
            AssetLocation stonePathAsset = new AssetLocation(STONE_PATH_CODE);

            Block packedDirt = world.GetBlock(packedDirtAsset);
            Block stonePath = world.GetBlock(stonePathAsset);

            //A 1% chance to turn packed dirt into a stonepath. Only players can make packed dirt trails.
            CreateTrailBlockTransform(packedDirtAsset, packedDirt.Id, 0.01, stonePath.Id, true);

            //STONE PATH
            //This is as far as we go.

        }

        private void ValidateTrailBlocks(IWorldAccessor world, string[] blockCodes )
        {
            foreach( string code in blockCodes ) 
            { 
                AssetLocation blockAsset = new AssetLocation( code );

                Block block = world.GetBlock(blockAsset);

                Debug.Assert( block != null );

                int blockID = block.BlockId;
            }
        }

        private void CreateTrailBlockTransform( AssetLocation blockAsset, int blockID, double transformChance, int transformBlockID, bool transformByPlayerOnly)
        {
            Debug.Assert( !trailBlockTouchTransforms.ContainsKey(blockID), "Block " + trailBlockTouchTransforms + " is already registered with the trail block transform system.");

            TrailBlockTouchTransformData touchTransformData = new TrailBlockTouchTransformData(blockAsset, transformChance, transformBlockID, transformByPlayerOnly);
            trailBlockTouchTransforms.Add(blockID, touchTransformData);
        }

        private void BuildTrailTouchBlockVariantProgression( IWorldAccessor world, string baseCode, string[] variantCodeProgression, double[] transformChanceByVariant, bool[] transformByPlayerOnlyByVariant, string variantExitTransformationBlockCode )
        {
            Debug.Assert(variantCodeProgression.Length > 0);
            Debug.Assert( variantCodeProgression.Length == transformChanceByVariant.Length );

            for ( int variantIndex = 0;  variantIndex < variantCodeProgression.Length; variantIndex++)
            {
                string blockCode = baseCode + "-" + variantCodeProgression[variantIndex];
                string transformBlockCode = "";

                if ( variantIndex == variantCodeProgression.Length - 1 ) 
                    transformBlockCode = variantExitTransformationBlockCode;
                else
                    transformBlockCode = baseCode + "-" + variantCodeProgression[variantIndex + 1];

                if (transformBlockCode == "")
                    continue;

                AssetLocation blockAsset = new AssetLocation(blockCode);
                AssetLocation transformBlockAsset = new AssetLocation( transformBlockCode );

                Block block = world.GetBlock(blockAsset);
                Block transformBlock = world.GetBlock(transformBlockAsset);

                int blockID = block.BlockId;
                int transformBlockID = transformBlock.BlockId;

                CreateTrailBlockTransform(blockAsset, blockID, transformChanceByVariant[variantIndex], transformBlockID, transformByPlayerOnlyByVariant[variantIndex]);
            }
        }

        /*
          _____          _ _   __  __                                                   _   
         |_   _| __ __ _(_) | |  \/  | __ _ _ __   __ _  __ _  ___ _ __ ___   ___ _ __ | |_ 
           | || '__/ _` | | | | |\/| |/ _` | '_ \ / _` |/ _` |/ _ \ '_ ` _ \ / _ \ '_ \| __|
           | || | | (_| | | | | |  | | (_| | | | | (_| | (_| |  __/ | | | | |  __/ | | | |_ 
           |_||_|  \__,_|_|_| |_|  |_|\__,_|_| |_|\__,_|\__, |\___|_| |_| |_|\___|_| |_|\__|
                                                        |___/                               
        */

        public void AddOrUpdateBlockPosTrailData( IWorldAccessor world, Block block, BlockPos blockPos, Entity touchEnt )
        {
            IWorldChunk chunk = touchEnt.World.BlockAccessor.GetChunkAtBlockPos( blockPos );
            Debug.Assert( chunk != null );

            //If this block position doesn't contain a block we should monitor, remove it from tracking.
            if ( !ShouldTrackBlockTrailData(block) )
                RemoveBlockPosTrailData(world, blockPos);

            long blockTrailID = ConvertBlockPositionToTrailPosID(blockPos);

            if ( trailChunkEntries.ContainsKey( chunk ) ) 
            {
                if ( trailChunkEntries[ chunk ].ContainsKey(blockTrailID) )
                {
                    TrailBlockPosEntry entryToUpdate = trailChunkEntries[chunk].GetValueSafe(blockTrailID);
                    bool shouldTryTransform = entryToUpdate.BlockTouched( touchEnt.EntityId, touchEnt.ServerPos, touchEnt.World.ElapsedMilliseconds );
                    trailChunkEntries[chunk][blockTrailID] = entryToUpdate;

                    if (shouldTryTransform)
                    {
                        TryToTransformTrailBlock(world, blockPos, block.BlockId, touchEnt);
                    }
                }
                else
                {
                    TrailBlockPosEntry trailBlockEntry = new TrailBlockPosEntry(touchEnt.EntityId, touchEnt.ServerPos, touchEnt.World.ElapsedMilliseconds);
                    trailChunkEntries[chunk].Add(blockTrailID, trailBlockEntry);

                    TryToTransformTrailBlock(world, blockPos, block.BlockId, touchEnt);
                }
            }
            else
            {
                TrailBlockPosEntry trailBlockEntry = new TrailBlockPosEntry(touchEnt.EntityId, touchEnt.ServerPos, touchEnt.World.ElapsedMilliseconds);
                Dictionary<long, TrailBlockPosEntry> trailChunkEntry = new Dictionary<long, TrailBlockPosEntry>();
                trailChunkEntry.Add(blockTrailID, trailBlockEntry);
                trailChunkEntries.Add(chunk, trailChunkEntry);

                TryToTransformTrailBlock(world, blockPos, block.BlockId, touchEnt);
            }
        }

        public void RemoveBlockPosTrailData( IWorldAccessor world, BlockPos blockPos )
        {
            IWorldChunk chunk = world.BlockAccessor.GetChunkAtBlockPos(blockPos);
            Debug.Assert(chunk != null);

            long blockTrailID = ConvertBlockPositionToTrailPosID(blockPos);

            Debug.Assert(trailChunkEntries.ContainsKey( chunk ) );
            Debug.Assert(trailChunkEntries[chunk].ContainsKey(blockTrailID) );

            trailChunkEntries[chunk].Remove(blockTrailID);

            if ( trailChunkEntries[chunk].Count() ==0 )
                trailChunkEntries.Remove(chunk);
        }

        public bool BlockPosHasTrailData(IWorldAccessor world, BlockPos blockPos)
        {
            IWorldChunk chunk = world.BlockAccessor.GetChunkAtBlockPos(blockPos);
            Debug.Assert(chunk != null);

            long blockTrailID = ConvertBlockPositionToTrailPosID(blockPos);

            if (trailChunkEntries.ContainsKey(chunk))
            {
                if (trailChunkEntries[chunk].ContainsKey(blockTrailID))
                    return true;
            }

            return false;
        }

        public TrailBlockPosEntry GetBlockPosTrailData( IWorldAccessor world, BlockPos blockPos )
        {
            IWorldChunk chunk = world.BlockAccessor.GetChunkAtBlockPos( blockPos );
            Debug.Assert(chunk != null);

            if (trailChunkEntries.ContainsKey(chunk))
            {
                long blockTrailID = ConvertBlockPositionToTrailPosID(blockPos);

                if (trailChunkEntries[chunk].ContainsKey(blockTrailID))
                    return trailChunkEntries[chunk][blockTrailID];
            }

            Debug.Assert(false, "BlockPos does not have trail data, call BlockPosHasTrailData to check before calling this function.");

            return new TrailBlockPosEntry();
        }

        //Save From Mod Chunk

        //Load From Mod Chunk

        //Utility
        private void TryToTransformTrailBlock( IWorldAccessor world, BlockPos blockPos, int blockID, Entity touchEnt )
        {

            if ( trailBlockTouchTransforms.ContainsKey( blockID ) )
            {
                TrailBlockTouchTransformData trailBlockTransformData = trailBlockTouchTransforms[blockID];
                if ( world.Rand.NextDouble() <= trailBlockTransformData.transformChance )
                {

                    bool touchIsPlayer = false;
                    bool touchIsSneaking = false;

                    if ( touchEnt is EntityPlayer )
                    {
                        touchIsPlayer = true;

                        EntityPlayer touchPlayer = (EntityPlayer) touchEnt;

                        if (touchPlayer.Controls.Sneak)
                            touchIsSneaking = true;
                    }

                    if (trailBlockTransformData.transformByPlayerOnly && !touchIsPlayer)
                        return;

                    if ( !touchIsSneaking)
                    {
                        BlockPos upPos = blockPos.UpCopy();
                        Block upBlock = world.BlockAccessor.GetBlock(upPos);
                        if ( upBlock != null ) 
                        {
                            if (CanTramplePlant(upBlock))
                                world.BlockAccessor.BreakBlock(upPos, null, 0);
                        }
                    }

                    world.BlockAccessor.SetBlock(trailBlockTransformData.transformBlockID, blockPos);

                    //If our new block doesn't have a transform, clear it.
                    if (!trailBlockTouchTransforms.ContainsKey(trailBlockTransformData.transformBlockID))
                        RemoveBlockPosTrailData( world, blockPos );
                }
            }
        }

        public bool BlockCenterHorizontalInEntityBoundingBox(Entity ent, BlockPos blockPos ) 
        {
            if( BlockPosHasTrailData( ent.World, blockPos ) )
            {
                TrailBlockPosEntry blockTrailEntry = GetBlockPosTrailData( ent.World, blockPos );
                
                EntityProperties agentProperties = ent.World.GetEntityType(ent.Code);

                //Hande the case where the entry is invalid, or has been disabled by mod flags.
                if (agentProperties == null)
                    return false;

                Vec2f selBox = agentProperties.SelectionBoxSize;
                Vec3d posDeltaFlat = blockPos.ToVec3d() - ent.ServerPos.XYZ;

                float boundsMin = -Math.Max( 1, selBox.X);
                float boundsMax = Math.Max(1, selBox.X);

                if (boundsMin < posDeltaFlat.X && boundsMax > posDeltaFlat.X &&
                    boundsMin < posDeltaFlat.Z && boundsMax > posDeltaFlat.Z)
                    return true;
            }

            return false;
        }

        public bool ShouldTrackBlockTrailData( Block block ) 
        {
            if( trailBlockTouchTransforms.ContainsKey(block.Id) )
                return true;

            if (block.BlockMaterial == EnumBlockMaterial.Snow)
                return true;

            if (block.BlockMaterial == EnumBlockMaterial.Ice)
                return true;

            return false;
        }

        public static long ConvertBlockPositionToTrailPosID( BlockPos blockPos )
        {
            long XY = AppendDigits(blockPos.X, blockPos.Y);
            long XYZ = AppendDigits(XY, blockPos.Z);

            //This string concat is slow, we need a better method.
            long concatTest = (blockPos.X.ToString() + blockPos.Y.ToString() + blockPos.Z.ToString()).ToLong();
           
            Debug.Assert(XYZ == concatTest);

            return XYZ;
        }

        private static long AppendDigits(long value1, long value2)
        {
            long dn = (long)(Math.Ceiling(Math.Log10(value2 + 0.001)));     //0.001 is for exact 10, exact 100, ...
            long finalVal = (long)(value1 * Math.Ceiling(Math.Pow(10, dn))); //< ----because pow would give 99.999999(for some optimization modes)
            finalVal += value2;
            return finalVal;
        }

        private static bool CanTramplePlant( Block block )
        {
            if (block.BlockMaterial == EnumBlockMaterial.Plant)
            {
                if (block is BlockFern)
                    return true;

                if ( block is BlockTallGrass) 
                    return true;

                if (block is BlockLupine)
                    return true;

                string code = block.Code.FirstCodePart();

                if (code == "flower" )
                    return true;

                if (code == "tallfern")
                    return true;
            }

            return false;
        }
    }
}
