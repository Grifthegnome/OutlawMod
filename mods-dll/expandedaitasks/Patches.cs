using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;


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

    //////////////////////////////////////////////////////////////////
    ///PATCHING TO ADD ENTITIES INTO ENTITY LEDGER ON LOAD FROM DISK//
    //////////////////////////////////////////////////////////////////
    [HarmonyPatch(typeof(Entity))]
    public class OnEntityLoadedOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("OnEntityLoaded")]
        [HarmonyPostfix]
        static void OverrideOnEntityLoaded(Entity __instance)
        {
            EntityManager.RegisterEntityWithEntityLedger(__instance);

            if (!__instance.Alive && !__instance.ShouldDespawn)
                EntityManager.RegisterDeadEntity(__instance);

            if (__instance is EntityProjectile)
                EntityManager.RegisterEntityProjectile(__instance);
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
                AiUtility.SetLastAttacker(__instance, damageSource);
            }

            if ( AiUtility.GetLastAttacker( __instance) != null )
                AiUtility.TryNotifyHerdMembersToAttack( __instance, AiUtility.GetLastAttacker(__instance), AiUtility.GetHerdAlertRangeForEntity(__instance), true );
        }
    }
}