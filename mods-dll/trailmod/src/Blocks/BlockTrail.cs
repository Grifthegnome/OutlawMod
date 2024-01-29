using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TrailMod
{

    public class BlockTrail : Block
    {
        private const double PRETRAIL_DEVOLVE_TIME_MS = 90000; //after 15 minutes a pretrail block devolves back into a regular soil block.
        private const double TRAIL_DEVOLVE_TIME_MS = 14400000; //after 4 hours a trail devolves one level.
        private const string SOIL_CODE = "soil";
        private const string SOIL_GRASS_CODE = "none";

        private readonly string[] trailVariants = { "pretrail", "new", "established", "veryestablished", "old" };

        //To Do: Devolve Trails Over Time
        double lastTrailTouchTime = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            base.OnServerGameTick(world, pos, extra);

            string endVariant = this.Code.EndVariant();

            if ( world.ElapsedMilliseconds - lastTrailTouchTime >= GetTrailDevolveTime(endVariant) )
            {
                if ( endVariant == "pretrail" )
                {
                    string fertilityVariantCode = this.Code.SecondCodePart();

                    string devolveToSoilCode =  SOIL_CODE + "-" + fertilityVariantCode + "-" + SOIL_GRASS_CODE;

                    AssetLocation devolveSoilBlockAsset = new AssetLocation(devolveToSoilCode);

                    Block devolveSoilBlock = world.GetBlock(devolveSoilBlockAsset);

                    Debug.Assert( devolveSoilBlock != null );

                    lastTrailTouchTime = world.ElapsedMilliseconds;
                    world.BlockAccessor.SetBlock(devolveSoilBlock.Id, pos);
                }
                else
                {
                    //Devolve the block to the previous level.
                    int wearVariantID = GetTrailWearIDFromWearCode(endVariant);

                    string baseCode = this.CodeWithoutParts(1);
                    string newWearVariantCode = trailVariants[wearVariantID - 1];
                    string devolveBlockCode = baseCode + "-" + newWearVariantCode;

                    AssetLocation devolveBlockAsset = new AssetLocation(this.Code.ShortDomain() + ":" + devolveBlockCode);
                    Block devolveBlock = world.GetBlock(devolveBlockAsset);

                    Debug.Assert( devolveBlock != null );

                    lastTrailTouchTime = world.ElapsedMilliseconds;
                    world.BlockAccessor.SetBlock(devolveBlock.Id, pos);
                }
            }

        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            return true; //base.ShouldReceiveServerGameTicks(world, pos, offThreadRandom, out extra);
        }

        private double GetTrailDevolveTime( string wearVariant )
        {
            if ( wearVariant == "pretrail")
                return PRETRAIL_DEVOLVE_TIME_MS;

            return TRAIL_DEVOLVE_TIME_MS;
        }

        private int GetTrailWearIDFromWearCode( string wearCode )
        {
            switch( wearCode )
            {
                case "pretrail":
                    return 0;
                case "new":
                    return 1;
                case "established":
                    return 2;
                case "veryestablished":
                    return 3;
                case "old":
                    return 4;
            }

            Debug.Assert(false, "Wear code is invalid.");

            return -1;
        }

        public void TrailBlockTouched( double touchTime )
        {
            lastTrailTouchTime = touchTime;
        }
    }
}
