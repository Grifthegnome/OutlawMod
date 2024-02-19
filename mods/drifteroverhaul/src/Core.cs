
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace DrifterRemovalMod
{
    public class Core : ModSystem
    {
        ICoreAPI api;

        public override double ExecuteOrder()
        {
            return 0.1;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {

            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            base.Start(api);

        }
    }
}
