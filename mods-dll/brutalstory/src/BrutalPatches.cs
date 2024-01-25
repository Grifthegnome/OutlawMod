using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Server;
using System.Linq;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;


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

                //If we take fall damage and don't have a hit location, set it to the local origin of the victim.
                if (damageSource.Type == EnumDamageType.Gravity && damageSource.HitPosition == null)
                    damageSource.HitPosition = new Vec3d(0, 0, 0);
                    
                
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
                    damage              = damage,
                    ServerDamagePos     = __instance.ServerPos.XYZ
                };

                BrutalBroadcast.serverCoreApi.Network.GetChannel("brutalPacket").BroadcastPacket(packet);

                BloodFX.HandleBrutalDamage_Server(__instance, damageSource, damage);
            }

            if ( __instance.Api.Side == EnumAppSide.Client)
            {
                if (!BrutalUtility.DoesEntityAgentBleed(__instance))
                    return;

                BloodFX.Bleed(__instance, damageSource, damage, __instance.ServerPos.XYZ);
            }
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    ///PATCHING TO STORE HARVESTABLE DROPS AS ATTRIBUTES SO WE CAN SPAWN THEM ON BRUTAL GIB//
    /////////////////////////////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(EntityBehaviorHarvestable))]
    public class HarvestableInitializeOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        static void OverrideInitialize(EntityBehaviorHarvestable __instance, EntityProperties properties, JsonObject typeAttributes)
        {

            if (__instance.entity.World.Api.Side == EnumAppSide.Server)
            {

                BlockDropItemStack[] drops = typeAttributes["drops"].AsObject<BlockDropItemStack[]>();
                string[] dropCodes = new string[drops.Count()];
                int[] dropQuantity = new int[drops.Count()];
                string[] dropType = new string[drops.Count()];

                TreeAttribute dropEntries = new TreeAttribute();

                for (int i = 0; i < drops.Count(); i++)
                {
                    TreeAttribute dropEntry = new TreeAttribute();

                    dropCodes[i] = drops[i].Code.ToString();

                    dropQuantity[i] = GameMath.RoundRandom(__instance.entity.World.Rand, drops[i].Quantity.nextFloat());

                    switch(drops[i].Type )
                    {
                        case EnumItemClass.Block:
                            dropType[i] = "block";
                            break;
                        case EnumItemClass.Item:
                            dropType[i] = "item";
                            break;
                    }
                    
                    dropEntry.SetInt("quantity", dropQuantity[i]);
                    dropEntry.SetString( "type", dropType[i]);
                    dropEntries.SetAttribute(drops[i].Code.FirstPathPart(), dropEntry);
                }

                int minBones = 4;
                int maxBones = 6;

                TreeAttribute boneEntry = new TreeAttribute();
                boneEntry.SetInt("quantity", (int)MathUtility.GraphClampedValue(0, 1, minBones, maxBones, __instance.entity.World.Rand.NextDouble()));
                boneEntry.SetString("type", "item");
                dropEntries.SetAttribute("game:bone", boneEntry);

                __instance.entity.Attributes.SetAttribute("brutalSplatDropCodes", dropEntries);
            }
        }
    }
}