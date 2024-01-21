using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace BrutalStory
{
    public static class BrutalUtility
    {
        public static bool DoesEntityAgentBleed( EntityAgent agent )
        {
            switch( agent.Code.ToString() )
            {
                case "game:strawdummy":
                    return false;
            }

            return true;
        }
    }
}
