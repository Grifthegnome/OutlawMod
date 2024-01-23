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
        public int maxMinutesIdle = 15;

    }
    public class IdleKickCore : ModSystem
    {
        ICoreServerAPI sapi;
        Dictionary<string, EntityPos> playerPositions = new Dictionary<string, EntityPos>();

        IdleKickConfig config = new IdleKickConfig();

        int maxMinutesIdle = 15;

        public override double ExecuteOrder()
        {
            return 0.0;
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
            maxMinutesIdle = config.maxMinutesIdle;
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
                            playerAgent.CurrentControls.HasFlag(EnumEntityActivity.None )))
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

            sapi.Event.RegisterCallback(FindIdlePlayers, (maxMinutesIdle * 60) * 1000);
        }

        private void KickIdlePlayer( IServerPlayer player )
        {
            string kickReason = "Idle for " + maxMinutesIdle + " minutes.";
            player.Disconnect(kickReason);
        }
    }
}
