using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace OutlawMod
{

    public class EntityOutlaw: EntityHumanoid
    {

        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "formal", new TraderPersonality(1, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f, 1f, 1.8f) },
        };

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "rowdy"); }
            set
            {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].ChorldDelayMul, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);

            }
        }

        public EntityTalkUtil talkUtil;

        //Look at Entity.cs in the VAPI project for more functions you can override.

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            //Assign Personality for Classic Voice
            if ( World.Side == EnumAppSide.Client )
            {
                this.talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this);

                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));

                AssetLocation voiceSound = new AssetLocation(properties.Attributes?["classicVoice"].ToString());
                
                if (voiceSound != null)
                    talkUtil.soundName = voiceSound;

                this.Personality = this.Personality; // to update the talkutil
            }
        }

        /// <summary>
        /// Called when after the got loaded from the savegame (not called during spawn)
        /// </summary>
        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
        }

        /// <summary>
        /// Called when the entity spawns (not called when loaded from the savegame).
        /// </summary>
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if ( !OMGlobalConstants.devMode && Api.Side == EnumAppSide.Server)
            {
                //Check if the Outlaw is blocked by spawn rules.
                if ( !OutlawSpawnEvaluator.CanSpawnOutlaw( Pos.XYZ, this.Code ) )
                {
                    Utility.DebugLogToPlayerChat(Api as ICoreServerAPI, "Cannot Spawn " + this.Code.Path + " at: " + this.Pos + ". See Debug Log for Details.");
                    this.Die(EnumDespawnReason.Removed, null);
                    
                    return;
                }
            }
        }


        /// <summary>
        /// Called when the entity despawns
        /// </summary>
        /// <param name="despawn"></param>
        public override void OnEntityDespawn(EntityDespawnData despawnData )
        {
            base.OnEntityDespawn( despawnData );
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Client && OMGlobalConstants.outlawsUseClassicVintageStoryVoices )
            {
                talkUtil.OnGameTick(dt);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (talkUtil == null)
                return;

            switch ( packetid )
            {
                
                case 1001: //hurt

                    if (!Alive) 
                        return;
                        
                    talkUtil.Talk(EnumTalkType.Hurt);

                    break;
                
                case 1002: //Death

                    talkUtil.Talk(EnumTalkType.Death);

                    break;

                case 1003: //Melee Attack, Shoot at Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Complain);

                    break;

                case 1004: //Flee Entity, Morale Route

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Hurt2);

                    break;

                case 1005: //Seek Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Laugh);

                    break;

                case 1006: //Engage Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Laugh);

                    break;

            }
            
        }

        public override void PlayEntitySound(string type, IPlayer dualCallByPlayer = null, bool randomizePitch = true, float range = 24)
        {
            //If the config says use classic vintage story voices, use instrument voices.
            if ( OMGlobalConstants.outlawsUseClassicVintageStoryVoices )
            {
                if (World.Side == EnumAppSide.Server)
                {
                    switch (type)
                    {
                        case "hurt":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1001);
                            return;

                        case "death":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1002);
                            return;

                        case "meleeattack":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1003);
                            return;

                        case "melee":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1003);
                            return;

                        case "shootatentity":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1003);
                            return;

                        case "fleeentity":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1004);
                            return;

                        case "morale":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1004);
                            return;

                        case "seekentity":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1005);
                            return;
                        case "engageentity":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1006);
                            return;


                    }
                }
                else if (World.Side == EnumAppSide.Client)
                {
                    //Certain sound events originate on the client.
                    switch (type)
                    {
                        case "idle":
                            talkUtil.Talk(EnumTalkType.Idle);
                            return;
                    }
                }
            }
            else
            {
                //Otherwise use Outlaw Mod VO Lines.
                base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
            }            
        }
    }
}