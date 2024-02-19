using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BrutalStory
{
    public class BrutalParticleFloatingBlood : ParticlesProviderBase
    {
        Random rand = new Random();
        public Vec3d BasePos = new Vec3d();
        public Vec3d AddPos = new Vec3d();

        public Vec3f AddVelocity = new Vec3f();

        public float quantity;

        public int color;
        public int waterColor;

        public override EnumParticleModel ParticleModel => EnumParticleModel.Quad;
        public override bool DieInLiquid => false;
        public override bool DieInAir => true;
        public override float GravityEffect => 0f;
        public override float LifeLength => 10f;
        public override bool SwimOnLiquid => false;
        public override Vec3d Pos => new Vec3d(BasePos.X + rand.NextDouble() * AddPos.X, BasePos.Y + rand.NextDouble() * AddPos.Y, BasePos.Z + AddPos.Z * rand.NextDouble());
        public override float Quantity => quantity;

        public override float Size => 0.15f;

        public override int VertexFlags => 1 << 9; // Adds a 0.2 multiplier to the WBOIT weight so that it renders behind water
        public override EvolvingNatFloat SizeEvolve => new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1.5f);

        public override EvolvingNatFloat OpacityEvolve => new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -32);

        public BrutalParticleFloatingBlood(float quantity, int color, int waterColor, Vec3d basePos, Vec3d addPos, Vec3f addVelocity )
        {
            this.quantity = quantity;
            this.color = color;
            this.waterColor = waterColor;
            this.BasePos = basePos;
            this.AddPos = addPos;
            this.AddVelocity = addVelocity;
            //this.LifeLength = lifeLength;

        }

        public override Vec3f GetVelocity(Vec3d pos)
        {
            return new Vec3f(
                ((float)rand.NextDouble() - 0.5f) / 8f + AddVelocity.X,
                ((float)rand.NextDouble() - 0.5f) / 8f + AddVelocity.Y,
                ((float)rand.NextDouble() - 0.5f) / 8f + AddVelocity.Z
            );
        }

        public override int GetRgbaColor(ICoreClientAPI capi)
        {
            int wCol = ((waterColor & 0xff) << 16) | (waterColor & (0xff << 8)) | ((waterColor >> 16) & 0xff) | (255 << 24); // (waterColor & (0xff<<24));

            color = ColorUtil.ColorOverlay(color, wCol, 0.1f);

            return color;
        }

    }

}
