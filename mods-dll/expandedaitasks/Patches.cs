using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using ExpandedAiTasks.Managers;


namespace ExpandedAiTasks
{

    public static class ExpandedAiTasksHarmonyPatcher
    {
        private static Harmony harmony;

        public static bool ShouldPatch()
        {
            return harmony == null;
        }

        public static void ApplyPatches()
        {
            Debug.Assert(ShouldPatch(), "ExpandedAiTasks Harmony patches have already been applied, call ShouldPatch to determine if this method should be called.");
            harmony = new Harmony("com.grifthegnome.expandedaitasks.aitaskpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    //////////////////////////////////////////////////////////////
    ///PATCHING TO ADD ENTITIES INTO ENTITY LEDGER ON INITIALIZE//
    //////////////////////////////////////////////////////////////
    [HarmonyPatch(typeof(Entity))]
    public class AfterInitializedOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("AfterInitialized")]
        [HarmonyPostfix]
        static void OverrideAfterInitialized(Entity __instance, bool onFirstSpawn)
        {
            if (__instance.Api.Side == EnumAppSide.Server)
            {
                EntityManager.RegisterEntityWithEntityLedger(__instance);

                if (__instance is EntityProjectile && !EntityManager.IsRegisteredAsEntityProjectile(__instance))
                    EntityManager.RegisterEntityProjectile(__instance);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////
    ///PATCHING TO ADD A UNIVERAL SET LOCATION FOR LAST ENTITY TO ATTACK ON ENTITY AGENT//
    //////////////////////////////////////////////////////////////////////////////////////

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
            if (__instance.Alive)
            {
                Entity prevAttacker = AiUtility.GetLastAttacker(__instance);
                AiUtility.SetLastAttacker(__instance, damageSource);
                Entity newAttacker = AiUtility.GetLastAttacker(__instance);

                if (newAttacker != null && newAttacker != prevAttacker)
                    AiUtility.TryNotifyHerdMembersToAttack( __instance, AiUtility.GetLastAttacker(__instance), null, null, null, AiUtility.GetHerdAlertRangeForEntity(__instance), true );
            }
        }
    }
}