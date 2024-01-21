using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System;
using brutalstory.src;
using Vintagestory.API.Server;
using System.Linq;


namespace BrutalStory
{

    /////////////////////////////////////////////////////////
    ///PATCHING TO ADD BLOOD ON ENTITY AGENT RECIEVE DAMAGE//
    /////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(EntityAgent))]
    public class ReceiveDamageOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("ReceiveDamage")]
        [HarmonyPostfix]
        static void OverrideReceiveDamage(EntityAgent __instance, DamageSource damageSource, float damage)
        {

            if ( __instance.Api.Side == EnumAppSide.Server )
            {

                if (!BrutalUtility.DoesEntityAgentBleed(__instance))
                    return;

                long victimEntityID = -1;
                long sourceEntityID = -1;
                long causeEntityID  = -1;
                //BlockPos sourceBlockPos = null;

                if (__instance != null)
                    victimEntityID = __instance.EntityId;

                if (damageSource.SourceEntity != null)
                    sourceEntityID = damageSource.SourceEntity.EntityId;

                if ( damageSource.CauseEntity != null)
                    causeEntityID = damageSource.CauseEntity.EntityId;

                //We need to figure out how to get a BlockPos from a Block.
                //if ( damageSource.SourceBlock != null )
                //    sourceBlockPos = damageSource.SourceBlock.

                BrutalDamagePacket packet = new BrutalDamagePacket() 
                { 
                    victimEntityID      = victimEntityID,
                    Source              = damageSource.Source,
                    Type                = damageSource.Type,
                    HitPosition         = damageSource.HitPosition,
                    SourceEntityID      = sourceEntityID,
                    CauseEntityID       = causeEntityID,
                    //SourceBlockPos         = damageSource.SourceBlock,
                    SourcePos           = damageSource.SourcePos,
                    DamageTier          = damageSource.DamageTier,
                    KnockbackStrength   = damageSource.KnockbackStrength,
                    damage              = damage 
                };

                //Don't broadcast to the player dealing the damage.
                /*
                IServerPlayer[] ignorePlayers = {};
                if ( damageSource.SourceEntity is EntityPlayer )
                {
                    EntityPlayer player = (EntityPlayer)__instance;


                    IServerPlayer attackerPlayer = (IServerPlayer)BrutalBroadcast.serverCoreApi.World.PlayerByUid(player.PlayerUID);
                    ignorePlayers.Append(attackerPlayer);
                }
                */

                BrutalBroadcast.serverCoreApi.Network.GetChannel("brutalPacket").BroadcastPacket(packet);

                //Do screenshake for players on damage.
                if ( __instance is EntityPlayer )
                {
                    EntityPlayer player = (EntityPlayer) __instance;

                    IServerPlayer victimPlayer = (IServerPlayer)BrutalBroadcast.serverCoreApi.World.PlayerByUid( player.PlayerUID );

                    float shakeStrength = 0.0125f;

                    //This screen shake isn't right for our purposes, we will need our own brutal shake system.
                    //We should mess around with it further and see what it can do.

                    //BrutalBroadcast.serverCoreApi.Network.GetChannel("screenshake").SendPacket(new ScreenshakePacket() { Strength = shakeStrength }, victimPlayer);
                }
            }

            if ( __instance.Api.Side == EnumAppSide.Client)
            {
                if (!BrutalUtility.DoesEntityAgentBleed(__instance))
                    return;

                BloodFX.Bleed(__instance, damageSource, damage);
            }
        }
    }
}