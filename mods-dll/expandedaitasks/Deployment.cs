
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public static class ExpandedAiTasksDeployment
    {
        public static void Deploy( ICoreAPI api )
        {
            //Apply AiExpandedTask Patches if they haven't already been applied.
            if (ExpandedAiTasksHarmonyPatcher.ShouldPatch())
                ExpandedAiTasksHarmonyPatcher.ApplyPatches();

            if ( api.Side == EnumAppSide.Server )
            {
                RegisterAiTasksOnServer();
                IlluminationManager.Init(api as ICoreServerAPI);
                AiUtility.Init();
                api.Event.OnEntityDespawn += IlluminationManager.OnDespawn;
                api.Event.OnEntityDespawn += AwarenessManager.OnDespawn;

                api.Event.OnEntityDeath += AwarenessManager.OnDeath;

                //Tell Entity Manager to Track Projectile Spawns.
                api.Event.OnEntitySpawn += EntityManager.OnEntityProjectileSpawn;
                api.Event.OnEntityDeath += EntityManager.OnEntityDeath;
            }

            RegisterAiTasksShared();
            RegisterEntityBehaviors(api);
        }
        private static void RegisterAiTasksOnServer()
        {
            //We need to make sure we don't double register with outlaw mod, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity"))
                AiTaskRegistry.Register<AiTaskShootProjectileAtEntity>("shootatentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register<AiTaskPursueAndEngageEntity>("engageentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register<AiTaskStayCloseToHerd>("stayclosetoherd");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register<AiTaskEatDeadEntities>("eatdead");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("morale"))
                AiTaskRegistry.Register<AiTaskMorale>("morale");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("melee"))
                AiTaskRegistry.Register<AiTaskExpandedMeleeAttack>("melee");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("guard"))
                AiTaskRegistry.Register<AiTaskGuard>("guard");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("reacttoprojectiles"))
                AiTaskRegistry.Register<AiTaskReactToProjectiles>("reacttoprojectiles");
        }

        private static void RegisterAiTasksShared()
        {
            //We need to make sure we don't double register with outlaw mod, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity"))
                AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register("engageentity", typeof(AiTaskPursueAndEngageEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register("stayclosetoherd", typeof(AiTaskStayCloseToHerd));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register("eatdead", typeof(AiTaskEatDeadEntities));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("morale"))
                AiTaskRegistry.Register("morale", typeof(AiTaskMorale));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("melee"))
                AiTaskRegistry.Register("melee", typeof(AiTaskExpandedMeleeAttack));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("guard"))
                AiTaskRegistry.Register("guard", typeof(AiTaskGuard));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("reacttoprojectiles"))
                AiTaskRegistry.Register("reacttoprojectiles", typeof(AiTaskReactToProjectiles));
        }

        private static void RegisterEntityBehaviors( ICoreAPI api )
        {
            api.RegisterEntityBehaviorClass("lodrepulseagents", typeof(EntityBehaviorLODRepulseAgents));
        }
    }
}