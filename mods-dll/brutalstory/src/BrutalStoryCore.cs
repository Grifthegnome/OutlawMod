
using brutalstory.src;
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
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BrutalDamagePacket
    {
        public long victimEntityID;

        public EnumDamageSource Source;
        public EnumDamageType Type;
        public Vec3d HitPosition;
        public long SourceEntityID;
        public long CauseEntityID;
       // public BlockPos SourceBlockPos;
        public Vec3d SourcePos;
        public int DamageTier = 0;
        public float KnockbackStrength = 1f;

        public float damage;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BrutalNetworkTest
    {

    }

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

            api.RegisterCommand("brutaltest", "Send a test network message", "", OnBrutalTestCmd, Privilege.controlserver);
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
                .SetMessageHandler<BrutalDamagePacket>(HandleDamagePacket)
                //.SetMessageHandler<BrutalNetworkTest>(HandleNetworkTest)
                ;

            api.RegisterCommand("clienttest", "Send a test network message", "", OnBrutalClientTestCmd );

        }

        private void HandleDamagePacket(BrutalDamagePacket networkMessage)
        {
            Entity victimEntity = null;
            Entity sourceEntity = null;
            Entity causeEntity = null;
            //Block sourceBlock = null;

            EntityAgent victimAgent = null;
            if ( networkMessage.victimEntityID != -1 )
            {
                victimEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById(networkMessage.victimEntityID);

                if (victimEntity == null)
                    return;

                Debug.Assert( victimEntity is EntityAgent );
                victimAgent = (EntityAgent) victimEntity;
            }
                

            if ( networkMessage.SourceEntityID != -1 ) 
                sourceEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById( networkMessage.SourceEntityID );

            if ( networkMessage.CauseEntityID != -1 )
                causeEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById( networkMessage.CauseEntityID );

            //sourceBlock = BrutalBroadcast.clientCoreApi.World.BlockAccessor.GetBlock(networkMessage.SourceBlockPos);

            DamageSource damageSource       = new DamageSource();
            damageSource.Source             = networkMessage.Source;
            damageSource.Type               = networkMessage.Type;
            damageSource.HitPosition        = networkMessage.HitPosition;
            damageSource.SourceEntity       = sourceEntity;
            damageSource.CauseEntity        = causeEntity;
            //damageSource.SourceBlock        = sourceBlock;
            damageSource.SourcePos          = networkMessage.SourcePos;
            damageSource.DamageTier         = networkMessage.DamageTier;
            damageSource.KnockbackStrength  = networkMessage.KnockbackStrength;

            BloodFX.Bleed(victimAgent, damageSource, networkMessage.damage);
        }

        private void HandleNetworkTest(BrutalNetworkTest networkMessage) 
        {
            BrutalBroadcast.clientCoreApi.ShowChatMessage("BRUTAL MESSAGE RECIEVED FROM BRUTAL SERVER" );
        }

        private void OnBrutalClientTestCmd( int groupId, CmdArgs args)
        {
            ICoreClientAPI capi = BrutalBroadcast.clientCoreApi;
        }

        #endregion //Client
    }
}
