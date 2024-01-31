
using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TrailMod
{

    public class TrailModCore : ModSystem
    {

        private Harmony harmony;
        private TrailChunkManager trailChunkManager;

        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            harmony = new Harmony("com.grifthegnome.trailmod.trailpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            RegisterBlocksShared(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            trailChunkManager = TrailChunkManager.GetTrailChunkManager();
            trailChunkManager.InitData( api.World, api );

            api.Event.RegisterCallback(trailChunkManager.Clean, (int)TrailChunkManager.TRAIL_CLEANUP_INTERVAL);

            api.Event.ChunkDirty += trailChunkManager.OnChunkDirty;
            api.Event.ChunkColumnUnloaded += trailChunkManager.OnChunkColumnUnloaded;

            api.Event.SaveGameLoaded += trailChunkManager.OnSaveGameLoading;
            api.Event.GameWorldSave += trailChunkManager.OnSaveGameSaving;

            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, () => {
                trailChunkManager.ShutdownSaveState();
                //Clean up all manager stuff.
                //If we don't it persists between loads.
                trailChunkManager.ShutdownCleanup();
                trailChunkManager = null;
            });
        }

        private void RegisterBlocksShared(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockTrail", typeof(BlockTrail));
        }
    }
}
