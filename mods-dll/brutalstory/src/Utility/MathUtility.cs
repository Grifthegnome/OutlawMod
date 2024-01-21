
using System;
using Vintagestory.API.MathTools;

namespace BrutalStory
{
    public class MathUtility
    {
        public static double GraphClampedValue( double inputStart, double inputEnd, double outputStart, double outputEnd, double inputValue )
        {
            double lowVal = inputStart > inputEnd ? inputEnd : inputStart;
            double highVal = inputStart > inputEnd ? inputStart : inputEnd;

            double clampedInputValue = GameMath.Clamp(inputValue, lowVal, highVal);
            double slope = 1.0 * (outputEnd - outputStart) / (inputEnd - inputStart);
            double outputValue = outputStart + slope * (clampedInputValue - inputStart);
            return outputValue;
        }
    }
}