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
    public class BrutalParticleRedMist : ParticlesProviderBase
    {
        Random rand = new Random();
        public Vec3d basePos = new Vec3d();
        public Vec3d minBounds = new Vec3d();
        public Vec3d maxBounds = new Vec3d();

        public Vec3f addVelocity = new Vec3f();
        public float minVelocityScalar = 0;
        public float maxVelocityScalar = 0;

        public float minSize = 1;
        public float maxSize = 1;

        public float quantity;

        public int color;


        public override EnumParticleModel ParticleModel => EnumParticleModel.Quad;
        public override bool DieInLiquid => true;
        public override bool DieInAir => false;
        public override float GravityEffect => 0.01f;
        public override float LifeLength => 1f;
        public override bool SwimOnLiquid => false;

        public override Vec3d Pos => new Vec3d(
            basePos.X + MathUtility.GraphClampedValue(0, 1, minBounds.X, maxBounds.X, rand.NextDouble()),
            basePos.Y + MathUtility.GraphClampedValue(0, 1, minBounds.Y, maxBounds.Y, rand.NextDouble()),
            basePos.Z + MathUtility.GraphClampedValue(0, 1, minBounds.Z, maxBounds.Z, rand.NextDouble()));
        public override float Quantity => quantity;

        public override float Size => minSize + (float)rand.NextDouble() * (maxSize - minSize);

        public override int VertexFlags => 0;
        public override EvolvingNatFloat SizeEvolve => new EvolvingNatFloat(EnumTransformFunction.LINEAR, 12f);

        public override EvolvingNatFloat OpacityEvolve => new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -16);

        public BrutalParticleRedMist(float quantity, int color, Vec3d basePos, Vec3d minBounds, Vec3d maxBounds, Vec3f addVelocity, float minVelocityScalar, float maxVelocityScalar, float minSize, float maxSize)
        {
            this.quantity = quantity;
            this.color = color;
            this.basePos = basePos;
            this.minBounds = minBounds;
            this.maxBounds = maxBounds;
            this.basePos = basePos;
            this.addVelocity = addVelocity;
            this.minVelocityScalar = minVelocityScalar;
            this.maxVelocityScalar = maxVelocityScalar;
            this.minSize = 0;
            this.maxSize = 0;

            WindAffected = true;
        }

        public override Vec3f GetVelocity(Vec3d pos)
        {

            addVelocity.Normalize();
            float velScalar = (float)MathUtility.GraphClampedValue(0, 1, minVelocityScalar, maxVelocityScalar, rand.NextDouble());

            return new Vec3f(
                ((((float)rand.NextDouble() - 0.5f) / 8f) + (addVelocity.X) * velScalar),
                ((((float)rand.NextDouble() - 0.5f) / 8f) + (addVelocity.Y) * velScalar),
                ((((float)rand.NextDouble() - 0.5f) / 8f) + (addVelocity.Z) * velScalar)
            );
        }

        public override int GetRgbaColor(ICoreClientAPI capi)
        {
            return color;
        }
    }

}
