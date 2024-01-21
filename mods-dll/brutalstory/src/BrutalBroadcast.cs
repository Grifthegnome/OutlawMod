using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace BrutalStory
{
    public class BrutalBroadcast
    {
        public static ICoreClientAPI clientCoreApi;
        public static ICoreServerAPI serverCoreApi;

        public static void InitServer( ICoreServerAPI sapi )
        {
            serverCoreApi = sapi;
        }

        public static void InitClient( ICoreClientAPI capi )
        {
            clientCoreApi = capi;
        }
    }
}
