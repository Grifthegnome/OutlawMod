﻿using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Client;


namespace TrailMod
{

    //////////////////////////////////////////////////////////////////////////////////////
    ///PATCHING TO ADD A UNIVERAL SET LOCATION FOR LAST ENTITY TO ATTACK ON ENTITY AGENT//
    //////////////////////////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(Block))]
    public class OverrideOnEntityCollide
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("OnEntityCollide")]
        [HarmonyPostfix]
        static void OnEntityCollideOverride(Block __instance, IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Side.IsClient() )
                return;

            if (entity == null)
                return;
            
            if ( !entity.Alive )
                return;

            if (!(entity is EntityAgent))
                return;

            //Only run trail logic within 100 blocks of a player.
            if (entity.minRangeToClient > 100)
                return;

            if ( entity is EntityPlayer )
            {
                EntityPlayer entityPlayer = (EntityPlayer)entity;
                if(entityPlayer.Player.WorldData.CurrentGameMode != EnumGameMode.Survival)
                    return;
            }
               
            if (world.Side == EnumAppSide.Client)
                return;

            if (!entity.Collided)
                return;

            TrailChunkManager trailChunkManager = TrailChunkManager.GetTrailChunkManager();
            bool shouldTrackTrailData = trailChunkManager.ShouldTrackBlockTrailData(__instance);

            if (shouldTrackTrailData)
            {
                if (facing == BlockFacing.UP)
                    trailChunkManager.AddOrUpdateBlockPosTrailData(world, __instance, pos, entity);
            
                //Check if the center of the block overlaps the entity bounding box.
                if (!trailChunkManager.BlockCenterHorizontalInEntityBoundingBox(entity, pos))
                    return;

                float snowLevel = __instance.snowLevel;

                switch (__instance.BlockMaterial)
                {

                    case EnumBlockMaterial.Snow:

                        if (snowLevel > 0)
                        {
                            if (__instance is BlockSnowLayer)
                            {
                                BlockSnowLayer snowLayer = (BlockSnowLayer)__instance;

                                if (snowLevel == 1)
                                {
                                    Block baseSnowBlock = world.GetBlock(snowLayer.CodeWithVariant("height", "" + 1));
                                    world.BlockAccessor.SetBlock(baseSnowBlock.Id, pos);
                                    return;
                                }

                                Block block = world.GetBlock(snowLayer.CodeWithVariant("height", "" + (snowLevel - 1)));
                                world.BlockAccessor.SetBlock(block.Id, pos);

                                __instance.snowLevel = Math.Clamp(snowLevel - 1, 0, snowLevel);

                            }

                            if (__instance is BlockTallGrass)
                            {
                                BlockTallGrass tallGrass = (BlockTallGrass)__instance;
                                Block baseTallGrassBlock = world.GetBlock(tallGrass.CodeWithVariant("cover", "snow"));
                                world.BlockAccessor.SetBlock(baseTallGrassBlock.Id, pos);
                                __instance.snowLevel = 1;
                                return;                         
                            }

                            break;
                        }

                        break;

                    case EnumBlockMaterial.Ice:

                        if (__instance is BlockLakeIce)
                        {
                            if (world.Rand.NextDouble() < 0.001)
                            {
                                BlockFacing[] horizontals = BlockFacing.HORIZONTALS;

                                foreach (BlockFacing blockFacing in horizontals)
                                {
                                    BlockPos possibleIcePos = pos.AddCopy(blockFacing);
                                    Block possibleIceBlock = world.BlockAccessor.GetBlock(possibleIcePos);

                                    if (possibleIceBlock == null)
                                        continue;

                                    if (possibleIceBlock is BlockLakeIce)
                                    {
                                        world.BlockAccessor.BreakBlock(possibleIcePos, null);
                                    }
                                }

                                world.BlockAccessor.BreakBlock(pos, null);

                            }
                        }

                        break;

                }
            }
        }
    }
}