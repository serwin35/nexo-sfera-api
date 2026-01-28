using InsERT.Moria.KlientPoczty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Ksiegowosc
{
    /// <summary>
    /// Plugin obsługujący e-maile z załączonym plikiem EPP. 
    /// Po wczytaniu e-maila zawierającego plik o rozszerzeniu .epp, program zapisze załącznik na pulpicie użytkownika w folderze ZalacznikiEPP.
    /// Następnie wyświetli użytkownikowi powiadomienie windowsowe.
    /// </summary>
    /// <remarks>Przykład działa tylko gdy w konfiguracji konta pocztowego ustawione jest pobieranie całych wiadomości (Konfiguracja konta pocztowego -> Poczta przychodząca -> Więcej opcji... -> Sposób pobierania wiadomości).</remarks>
    public class ObslugaMailaZZalaczonymPlikiemEPP : KlientSferyZdarzeniowej<IWiadomoscPocztowa>
    {
        private readonly string SciezkaDoFolderuZZalacznikami = string.Concat(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "\\ZalacznikiEPP");
        public override void PrzedZapisemObiektu(IKontekstZdarzeniaPrzedZapisemObiektu<IWiadomoscPocztowa> kontekst)
        {
            var wiadomosc = kontekst.ObiektBiznesowy.Dane;
            if (kontekst.StanObiektu == StanObiektu.Dodawany)
            {
                var zalacznikiDoZapisania = wiadomosc.Zalaczniki.Where(z => z.NazwaPliku.Contains(".epp"));
                if (zalacznikiDoZapisania.Any() && ZapiszZalaczniki(zalacznikiDoZapisania))
                {
                    new ToastContentBuilder()
                        .AddText("Przyszedł nowy e-mail z plikiem EPP w załączniku")
                        .AddText("Plik EPP został zapisany w folderze: " + SciezkaDoFolderuZZalacznikami)
                        .Show();
                }
            }
        }

        private bool ZapiszZalaczniki(IEnumerable<KopiaZalacznikaPocztowego> zalaczniki)
        {
            foreach (KopiaZalacznikaPocztowego zalacznik in zalaczniki)
            {
                string sciezka = string.Concat(SciezkaDoFolderuZZalacznikami, @"\", zalacznik.NazwaPliku);
                try
                {
                    Directory.CreateDirectory(SciezkaDoFolderuZZalacznikami);
                    // w przypadku gdy załączniki przechowywane są na serwerze to w polu Dane jest pusta tablica bajtów i plik zapisany na dysku będzie pusty.
                    File.WriteAllBytes(sciezka, zalacznik.Zalacznik.Zawartosc.Dane);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}