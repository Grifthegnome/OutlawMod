
using HarmonyLib;
using ProtoBuf;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TrailMod
{
    [ProtoContract]
    public class TrailModConfig
    {
        [ProtoMember(1)]
        public bool dirtRoadsOnly = false;
    }
    public class TrailModCore : ModSystem
    {

        TrailModConfig config = new TrailModConfig();

        private Harmony harmony;
        private TrailChunkManager trailChunkManager;

        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            if ( api.Side == EnumAppSide.Server )
            {
                ReadConfigFromJson(api);
                ApplyConfigPatchFlags(api);
            }
            
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

        private void ReadConfigFromJson(ICoreAPI api)
        {
            //Called Server Only
            try
            {
                TrailModConfig modConfig = api.LoadModConfig<TrailModConfig>("TrailModConfig.json");

                if (modConfig != null)
                {
                    config = modConfig;
                }
                else
                {
                    //We don't have a valid config.
                    throw new Exception();
                }

            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading TrailModConfig.json, Will initialize new one", e);
                config = new TrailModConfig();
                api.StoreModConfig(config, "TrailModConfig.json");
            }
        }

        private void ApplyConfigPatchFlags(ICoreAPI api)
        {
            //Enable/Disable Config Settngs
            api.World.Config.SetBool("dirtRoadsOnly", config.dirtRoadsOnly);
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }
}
