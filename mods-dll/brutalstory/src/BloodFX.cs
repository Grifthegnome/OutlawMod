using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Drawing;
using BrutalStory;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace BrutalStory
{
    public static class BloodFX
    {
        static Vec4i bloodColorDefaultBGRA = new Vec4i(26, 0, 128, 255);
        static int bloodColorDefaultCode = ColorUtil.ColorFromRgba(bloodColorDefaultBGRA.X, bloodColorDefaultBGRA.Y, bloodColorDefaultBGRA.Z, bloodColorDefaultBGRA.W);

        static Vec3d emptyVec3d = new Vec3d(0, 0, 0);
        static Vec3f emptyVec3f = new Vec3f(0, 0, 0);
        static int emptyColor = ColorUtil.ColorFromRgba(0,0,0,0);

        static BrutalStandardFxData bloodSprayProperties;
        static BrutalBloodyWaterFxData bloodyWaterProperties;

        public static void Init()
        {
            //Create Bloodspray Settings
            Vec3f bloodSprayMinVel = new Vec3f(5.0f, 5.0f, 5.0f);
            Vec3f bloodSprayMaxVel = new Vec3f(15.0f, 15.0f, 15.0f);
            int bloodSprayColorCode = bloodColorDefaultCode;

            int bloodSprayMinQuantity = 5;
            int bloodSprayMaxQuantity = 20;

            float bloodSprayMinVelocityScale = 2;
            float bloodSprayMaxVelocityScale = 8;

            float bloodSprayLifeLength = 10f;
            float bloodSprayGravityEffect = 1f;

            float bloodSprayMinSize = 1f;
            float bloodSprayMaxSize = 1.5f;

            bloodSprayProperties = new BrutalStandardFxData(
                bloodSprayMinQuantity,
                bloodSprayMaxQuantity, 
                bloodSprayColorCode,
                bloodSprayMinVelocityScale,
                bloodSprayMaxVelocityScale,
                bloodSprayLifeLength,
                bloodSprayGravityEffect,
                bloodSprayMinSize,
                bloodSprayMaxSize, 
                EnumParticleModel.Cube );


            //Create Bloody Water Settings.
            int bloodyWaterMinQuantity = 5;
            int bloodyWaterMaxQuantity = 20;
            int bloodyWaterColorCode = ColorUtil.ColorFromRgba(bloodColorDefaultBGRA.X, bloodColorDefaultBGRA.Y, bloodColorDefaultBGRA.Z, 191);
            Vec3d bloodyWaterAddPos = emptyVec3d;
            Vec3f bloodyWaterAddVel = emptyVec3f;


            bloodyWaterProperties = new BrutalBloodyWaterFxData(
                bloodyWaterMinQuantity,
                bloodyWaterMaxQuantity,
                bloodyWaterColorCode,
                bloodyWaterAddPos,
                bloodyWaterAddVel
                );

        }

        public static void Bleed( EntityAgent agent, DamageSource damageSource, float damage)
        {
            Debug.Assert(agent.Api.Side == EnumAppSide.Client, "Bleed is being called on the server, this should never happen!");

            if (!ShouldPlayBloodFX(agent, damageSource))
                return;

            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, bloodSprayProperties.minQuantity, bloodSprayProperties.maxQuantity, ((double)damage));

            Vec3d hitPos = GetHitPosition(agent, damageSource);

            Vec3d minPos = agent.Pos.XYZ.Add(hitPos);
            Vec3d maxPos = agent.Pos.XYZ.Add(hitPos);

            Entity attacker = damageSource.GetCauseEntity();

            Vec3f hitDir = GetHitDirection(agent, damageSource);

            Vec3f minVelocity = hitDir * bloodSprayProperties.minVelocityScale;
            Vec3f maxVelocity = hitDir * bloodSprayProperties.maxVelocityScale;

            SimpleParticleProperties bloodSprayParticleEffect = new SimpleParticleProperties(
                quantity,
                quantity, 
                bloodSprayProperties.color,
                minPos,
                maxPos,
                minVelocity *= -1,
                maxVelocity *= -1,
                bloodSprayProperties.lifeLength,
                bloodSprayProperties.gravityEffect,
                bloodSprayProperties.minSize,
                bloodSprayProperties.maxSize,
                bloodSprayProperties.model);

            bloodSprayParticleEffect.ShouldDieInLiquid = true;

            agent.World.SpawnParticles(bloodSprayParticleEffect, null);

            //Test Bloody Water Particles
            BrutalFloatingBloodParticles bloodyWaterParticleEffect = new BrutalFloatingBloodParticles(
                bloodyWaterProperties.minQuantity,
                bloodyWaterProperties.color,
                emptyColor,
                minPos,
                bloodyWaterProperties.addPos,
                bloodyWaterProperties.addVelocity
                );

            agent.World.SpawnParticles(bloodyWaterParticleEffect, null);

        }
    
        private static Vec3f emptyHitDir = new Vec3f(0f, 0f, 0f);
        private static Vec3f upwardHitDir = new Vec3f(0f, -1f, 0f);
        private static Vec3f GetHitDirection( EntityAgent agent, DamageSource damageSource )
        {
            Vec3f hitDir = emptyHitDir;

            Entity source = damageSource.SourceEntity;
            Entity attacker = damageSource.GetCauseEntity();

            switch( damageSource.Type )
            {
                //Fall and Splat, do a blood geiser.
                case EnumDamageType.Gravity:
                {
                    return upwardHitDir; 
                }
            }

            if (attacker != null)
            {
                if (attacker is EntityAgent)
                {
                    Vec3d hitPos = GetHitPosition( agent, damageSource );
                    hitDir = ( agent.Pos.XYZ.Add(hitPos) - attacker.Pos.XYZ.Add(attacker.LocalEyePos) ).ToVec3f().Normalize();
                }
            }

            return hitDir;
        }

        private static Vec3d GetHitPosition(  EntityAgent agent, DamageSource damageSource )
        {
            return damageSource.HitPosition != null ? damageSource.HitPosition : agent.LocalEyePos.Mul(0.75);
        }

        private static bool ShouldPlayBloodFX( EntityAgent agent, DamageSource damageSource )
        {
            if ( agent.WatchedAttributes.HasAttribute("health") )
            {
                //Add specal case for poison where we make players barf blood >:)
                switch (damageSource.Type)
                {
                    //Fall and Splat
                    case EnumDamageType.Gravity:
                    case EnumDamageType.PiercingAttack:
                    case EnumDamageType.Injury:
                    case EnumDamageType.BluntAttack:
                    case EnumDamageType.Crushing:
                    case EnumDamageType.SlashingAttack:
                        return true;

                }
            }

            return false;
        }
    }

    
}
