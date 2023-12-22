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

namespace OutlawMod
{

    ////////////////////////////////////////////////////////////////
    /// PATCHING HARVESTABLE BEHAVIOR FOR CAUSE OF DEATH EVIDENCE///
    ////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(EntityBehaviorHarvestable))]
    public class BehaviorHarvestableOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            if (original != null)
            {

                foreach (var patched in harmony.GetPatchedMethods() )
                {
                    if (patched.Name == original.Name)
                        return false;
                }
            }

            return true;
        }


        [HarmonyPatch("GetInfoText")]
        [HarmonyPrefix]
        static bool IgnoreAndOverrideGetInfoText(EntityBehaviorHarvestable __instance, StringBuilder infotext)
        {

            if (!__instance.entity.Alive)
            {
                //A work around we have to use because the GotCrushed class member variable uses getter and setter functions we can't access from here. 
                bool gotCrushed = (__instance.entity.WatchedAttributes.HasAttribute("deathReason") && (EnumDamageSource)__instance.entity.WatchedAttributes.GetInt("deathReason") == EnumDamageSource.Fall) ||
                    (__instance.entity.WatchedAttributes.HasAttribute("deathDamageType") && (EnumDamageType)__instance.entity.WatchedAttributes.GetInt("deathDamageType") == EnumDamageType.Crushing);

                if (gotCrushed)
                {
                    infotext.AppendLine(Lang.Get("Looks crushed. Won't be able to harvest as much from this carcass."));
                }

                string deathByEntityLangCode = __instance.entity.WatchedAttributes.GetString("deathByEntityLangCode");

                if (deathByEntityLangCode != null && !__instance.entity.WatchedAttributes.HasAttribute("deathByPlayer"))
                {
                    string killerLangCode = deathByEntityLangCode;
                    string killerName = "";

                    if (killerLangCode.Contains("prefixandcreature-"))
                    {
                        //todo: don't hard code this number, it's dumb and bad.
                        killerName = killerLangCode.Substring(18);
                    }

                    if (Lang.HasTranslation("deadcreature-eaten-" + killerName))
                    {
                        infotext.AppendLine(Lang.Get("deadcreature-eaten-" + killerName));
                    }
                    else
                    {
                        if (deathByEntityLangCode.Contains("wolf"))
                        {
                            infotext.AppendLine(Lang.Get("deadcreature-eaten-wolf"));
                        }
                        else
                        {
                            infotext.AppendLine(Lang.Get("deadcreature-eaten"));
                        }
                    }
                }
            }

            //A work around we have to use because the AnimalWeight class member variable uses getter and setter functions we can't access from here. 
            float animalWeight = __instance.entity.WatchedAttributes.GetFloat("animalWeight", 1);

            if (animalWeight >= 0.95f)
            {
                infotext.AppendLine(Lang.Get("creature-weight-good"));
            }
            else if (animalWeight >= 0.75f)
            {
                infotext.AppendLine(Lang.Get("creature-weight-ok"));
            }
            else if (animalWeight >= 0.5f)
            {
                infotext.AppendLine(Lang.Get("creature-weight-low"));
            }
            else
            {
                infotext.AppendLine(Lang.Get("creature-weight-starving"));
            }

            return false;
        }
    }

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
}