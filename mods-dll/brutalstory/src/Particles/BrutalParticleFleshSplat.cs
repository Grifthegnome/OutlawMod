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
    public class BrutalParticleFleshSplat : ParticlesProviderBase
    {
        Random rand = new Random();
        public Vec3d basePos = new Vec3d();
        public Vec3d addPos = new Vec3d();

        public Vec3f addVelocity = new Vec3f();
        public float minVelocityScalar = 0;
        public float maxVelocityScalar = 0;

        public float minSize = 1;
        public float maxSize = 1;

        public float quantity;

        public int color;


        public override EnumParticleModel ParticleModel => EnumParticleModel.Cube;
        public override bool DieInLiquid => true;
        public override bool DieInAir => false;
        public override float GravityEffect => 1.0f;
        public override float LifeLength => 10f;
        public override bool SwimOnLiquid => true;
        public override Vec3d Pos => new Vec3d(basePos.X + rand.NextDouble() * addPos.X, basePos.Y + rand.NextDouble() * addPos.Y, basePos.Z + addPos.Z * rand.NextDouble());
        public override float Quantity => quantity;

        public override float Size => minSize + (float)rand.NextDouble() * (maxSize - minSize);

        public override int VertexFlags => 0;

        public BrutalParticleFleshSplat(float quantity, int color, Vec3d basePos, Vec3d addPos, Vec3f addVelocity, float minVelocityScalar, float maxVelocityScalar, float minSize, float maxSize )
        {
            this.quantity = quantity;
            this.color = color;
            this.basePos = basePos;
            this.addPos = addPos;
            this.addVelocity = addVelocity;
            this.minVelocityScalar = minVelocityScalar;
            this.maxVelocityScalar = maxVelocityScalar;
            this.minSize = minSize;
            this.maxSize = maxSize;

            Bounciness = 0.1f;
        }

        public override Vec3f GetVelocity(Vec3d pos)
        {
            minVelocityScalar *= 0.75f;
            maxVelocityScalar *= 0.75f;

            float velScalar = (float)MathUtility.GraphClampedValue(0, 1, minVelocityScalar, maxVelocityScalar, rand.NextDouble());

            return new Vec3f(
                ((float)rand.NextDouble() - 0.5f) / 0.1f + (addVelocity.X * velScalar),
                ((float)rand.NextDouble() - 0.5f) / 0.1f + (addVelocity.Y),
                ((float)rand.NextDouble() - 0.5f) / 0.1f + (addVelocity.Z * velScalar)
            );
        }

        public override int GetRgbaColor(ICoreClientAPI capi)
        {
            return color;
        }

    }

}
