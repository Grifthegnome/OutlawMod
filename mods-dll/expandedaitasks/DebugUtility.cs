using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace ExpandedAiTasks
{
    public static class DebugUtility
    {
        public static void DebugDrawPosition(IWorldAccessor world, Vec3d pos, int red, int green, int blue)
        {
            BlockPos blockPos = new BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z);
            DebugDrawBlockLocation(world, blockPos, red, green, blue);
        }
        public static void DebugDrawBlockLocation(IWorldAccessor world, BlockPos blockPos, int red, int green, int blue )
        {
            // Debug visualization
            Debug.Assert(red >= 0 && red <= 255);
            Debug.Assert(green >= 0 && green <= 255);
            Debug.Assert(blue >= 0 && blue <= 255);

            List<BlockPos> blockPositions = new List<BlockPos>();
            blockPositions.Add(blockPos);

            int color = ColorUtil.ColorFromRgba(red, green, blue, 150);
            List<int> colors = new List<int>();
            colors.Add(color);

            IPlayer player = world.AllOnlinePlayers[0];
            world.HighlightBlocks(player, 2, blockPositions, colors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
        }

        public static void DebugTargetPositionAndLaskKnownPositionBlockLocation(IWorldAccessor world, Vec3d targetPos, Vec3d lkpPos )
        {
            BlockPos targetBlockPos = new BlockPos((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z);
            BlockPos lkpBlockPos = new BlockPos((int)lkpPos.X, (int)lkpPos.Y, (int)lkpPos.Z);

            // Debug visualization
            List<BlockPos> blockPositions = new List<BlockPos>();
            blockPositions.Add(targetBlockPos);
            blockPositions.Add(lkpBlockPos);

            int colorTarget = ColorUtil.ColorFromRgba(255, 0, 0, 150);
            int colorLKP = ColorUtil.ColorFromRgba(0, 255, 0, 150);
            List<int> colors = new List<int>();
            colors.Add(colorTarget);
            colors.Add(colorLKP);

            IPlayer player = world.AllOnlinePlayers[0];
            world.HighlightBlocks(player, 2, blockPositions, colors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
        }

        public static void DebugTargetPositionAndLaskKnownPositionandCurrentNavPositionBlockLocation(IWorldAccessor world, Vec3d targetPos, Vec3d lkpPos, PathTraverserBase pathTraverser)
        {
            BlockPos targetBlockPos = new BlockPos((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z);
            BlockPos lkpBlockPos = new BlockPos((int)lkpPos.X, (int)lkpPos.Y, (int)lkpPos.Z);
            BlockPos currentNavBlockPos = new BlockPos((int)pathTraverser.CurrentTarget.X, (int)pathTraverser.CurrentTarget.Y, (int)pathTraverser.CurrentTarget.Z);

            // Debug visualization
            List<BlockPos> blockPositions = new List<BlockPos>();
            blockPositions.Add(targetBlockPos);
            blockPositions.Add(lkpBlockPos);
            blockPositions.Add(currentNavBlockPos);

            int colorTarget = ColorUtil.ColorFromRgba(255, 0, 0, 150);
            int colorLKP = ColorUtil.ColorFromRgba(0, 255, 0, 150);
            int colorNav = ColorUtil.ColorFromRgba(0, 0, 255, 150);
            List<int> colors = new List<int>();
            colors.Add(colorTarget);
            colors.Add(colorLKP);
            colors.Add(colorNav);

            IPlayer player = world.AllOnlinePlayers[0];
            world.HighlightBlocks(player, 2, blockPositions, colors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
        }

        public static void DebugDrawBlockPath(  )
        {
            /*
            // Debug visualization
            List<BlockPos> poses = new List<BlockPos>();
            List<int> colors = new List<int>();
            int i = 0;

            foreach (var node in waypoints)
            {
                poses.Add(node.AsBlockPos);
                colors.Add(ColorUtil.ColorFromRgba(128, 128, Math.Min(255, 128 + i * 8), 150));
                i++;
            }

            poses.Add(desiredTarget.AsBlockPos);
            colors.Add(ColorUtil.ColorFromRgba(128, 0, 255, 255));

            IPlayer player = entity.World.AllOnlinePlayers[0];
            entity.World.HighlightBlocks(player, 2, poses,
                colors,
                API.Client.EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary
            );
            */
        }

    }
}
