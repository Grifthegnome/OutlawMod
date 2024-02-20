using System.Collections.Generic;
using Vintagestory.GameContent;

namespace EvilBelow
{
    public interface IEBSpawnBlocker : IPointOfInterest
    {
        float blockingRange();
    }
}