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

namespace brutalstory.src
{
    public struct fxDataPackage
    {
        private float _minQuantity;
        private float _maxQuantity;
        private int _color;
        private float _minVelocityScale;
        private float _maxVelocityScale;
        private float _lifeLength = 1f;
        private float _gravityEffect = 1f;
        private float _minSize = 1f;
        private float _maxSize = 1f;
        private EnumParticleModel _model = EnumParticleModel.Cube;

        public fxDataPackage(float minQuantity, float maxQuantity, int color, float minVelocityScale, float maxVelocityScale, float lifeLength, float gravityEffect, float minSize, float maxSize, EnumParticleModel model)
        {
            this._minQuantity = minQuantity;
            this._maxQuantity = maxQuantity;
            this._color = color;
            this._minVelocityScale = minVelocityScale;
            this._maxVelocityScale = maxVelocityScale;
            this._lifeLength = lifeLength;
            this._gravityEffect = gravityEffect;
            this._minSize = minSize;
            this._maxSize = maxSize;
            this._model = model;
        }

        public float minQuantity
        {
            get { return _minQuantity; }
        }

        public float maxQuantity
        { 
            get { return _maxQuantity; } 
        }

        public int color
        { 
            get { return _color; } 
        }

        public float minVelocityScale
        {
            get { return _minVelocityScale; }
        }

        public float maxVelocityScale
        { 
            get { return _maxVelocityScale; } 
        }

        public float lifeLength
        { 
            get { return _lifeLength; } 
        }

        public float gravityEffect
        { 
            get { return _gravityEffect; } 
        }

        public float minSize
        {
            get { return _minSize; } 
        }

        public float maxSize
        {
            get { return _maxSize; }
        }

        public EnumParticleModel model
        { 
            get { return _model; } 
        }

    }
    public static class BloodFX
    {
        static int bloodColorDefault = ColorUtil.ColorFromRgba(26, 0, 128, 255);

        static fxDataPackage bloodSprayProperties;

        public static void Init()
        {
            Vec3f minVelocity = new Vec3f(5.0f, 5.0f, 5.0f);
            Vec3f maxVelocity = new Vec3f(15.0f, 15.0f, 15.0f);
            int bloodColorCode = bloodColorDefault;

            int minQuantity = 5;
            int maxQuantity = 20;

            float minVelocityScale = 2;
            float maxVelocityScale = 8;

            float lifeLength = 10f;
            float gravityEffect = 1f;

            float minSize = 1f;
            float maxSize = 1.5f;

            bloodSprayProperties = new fxDataPackage(
                minQuantity,
                maxQuantity, 
                bloodColorCode,
                minVelocityScale,
                maxVelocityScale,
                lifeLength,
                gravityEffect,
                minSize,
                maxSize, 
                EnumParticleModel.Cube );
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

            IParticlePropertiesProvider particleEffect = (IParticlePropertiesProvider)new SimpleParticleProperties(
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

            agent.World.SpawnParticles(particleEffect, null);

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
                //if ( source )

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
