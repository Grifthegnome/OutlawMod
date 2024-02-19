using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Util;
using Vintagestory.API.Config;

namespace BrutalStory
{
    /*
      ____             _        _   _   _ _   _ _ _ _         
     | __ ) _ __ _   _| |_ __ _| | | | | | |_(_) (_) |_ _   _ 
     |  _ \| '__| | | | __/ _` | | | | | | __| | | | __| | | |
     | |_) | |  | |_| | || (_| | | | |_| | |_| | | | |_| |_| |
     |____/|_|   \__,_|\__\__,_|_|  \___/ \__|_|_|_|\__|\__, |
                                                        |___/  
    */
    public static class BrutalUtility
    {
        public static bool DoesEntityAgentBleed( EntityAgent agent )
        {
            switch( agent.Code.ToString() )
            {
                case "game:strawdummy":
                    return false;
            }

            return true;
        }

/*
    ____                           _   _   _ _   _ _ _ _         
   / ___| ___ _ __   ___ _ __ __ _| | | | | | |_(_) (_) |_ _   _ 
  | |  _ / _ \ '_ \ / _ \ '__/ _` | | | | | | __| | | | __| | | |
  | |_| |  __/ | | |  __/ | | (_| | | | |_| | |_| | | | |_| |_| |
   \____|\___|_| |_|\___|_|  \__,_|_|  \___/ \__|_|_|_|\__|\__, |
                                                           |___/ 
 */
        public static Vec3d GetCenterMass(Entity ent)
        {
            if (ent.SelectionBox.Empty)
                return ent.SidedPos.XYZ;

            float heightOffset = ent.SelectionBox.Y2 - ent.SelectionBox.Y1;
            return ent.SidedPos.XYZ.Add(0, heightOffset, 0);
        }

        public static bool LocationInLiquid(IWorldAccessor world, Vec3d pos)
        {
            BlockPos blockPos = pos.AsBlockPos;
            Block block = world.BlockAccessor.GetBlock(blockPos);

            if (block != null)
            {
                return block.BlockMaterial == EnumBlockMaterial.Liquid;
            }

            return false;
        }

        public static Vec3d ClampPositionToGround(IWorldAccessor world, Vec3d startingPos, int maxBlockDistance)
        {
            BlockPos posAsBlockPos = startingPos.AsBlockPos;
            BlockPos previousCheckPos = posAsBlockPos.Copy();
            BlockPos currentCheckPos = posAsBlockPos.Copy();

            Block currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

            if (currentBlock == null)
            {
                return startingPos;
            }
            else
            {
                //our starting point is in solid
                if (IsPositionInSolid(world, startingPos))
                    return PopPositionAboveGround(world, startingPos, maxBlockDistance);
            }

            int groundCheckTries = 0;
            while (maxBlockDistance > groundCheckTries)
            {
                currentCheckPos = previousCheckPos.DownCopy();
                currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

                //Check Block Below us.
                if (currentBlock != null)
                {
                    if (IsPositionInSolid(world, currentCheckPos))
                    {
                        return new Vec3d(previousCheckPos.X, previousCheckPos.Y, previousCheckPos.Z);
                    }

                }

                previousCheckPos = currentCheckPos;
                groundCheckTries++;
            }

            return startingPos;

        }

        //WE WILL LIKELY NEED THIS FOR BLOOD TRACES, BUT WE NEVER FINISHED IMPLEMENTING IT AND MAKING SURE IT WORKS PROPERLY. SORT THIS!
        public static Vec3d ClampVectorToSurface(IWorldAccessor world, Vec3d startingPos, Vec3d vectorToClamp, float maxDistance)
        {
            return TraceToSurface(world, startingPos, vectorToClamp, maxDistance);
        }

        private static BlockSelection blockSel = null;
        private static EntitySelection entitySel = null;

        public static Vec3d TraceToSurface(IWorldAccessor world, Vec3d pos, Vec3d dir, float maxTraceDist)
        {
            blockSel = null;
            entitySel = null;

            world.RayTraceForSelection(pos, pos + (dir * maxTraceDist), ref blockSel, ref entitySel, TraceToGround_BlockFilter, TraceToGround_EntityFilter);
            
            BrutalDebugUtility.DebugDrawRayTrace(world, pos, pos + (dir * maxTraceDist), TraceToGround_BlockFilter, TraceToGround_EntityFilter);

            if (blockSel != null)
            {
                return blockSel.FullPosition;
            }
            

            return pos;
        }

        private static bool TraceToGround_BlockFilter(BlockPos pos, Block block)
        {
            return block.SideIsSolid(pos, BlockFacing.UP.Index);
        }

        private static bool TraceToGround_EntityFilter(Entity ent)
        {
            return false;
        }

        public static Vec3d ClampVectorToGround(IWorldAccessor world, Vec3d startingPos, int maxBlockDistance)
        {
            BlockPos posAsBlockPos = startingPos.AsBlockPos;
            BlockPos previousCheckPos = posAsBlockPos.Copy();
            BlockPos currentCheckPos = posAsBlockPos.Copy();

            Block currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

            if (currentBlock == null)
            {
                return startingPos;
            }
            else
            {
                //our starting point is in solid
                if (IsPositionInSolid(world, startingPos))
                    return PopPositionAboveGround(world, startingPos, maxBlockDistance);
            }

            int groundCheckTries = 0;
            while (maxBlockDistance > groundCheckTries)
            {
                currentCheckPos = previousCheckPos.DownCopy();
                currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

                //Check Block Below us.
                if (currentBlock != null)
                {
                    if (IsPositionInSolid(world, currentCheckPos))
                    {
                        return new Vec3d(previousCheckPos.X, previousCheckPos.Y, previousCheckPos.Z);
                    }

                }

                previousCheckPos = currentCheckPos;
                groundCheckTries++;
            }

            return startingPos;

        }

        public static Vec3d PopPositionAboveGround(IWorldAccessor world, Vec3d startingPos, int maxBlockDistance)
        {
            BlockPos posAsBlockPos = startingPos.AsBlockPos;
            BlockPos previousCheckPos = posAsBlockPos.Copy();
            BlockPos currentCheckPos = posAsBlockPos.Copy();

            Block currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

            if (currentBlock == null)
            {
                return startingPos;
            }
            else
            {
                //our starting point is in solid
                if (!IsPositionInSolid(world, startingPos))
                    return startingPos;
            }

            int groundCheckTries = 0;
            while (maxBlockDistance > groundCheckTries)
            {
                currentCheckPos = previousCheckPos.UpCopy();
                currentBlock = world.BlockAccessor.GetBlock(currentCheckPos);

                //Check Block Below us.
                if (currentBlock != null)
                {
                    if (!IsPositionInSolid(world, currentCheckPos))
                    {
                        return new Vec3d(currentCheckPos.X, currentCheckPos.Y, currentCheckPos.Z);
                    }

                }

                previousCheckPos = currentCheckPos;
                groundCheckTries++;
            }

            return startingPos;
        }

        public static Vec3d MovePositionByBlockInDirectionOfVector(Vec3d positionToMove, Vec3d directionToMove)
        {
            BlockPos endBlockPos = (positionToMove + directionToMove).AsBlockPos;
            return new Vec3d(endBlockPos.X, endBlockPos.Y, endBlockPos.Z);
        }

        public static bool IsPositionInSolid(IWorldAccessor world, Vec3d pos)
        {
            BlockPos blockPos = pos.AsBlockPos;
            return IsPositionInSolid(world, blockPos);
        }

        public static bool IsPositionInSolid(IWorldAccessor world, BlockPos blockPos)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block blockAtPos = blockAccessor.GetBlock(blockPos);

            bool solid = blockAtPos.BlockMaterial != EnumBlockMaterial.Air && blockAtPos.BlockMaterial != EnumBlockMaterial.Liquid && blockAtPos.BlockMaterial != EnumBlockMaterial.Snow &&
                blockAtPos.BlockMaterial != EnumBlockMaterial.Plant && blockAtPos.BlockMaterial != EnumBlockMaterial.Leaves;

            if (solid)
            {
                bool confirmedSolid = false;
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    if (blockAtPos.SideSolid[facing.Index] == true)
                    {
                        confirmedSolid = true;
                        break;
                    }

                    BlockEntity blockEnt = blockAccessor.GetBlockEntity(blockPos);
                    if (blockAtPos is BlockMicroBlock)
                    {
                        if (blockAccessor.GetBlockEntity(blockPos) is BlockEntityMicroBlock)
                        {
                            BlockEntityMicroBlock microBlockEnt = blockAccessor.GetBlockEntity(blockPos) as BlockEntityMicroBlock;
                            if (microBlockEnt.sideAlmostSolid[facing.Index] == true)
                            {
                                confirmedSolid = true;
                                break;
                            }
                        }
                    }
                }

                solid = confirmedSolid;
            }

            return solid;
        }

        public static bool EntityCodeInList(Entity ent, List<string> codes)
        {
            foreach (string code in codes)
            {
                if (ent.Code.Path == code)
                    return true;

                if (ent.Code.Path.StartsWithFast(code))
                    return true;
            }

            return false;
        }
    }
}
