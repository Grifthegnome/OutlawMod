
using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TrailMod
{

    public class TrailModCore : ModSystem
    {

        private Harmony harmony;

        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            harmony = new Harmony("com.grifthegnome.trailmod.trailpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }
    }
}
