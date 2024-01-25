using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
        public Vec3d ServerDamagePos;

        public float damage;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BrutalNetworkTest
    {

    }
    public class BrutalBroadcast
    {
        public static ICoreClientAPI clientCoreApi;
        public static ICoreServerAPI serverCoreApi;

        public static void InitServer( ICoreServerAPI sapi )
        {
            serverCoreApi = sapi;
        }

        public static void InitClient( ICoreClientAPI capi )
        {
            clientCoreApi = capi;
        }

        public static void HandleDamagePacket(BrutalDamagePacket networkMessage)
        {
            Entity victimEntity = null;
            Entity sourceEntity = null;
            Entity causeEntity = null;
            //Block sourceBlock = null;

            EntityAgent victimAgent = null;
            if (networkMessage.victimEntityID != -1)
            {
                victimEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById(networkMessage.victimEntityID);

                if (victimEntity == null)
                    return;

                Debug.Assert(victimEntity is EntityAgent);
                victimAgent = (EntityAgent)victimEntity;
            }


            if (networkMessage.SourceEntityID != -1)
                sourceEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById(networkMessage.SourceEntityID);

            if (networkMessage.CauseEntityID != -1)
                causeEntity = BrutalBroadcast.clientCoreApi.World.GetEntityById(networkMessage.CauseEntityID);

            //sourceBlock = BrutalBroadcast.clientCoreApi.World.BlockAccessor.GetBlock(networkMessage.SourceBlockPos);

            DamageSource damageSource = new DamageSource();
            damageSource.Source = networkMessage.Source;
            damageSource.Type = networkMessage.Type;
            damageSource.HitPosition = networkMessage.HitPosition;
            damageSource.SourceEntity = sourceEntity;
            damageSource.CauseEntity = causeEntity;
            //damageSource.SourceBlock        = sourceBlock;
            damageSource.SourcePos = networkMessage.SourcePos;
            damageSource.DamageTier = networkMessage.DamageTier;
            damageSource.KnockbackStrength = networkMessage.KnockbackStrength;

            BloodFX.Bleed(victimAgent, damageSource, networkMessage.damage, networkMessage.ServerDamagePos);
        }

        public static void HandleNetworkTest(BrutalNetworkTest networkMessage)
        {
            BrutalBroadcast.clientCoreApi.ShowChatMessage("BRUTAL MESSAGE RECIEVED FROM BRUTAL SERVER");
        }

        public static void OnBrutalClientTestCmd(int groupId, CmdArgs args)
        {
            ICoreClientAPI capi = BrutalBroadcast.clientCoreApi;
        }
    }
}
