using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RozwiazanieWlasne
{
    public interface IOdczytIZapisWartosciPolWlasnychTester
    {
        void PodpiecieDoZdarzeniaPropertyChanged();
        void OdpiecieOdZdarzeniaPropertyChanged();

        void OdczytIZapisWartosciPolaTypuTekst();
        void OdczytIZapisWartosciPolaTypuDlugiTekst();
        void OdczytIZapisWartosciPolaTypuLiczbaCalkowita();
        void OdczytIZapisWartosciPolaTypuLiczbaRzeczywista();
        void OdczytIZapisWartosciPolaTypuWartoscLogiczna();
        void OdczytIZapisWartosciPolaTypuData();
        void OdczytIZapisWartosciPolaTypuSlownikWlasny();
        void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByInt();
        void OdczytIZapisWartosciPolaTypuSlownikWlasnySqlByGuid();
        void OdczytIZapisWartosciPolaTypuSlownikSystemowyWalut();
        void OdczytIZapisWartosciPolaTypuSlownikSystemowyMagazynow();
        void OdczytIZapisWartosciPolaTypuSlownikSystemowyRachunkowBankowych();
    }
}