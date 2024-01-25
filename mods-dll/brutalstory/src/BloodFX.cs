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
using Vintagestory.ServerMods;
using HarmonyLib;

namespace BrutalStory
{
    public static class BloodFX
    {
        static Vec4i bloodColorDefaultBGRA = new Vec4i(26, 0, 128, 255);
        static Vec4i fleshColorDefaultBGRA = new Vec4i(26, 0, 64, 255);
        static int bloodColorDefaultCode = ColorUtil.ColorFromRgba(bloodColorDefaultBGRA.X, bloodColorDefaultBGRA.Y, bloodColorDefaultBGRA.Z, bloodColorDefaultBGRA.W);
        static int fleshColorDefaultCode = ColorUtil.ColorFromRgba(fleshColorDefaultBGRA.X, fleshColorDefaultBGRA.Y, fleshColorDefaultBGRA.Z, fleshColorDefaultBGRA.W);

        static Vec3d emptyVec3d = new Vec3d(0, 0, 0);
        static Vec3f emptyVec3f = new Vec3f(0, 0, 0);
        static int emptyColor = ColorUtil.ColorFromRgba(0,0,0,0);

        static BrutalStandardFxData bloodSprayProperties;
        static BrutalStandardFxData woundBleedProperties;
        static BrutalStandardFxData fallImpactGeyserBlood;
        static BrutalStandardFxData gibFlesh;
        static BrutalStandardFxData gibBrain;
        static BrutalStandardFxData gibBones;
        static BrutalStandardFxData gibRedMist;


        static BrutalBloodyWaterFxData bloodyWaterProperties;

        #region Client
        public static void Init()
        {
            //////////////////////////////
            //Create Bloodspray Settings//
            //////////////////////////////
            
            //This is blood that sprays from the wound.
            int bloodSprayColorCode = bloodColorDefaultCode;

            int bloodSprayMinQuantity = 5;
            int bloodSprayMaxQuantity = 20;

            float bloodSprayMinVelocityScale = 1;
            float bloodSprayMaxVelocityScale = 8;

            float bloodSprayLifeLength = 3f;
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

            ///////////////////////////////
            //Create Wound Bleed Settings//
            ///////////////////////////////
            
            //This is just blood that drips out of the wound.
            int woundBleedColorCode = bloodColorDefaultCode;

            int woundBleedMinQuantity = 3;
            int woundBleedMaxQuantity = 8;

            float woundBleedMinVelocityScale = 0f;
            float woundBleedMaxVelocityScale = 0.1f;

            float woundBleedLifeLength = 3f;
            float woundBleedGravityEffect = 1.0f;

            float woundBleedMinSize = 1f;
            float woundBleedMaxSize = 1.5f;

            woundBleedProperties = new BrutalStandardFxData(
                woundBleedMinQuantity,
                woundBleedMaxQuantity,
                woundBleedColorCode,
                woundBleedMinVelocityScale,
                woundBleedMaxVelocityScale,
                woundBleedLifeLength,
                woundBleedGravityEffect,
                woundBleedMinSize,
                woundBleedMaxSize,
                EnumParticleModel.Cube);

            ////////////////////////////////
            //Create Bloody Water Settings//
            ////////////////////////////////
            
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

            ////////////////////////////////
            //Create Blood Geyser Settings//
            ////////////////////////////////

            //This is blood that sprays into the air when a creature hits the ground.
            int geyserBloodColorCode = bloodColorDefaultCode;

            int geyserBloodMinQuantity = 5;
            int geyserBloodMaxQuantity = 20;

            float geyserBloodMinVelocityScale = 8;
            float geyserBloodMaxVelocityScale = 30;

            float geyserBloodLifeLength = 10f;
            float geyserBloodGravityEffect = 1f;

            float geyserBloodMinSize = 1f;
            float geyserBloodMaxSize = 1.5f;

            fallImpactGeyserBlood = new BrutalStandardFxData(
                geyserBloodMinQuantity,
                geyserBloodMaxQuantity,
                geyserBloodColorCode,
                geyserBloodMinVelocityScale,
                geyserBloodMaxVelocityScale,
                geyserBloodLifeLength,
                geyserBloodGravityEffect,
                geyserBloodMinSize,
                geyserBloodMaxSize,
                EnumParticleModel.Cube);

            ///////////////////////
            //Create Gib Settings//
            ///////////////////////

            //This is flesh chunks that spray into the air when a creature hits the ground.
            int gibFleshColorCode = fleshColorDefaultCode;

            int gibFleshMinQuantity = 5;
            int gibFleshMaxQuantity = 20;

            float gibFleshMinVelocityScale = 1;
            float gibFleshMaxVelocityScale = 8;

            float gibFleshLifeLength = 10f;
            float gibFleshGravityEffect = 1f;

            float gibFleshMinSize = 2f;
            float gibFleshMaxSize = 3.5f;

            gibFlesh = new BrutalStandardFxData(
                gibFleshMinQuantity,
                gibFleshMaxQuantity,
                gibFleshColorCode,
                gibFleshMinVelocityScale,
                gibFleshMaxVelocityScale,
                gibFleshLifeLength,
                gibFleshGravityEffect,
                gibFleshMinSize,
                gibFleshMaxSize,
                EnumParticleModel.Cube);

            //static BrutalStandardFxData gibBrain;
            //static BrutalStandardFxData gibBones;

            //This is blood mist that hangs in the air when a creature hits the ground.
            int gibRedMistColorCode = ColorUtil.ColorFromRgba(bloodColorDefaultBGRA.X, bloodColorDefaultBGRA.Y, bloodColorDefaultBGRA.Z, 100);

            int gibRedMistMinQuantity = 4;
            int gibRedMistMaxQuantity = 6;

            float gibRedMistMinVelocityScale = 0.25f;
            float gibRedMistMaxVelocityScale = 0.5f;

            float gibRedMistLifeLength = 3f;
            float gibRedMistGravityEffect = 1.0f;

            float gibRedMistMinSize = 0.5f;
            float gibRedMistMaxSize = 2.0f;

            gibRedMist = new BrutalStandardFxData(
                gibRedMistMinQuantity,
                gibRedMistMaxQuantity,
                gibRedMistColorCode,
                gibRedMistMinVelocityScale,
                gibRedMistMaxVelocityScale,
                gibRedMistLifeLength,
                gibRedMistGravityEffect,
                gibRedMistMinSize,
                gibRedMistMaxSize,
                EnumParticleModel.Quad);
        }

        public static void Bleed( EntityAgent agent, DamageSource damageSource, float damage, Vec3d serverPos )
        {
            Debug.Assert(agent.Api.Side == EnumAppSide.Client, "Bleed is being called on the server, this should never happen!");
            
            if (!ShouldPlayBloodFX(agent, damageSource))
                return;

            SpawnBloodFXForDamage(agent, damageSource, damage, serverPos);
        }
    
        private static void SpawnBloodFXForDamage(EntityAgent agent, DamageSource damageSource, float damage, Vec3d serverPos )
        {
            if ( damageSource.Type == EnumDamageType.Poison )
            {
                //To Do: have the player barf blood.
                return;
            }

            if ( agent.FeetInLiquid ) 
            {
                CreateBloodyWaterFX(agent, damageSource, damage);
            }
            
            if ( !agent.Swimming ) 
            { 
                if ( damageSource.Type == EnumDamageType.Gravity )
                {
                    ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");
                    Debug.Assert(treeAttribute != null);
                    float maxHealth = treeAttribute.GetFloat("maxhealth");

                    if ( damage > maxHealth )
                    {
                        CreateBloodImpactGeyser(agent, damageSource, damage, serverPos);
                        GibEntityAgent_Client(agent, damageSource, damage, serverPos);
                    }
                }
                else
                {
                    CreateBloodSpray(agent, damageSource, damage);
                    CreateWoundBleed(agent, damageSource, damage);
                }
            }
        }

        private static void CreateBloodSpray( EntityAgent agent, DamageSource damageSource, float damage) 
        {
            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, bloodSprayProperties.minQuantity, bloodSprayProperties.maxQuantity, ((double)damage));

            Vec3d hitPos = GetHitPositionLocalPosition(agent, damageSource);

            Vec3d minPos = agent.Pos.XYZ + hitPos;
            Vec3d maxPos = agent.Pos.XYZ + hitPos;

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

            bloodSprayParticleEffect.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -bloodSprayProperties.lifeLength);
            bloodSprayParticleEffect.ShouldDieInLiquid = true;

            agent.World.SpawnParticles(bloodSprayParticleEffect, null);
        }

        private static Vec3d minWoundOffsetVec = new Vec3d(0.05, 0.05, 0.05);
        private static Vec3d maxWoundOffsetVec = new Vec3d(-0.05, -0.05, -0.05);
        private static void CreateWoundBleed(EntityAgent agent, DamageSource damageSource, float damage)
        {
            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, woundBleedProperties.minQuantity, woundBleedProperties.maxQuantity, ((double)damage));

            Vec3d hitPos = GetHitPositionLocalPosition(agent, damageSource);

            Vec3d minPos = agent.Pos.XYZ + (hitPos + minWoundOffsetVec);
            Vec3d maxPos = agent.Pos.XYZ + (hitPos + maxWoundOffsetVec);

            Entity attacker = damageSource.GetCauseEntity();

            Vec3f hitDir = GetHitDirection(agent, damageSource);

            Vec3f minVelocity = (hitDir * woundBleedProperties.minVelocityScale);
            Vec3f maxVelocity = (hitDir * woundBleedProperties.maxVelocityScale);

            SimpleParticleProperties woundBleedParticleEffect = new SimpleParticleProperties(
                quantity,
                quantity,
                woundBleedProperties.color,
                minPos,
                maxPos,
                minVelocity *= -1,
                maxVelocity *= -1,
                woundBleedProperties.lifeLength,
                woundBleedProperties.gravityEffect,
                woundBleedProperties.minSize,
                woundBleedProperties.maxSize,
                woundBleedProperties.model);

            woundBleedParticleEffect.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -woundBleedProperties.lifeLength);
            woundBleedParticleEffect.ShouldDieInLiquid = true;

            agent.World.SpawnParticles(woundBleedParticleEffect, null);
        }

        private static Vec3d feetInWaterOffsetVec = new Vec3d(0, 0.5, 0);
        private static void CreateBloodyWaterFX(EntityAgent agent, DamageSource damageSource, float damage)
        {        
            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, bloodyWaterProperties.minQuantity, bloodyWaterProperties.maxQuantity, ((double)damage));

            Vec3d hitPos = GetHitPositionLocalPosition(agent, damageSource);

            Vec3d pos = agent.Swimming ? agent.Pos.XYZ + hitPos : agent.Pos.XYZ + feetInWaterOffsetVec;

            Block liquidBlock = agent.World.BlockAccessor.GetBlock(pos.AsBlockPos);
            //Debug.Assert(liquidBlock.BlockMaterial == EnumBlockMaterial.Liquid);
            int waterColor = liquidBlock.GetColor(BrutalBroadcast.clientCoreApi, pos.AsBlockPos);

            //Bloody Water Particles
            BrutalParticleFloatingBlood bloodyWaterParticleEffect = new BrutalParticleFloatingBlood(
                quantity,
                bloodyWaterProperties.color,
                waterColor,
                pos,
                bloodyWaterProperties.addPos,
                bloodyWaterProperties.addVelocity
                );

            agent.World.SpawnParticles(bloodyWaterParticleEffect, null);
        }

        private static void CreateBloodImpactGeyser(EntityAgent agent, DamageSource damageSource, float damage, Vec3d serverPos )
        {
            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, fallImpactGeyserBlood.minQuantity, fallImpactGeyserBlood.maxQuantity, ((double)damage));

            //We are setting the hit position to the server agent foot position in our broadcast func.
            Vec3d hitPos = GetHitPositionWorldPosition(agent, damageSource, serverPos);

            //Fire at a random upward trajectory
            float xRand = agent.World.Rand.NextDouble() > 0.5 ? (float)(agent.World.Rand.NextDouble() * 0.05) : (float)(agent.World.Rand.NextDouble() * -0.05);
            float yRand = agent.World.Rand.NextDouble() > 0.5 ? (float)(agent.World.Rand.NextDouble() * 0.05) : (float)(agent.World.Rand.NextDouble() * -0.05);
            Vec3f hitDir = new Vec3f(xRand, upwardHitDir.Y, yRand);

            Vec3f minVelocity = hitDir * fallImpactGeyserBlood.minVelocityScale;
            Vec3f maxVelocity = hitDir * (float)MathUtility.GraphClampedValue(0, maxHealth * 2, fallImpactGeyserBlood.minVelocityScale, fallImpactGeyserBlood.maxVelocityScale, ((double)damage));

            SimpleParticleProperties bloodGeyserParticleEffect = new SimpleParticleProperties(
                quantity,
                quantity,
                fallImpactGeyserBlood.color,
                hitPos,
                hitPos,
                minVelocity *= -1,
                maxVelocity *= -1,
                fallImpactGeyserBlood.lifeLength,
                fallImpactGeyserBlood.gravityEffect,
                fallImpactGeyserBlood.minSize,
                fallImpactGeyserBlood.maxSize,
                fallImpactGeyserBlood.model);

            bloodGeyserParticleEffect.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -(fallImpactGeyserBlood.lifeLength * 0.25f ));
            bloodGeyserParticleEffect.WindAffected = true;
            bloodGeyserParticleEffect.WindAffectednes = 1.0f;
            bloodGeyserParticleEffect.ShouldDieInLiquid = true;

            agent.World.SpawnParticles(bloodGeyserParticleEffect, null);
        }

        private static void GibEntityAgent_Client(EntityAgent agent, DamageSource damageSource, float damage, Vec3d serverPos)
        {
            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            //Scale Amount of blood by damage compared to max health.
            int quantity = (int)MathUtility.GraphClampedValue(0, maxHealth, gibFlesh.minQuantity, gibFlesh.maxQuantity, ((double)damage));

            // agent.SelectionBox

            EntityProperties agentProperties = agent.World.GetEntityType(agent.Code);

            //Hande the case where the entry is invalid, or has been disabled by mod flags.
            if (agentProperties == null)
                return;

            //To do: Make quantity of gibs based on hitbox of agent.
            Cuboidf collisionBox = agentProperties.SpawnCollisionBox;

            //We are setting the hit position to the server agent foot position in our broadcast func.
            Vec3d hitPos = GetHitPositionWorldPosition(agent, damageSource, serverPos);

            Vec3d collStartFlat = collisionBox.Startd.Clone();
            collStartFlat.Y = 0;

            Vec3d collEndFlat = collisionBox.Endd.Clone();
            collEndFlat.Y = 0;

            //To do: Base this on damage direction, so we can use it for falls or really powerful attacks.

            Vec3f hitDir = GetHitDirection(agent, damageSource);

            BrutalParticleFleshSplat gibFleshParticleEffect = new BrutalParticleFleshSplat(
                quantity,
                gibFlesh.color,
                hitPos + (collStartFlat * gibFlesh.maxSize) ,
                collEndFlat * gibFlesh.maxSize,
                hitDir *= -1,
                gibFlesh.minVelocityScale,
                gibFlesh.maxVelocityScale,
                gibFlesh.minSize,
                gibFlesh.maxSize);

            agent.World.SpawnParticles(gibFleshParticleEffect, null);

            //Create Blood Mist

            Vec3f redmistMinVelocity = hitDir * gibRedMist.minVelocityScale;
            Vec3f redmistMaxVelocity = hitDir * gibRedMist.maxVelocityScale;

            BrutalParticleRedMist gibRedMistParticleEffect = new BrutalParticleRedMist(
                gibRedMist.maxQuantity,
                gibRedMist.color,
                hitPos,
                collisionBox.Startd * 1.5,
                collisionBox.Endd * 1.5,
                hitDir,
                gibRedMist.minVelocityScale,
                gibRedMist.maxVelocityScale,
                gibRedMist.minSize,
                gibRedMist.maxSize
                );

            agent.World.SpawnParticles(gibRedMistParticleEffect, null);
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
                    float xRand = agent.World.Rand.NextDouble() > 0.5 ? (float)(agent.World.Rand.NextDouble() * 0.05) : (float)(agent.World.Rand.NextDouble() * -0.05);
                    float yRand = agent.World.Rand.NextDouble() > 0.5 ? (float)(agent.World.Rand.NextDouble() * 0.05) : (float)(agent.World.Rand.NextDouble() * -0.05);
                    Vec3f upSplatterVec = new Vec3f(xRand, upwardHitDir.Y, yRand);
                    return upSplatterVec; 
                }
            }

            if (attacker != null)
            {
                if (attacker is EntityAgent)
                {
                    Vec3d hitPos = GetHitPositionLocalPosition( agent, damageSource );
                    hitDir = ((agent.Pos.XYZ + hitPos) - (attacker.Pos.XYZ + attacker.LocalEyePos)).ToVec3f().Normalize();
                }
            }

            return hitDir;
        }

        private static Vec3d GetHitPositionLocalPosition(  EntityAgent agent, DamageSource damageSource )
        {
            return damageSource.HitPosition != null ? damageSource.HitPosition : agent.LocalEyePos * 0.75;
        }

        private static Vec3d GetHitPositionWorldPosition( EntityAgent agent, DamageSource damageSource, Vec3d serverPos )
        {
            return damageSource.HitPosition != null ? (serverPos + damageSource.HitPosition) : (serverPos + agent.LocalEyePos * 0.75);
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
                    case EnumDamageType.Poison:
                        return true;

                }
            }

            return false;
        }
        #endregion //Client

        #region Server

        public static void HandleBrutalDamage_Server(EntityAgent agent, DamageSource damageSource, float damage)
        {
            if (!agent.Swimming)
            {
                if (damageSource.Type == EnumDamageType.Gravity)
                {
                    GibEntityAgent_Server(agent, damageSource, damage);
                }
            }
        }

        private static void GibEntityAgent_Server(EntityAgent agent, DamageSource damageSource, float damage)
        {
            if (!(ShouldPlayBloodFX(agent, damageSource)))
                return;

            ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");
            Debug.Assert(treeAttribute != null);
            float maxHealth = treeAttribute.GetFloat("maxhealth");

            if ( damage > maxHealth )
            {
                //To Do: Spawn harvestable items on server, if the entity is harvestable.
                if ( agent.HasBehavior("harvestable") )
                {
                    EntityBehaviorHarvestable behaviorHarvestable = (EntityBehaviorHarvestable)agent.GetBehavior("harvestable");
                    //behaviorHarvestable

                    //We need to look at generate drops and reading the drops directily from the json.

                }

                if (!(agent is EntityPlayer))
                    agent.Die(EnumDespawnReason.Removed);
            }            
        }

        #endregion //Server

    }




}
