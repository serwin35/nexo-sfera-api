using System;

namespace ObslugaLogistyki.Parametry
{
    [Flags]
    public enum TypObslugiwanegoDokumentu : int
    {
        Zk = 1,
        Wz = 2,
        Ds = 4
    }
}
