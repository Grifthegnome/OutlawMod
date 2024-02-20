using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ExpandedAiTasks;
using ExpandedAiTasks.Managers;

namespace EvilBelow
{
    /////////////////////////////////////////////////////////////////////////////
    /// PATCHING AI TASKS TO ADD PLAY SOUND EVENTS FOR VS CLASIC OUTLAW VOICES///
    /////////////////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(AiTaskMeleeAttack))]
    public class AiTaskMeleeAttackOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }


        [HarmonyPatch("StartExecute")]
        [HarmonyPostfix]
        static void OverrideAddSoundCallToStartExecute(AiTaskMeleeAttack __instance)
        {
            if (__instance.entity.Alive)
            {
                __instance.entity.PlayEntitySound("meleeattack", null, true);
            }
        }
    }

    [HarmonyPatch(typeof(AiTaskFleeEntity))]
    public class AiTaskFleeEntityOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }


        [HarmonyPatch("StartExecute")]
        [HarmonyPostfix]
        static void OverrideAddSoundCallToStartExecute(AiTaskFleeEntity __instance)
        {
            if (__instance.entity.Alive)
            {
                __instance.entity.PlayEntitySound("fleeentity", null, true);
            }
        }
    }

    [HarmonyPatch(typeof(AiTaskSeekEntity))]
    public class AiTaskSeekEntityOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }


        [HarmonyPatch("StartExecute")]
        [HarmonyPostfix]
        static void OverrideAddSoundCallToStartExecute(AiTaskSeekEntity __instance)
        {
            if (__instance.entity.Alive)
            {
                __instance.entity.PlayEntitySound("seekentity", null, true);
            }
        }
    }

    //////////////////////////////////////////////////////////////
    /// PATCHING ENTITY HEALTH BEHAVIOR TO ALLOW SNEAK ATTACKS ///
    //////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(EntityBehaviorHealth))]
    public class OnEntityReceiveDamageOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            if (original != null)
            {

                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name)
                        return false;
                }
            }

            return true;
        }

        [HarmonyPatch("OnEntityReceiveDamage")]
        [HarmonyPrefix]
        static void OverrideOnEntityReceiveDamage(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
        {
            Entity attacker = damageSource.SourceEntity;

            if (attacker is EntityProjectile && damageSource.CauseEntity != null)
            {
                attacker = damageSource.CauseEntity;
            }

            //Give player super sneak attack damage if the target is not in combat and has not been in combat for 10 seconds.
            if (attacker is EntityPlayer && !AiUtility.IsInCombat(__instance.entity) && __instance.entity.World.ElapsedMilliseconds - AiUtility.GetLastTimeEntityInCombatMs(__instance.entity) > 10000.0f && damageSource.Type != EnumDamageType.Heal)
            {
                if ( !AwarenessManager.IsAwareOfTarget( __instance.entity, attacker, 60, 60 ) )
                {
                    if ( AiUtility.AttackWasFromProjectile(damageSource) )
                    {
                        damage *= EBGlobalConstants.sneakAttackDamageMultRanged;
                    }
                    else
                    {
                        damage *= EBGlobalConstants.sneakAttackDamageMultMelee;
                    }
                }
            }
        }

    }
}