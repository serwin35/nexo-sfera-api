using System;

namespace ObslugaLogistyki.Parametry
{
    [Flags]
    public enum TypObslugiwanejRealizacji : int
    {
        ZkWz = 1,
        ZkDs = 2,
        WzDs = 4
    }
}
