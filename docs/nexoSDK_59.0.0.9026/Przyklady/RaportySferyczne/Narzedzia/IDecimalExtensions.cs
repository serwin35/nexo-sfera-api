using System;

namespace RaportySferyczne
{
    internal static class IDecimalExtensions
    {
        public static decimal Round(this decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
