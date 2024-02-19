using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IdleKick
{
    [ProtoContract]
    public class IdleKickConfig
    {
        [ProtoMember(1)]
        public int maxMillisecondsIdle = 600000;

    }
    public class IdleKickCore : ModSystem
    {
        ICoreServerAPI sapi;
        Dictionary<string, EntityPos> playerPositions = new Dictionary<string, EntityPos>();

        IdleKickConfig config = new IdleKickConfig();

        int maxMillisecondsIdle = 600000;

        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            ReadConfigFromJson(api);
        }

        private void ReadConfigFromJson(ICoreAPI api)
        {
            try
            {
                IdleKickConfig modConfig = api.LoadModConfig<IdleKickConfig>("IdleKickConfig.json");

                if (modConfig != null)
                {
                    config = modConfig;
                }
                else
                {
                    //We don't have a valid config.
                    throw new Exception();
                }

            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading IdleKickConfig.json, Will initialize new one", e);
                config = new IdleKickConfig();
                api.StoreModConfig(config, "IdleKickConfig.json");
            }

            // Called on both sides
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            sapi.Event.PlayerJoin += OnPlayerJoin;
            sapi.Event.PlayerLeave += OnDisconnectOrLeave;
            sapi.Event.PlayerDisconnect += OnDisconnectOrLeave;

            sapi.Event.ServerRunPhase(EnumServerRunPhase.GameReady, ApplyConfig);
            sapi.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, OnWorldReady);
        }

        private void OnPlayerJoin( IServerPlayer connectPlayer )
        {
            Debug.Assert(!playerPositions.ContainsKey(connectPlayer.PlayerUID));
            playerPositions.Add( connectPlayer.PlayerUID, connectPlayer.Entity.ServerPos.Copy() );
        }

        private void OnDisconnectOrLeave( IServerPlayer disconnectPlayer )
        {
            if (playerPositions.ContainsKey(disconnectPlayer.PlayerUID)) 
                playerPositions.Remove( disconnectPlayer.PlayerUID );
        }

        private void OnWorldReady()
        {
            FindIdlePlayers(0f);
        }

        private void ApplyConfig()
        {
            maxMillisecondsIdle = config.maxMillisecondsIdle;
        }

        private void FindIdlePlayers( float dt )
        {

            foreach ( IServerPlayer player in ( sapi.World.AllOnlinePlayers ) )
            {
                if ( player != null )
                {
                    //We only care about kicking survival players for idling.
                    EnumGameMode currentGamemode = player.WorldData.CurrentGameMode;
                    if (currentGamemode != EnumGameMode.Survival)
                        continue;

                    if ( playerPositions.ContainsKey(player.PlayerUID) )
                    {
                        EntityPos playerOldPos = playerPositions[player.PlayerUID];
                        EntityAgent playerAgent = player.Entity as EntityAgent;

                        if (playerOldPos.BasicallySameAsIgnoreAngles(player.Entity.ServerPos, 0.01f) &&
                            (playerAgent.CurrentControls.HasFlag(EnumEntityActivity.Idle) ||
                            playerAgent.CurrentControls.HasFlag(EnumEntityActivity.FloorSitting) ||
                            playerAgent.CurrentControls.HasFlag(EnumEntityActivity.Dead) ||
                            playerAgent.CurrentControls.HasFlag(EnumEntityActivity.None) ||
                            playerAgent.CurrentControls.HasFlag(EnumEntityActivity.Mounted)))
                        {
                             KickIdlePlayer(player);
                        }
                        else
                        {
                            playerPositions[player.PlayerUID] = player.Entity.ServerPos.Copy();
                        }
                    }
                    else
                    {
                        playerPositions.Add(player.PlayerUID, player.Entity.ServerPos.Copy());
                    }
                }
            }

            sapi.Event.RegisterCallback(FindIdlePlayers, maxMillisecondsIdle );
        }

        private void KickIdlePlayer( IServerPlayer player )
        {
            string minuteText = "minutes.";
            if (maxMillisecondsIdle <= 60000)
                minuteText = "minute.";

            string kickReason = "Idle for " + (int)( (maxMillisecondsIdle / 1000 ) / 60 ) + " " + minuteText;
            player.Disconnect(kickReason);
        }
    }
}
