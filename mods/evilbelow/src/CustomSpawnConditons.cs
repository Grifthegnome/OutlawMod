
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace EvilBelow
{

    public class CustomSpawnConditions : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
            sapi.Event.OnEntityDespawn += Event_OnEntityDespawn;
        }

        private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {

            if (EBGlobalConstants.devMode)
            {
                if (properties.Code.Path.StartsWithFast("drifter"))
                    return true;

                if (properties.Code.Path.StartsWithFast("butterfly"))
                    return true;

                string message = "[Evil Below Mod Debug] Attempting to spawn entity " + properties.Code.Path;
                sapi.Logger.Debug(message);
            }

            string type = properties.Code.FirstPathPart();

            //This may be a good location to spawn things that have to spawn in specific locations or on specific block materials.
            switch (type)
            {
                case "NONE":
                    return ShouldSpawnOutlawOfType(ref properties, spawnPosition);
                    
            }

            return true;
        }

        private void Event_OnEntityDespawn(Entity ent, EntityDespawnData despawnData)
        {
            if (EBGlobalConstants.devMode)
            {
                string type = ent.Code.FirstPathPart();

                //This is here so we can debug despawning.
                switch (type)
                {
                    case "NONE":
                        string message = "[Evil Below Mod Debug] Despawning entity " + ent.Code.Path;
                        sapi.Logger.Debug(message);
                        break;
                }
            }
        }

        private bool ShouldSpawnOutlawOfType( ref EntityProperties properties, Vec3d spawnPosition )
        {
            //Check spawn rules.
            return EBSpawnEvaluator.CanSpawnEBCreature(spawnPosition, properties.Code);
        }
    }
}
