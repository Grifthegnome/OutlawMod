using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace EvilBelow
{
    static class Utility
    {
        public static bool AnyPlayersOnlineInSurvivalMode( ICoreAPI api )
        {
            IPlayer[] playersOnline = api.World.AllOnlinePlayers;
            foreach ( IPlayer player in playersOnline )
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        public static bool AnyPlayersOnlineInSurvivalMode( ICoreServerAPI sapi )
        {
            IPlayer[] playersOnline = sapi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        public static bool AnyPlayersOnlineInSurvivalMode( ICoreClientAPI capi )
        {
            IPlayer[] playersOnline = capi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        //Note: This function networks a debug message, use this sparingly because it can cause massive hitches.
        public static void DebugLogToPlayerChat(ICoreServerAPI sapi, string text )
        {
            if (!EBGlobalConstants.devMode)
                return;

            string message = "[Evil Below Mod Debug] " + text;

            IPlayer[] playersOnline = sapi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                IServerPlayer serverPlayer = player as IServerPlayer;
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
            }            
        }

        public static void DebugLogMessage(ICoreAPI api, string text )
        {
            if (!EBGlobalConstants.devMode)
                return;

            string message = "[Evil Below Mod Debug] " + text;
            api.Logger.Debug(message);
        }
    }
}