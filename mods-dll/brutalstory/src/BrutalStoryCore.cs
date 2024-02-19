using HarmonyLib;
using ProtoBuf;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BrutalStory
{
    public class BrutalStoryModCore : ModSystem
    {

        private Harmony harmony;

        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            harmony = new Harmony("com.grifthegnome.brutalstory.brutalpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            api.Network
                .RegisterChannel("brutalPacket")
                .RegisterMessageType(typeof(BrutalDamagePacket))
                .RegisterMessageType(typeof(BrutalNetworkTest))
                ;
        }

        #region Server
        public override void StartServerSide(ICoreServerAPI api)
        {
            BrutalBroadcast.InitServer(api);

            base.StartServerSide(api);

            Debug.Assert( api.Network.GetChannel( "brutalPacket" ) != null );
        }

        private void OnBrutalTestCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            BrutalBroadcast.serverCoreApi.Network.GetChannel("brutalPacket").BroadcastPacket(new BrutalNetworkTest());
        }

        #endregion //Server

        #region Client
        public override void StartClientSide(ICoreClientAPI api)
        {
            BrutalBroadcast.InitClient(api);

            base.StartClientSide(api);

            BloodFX.Init();

            Debug.Assert(api.Network.GetChannel("brutalPacket") != null);

            api.Network.GetChannel( "brutalPacket" )
                .SetMessageHandler<BrutalDamagePacket>(BrutalBroadcast.HandleDamagePacket)
                //.SetMessageHandler<BrutalNetworkTest>(BrutalBroadcast.HandleNetworkTest)
                ;

        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        #endregion //Client
    }
}
