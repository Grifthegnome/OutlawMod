using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;


namespace EvilBelow
{
    public class BlockEntityEBSpawnBlocker : BlockEntity, IEBSpawnBlocker
    {
        //We need to be able to save and load all BlockEntityOutlawDeterents in the world so that we can check for them when outlaws attempt to spawn.
        //We need a living list of these blocks so that we don't have a need to save them at runtime.
        //This requires saving out the list to world save data and rebuilding it on server load.

        public Vec3d Position => Pos.ToVec3d();
        public string Type => "ebSpawnBlocker";

        public float blockingRange()
        {
            if (Block.Attributes?["ebSpawnBlockRadius"].Exists == true)
                return Block.Attributes["ebSpawnBlockRadius"].AsFloat();

           return 0f;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            //Register this entity as a Point of Intrest so we can find it easily.
            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }
    }
}