using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Mox.DatabaseAccess;
using System;

namespace RozszerzeniaKontroliSkarbowej
{
    /// <summary>
    /// Klasa bazowa zawierająca niezaimplementowane obiekty pomocnicze dostępne przy pisaniu kodu rozszerzeń plików JPK.
    /// </summary>
    public abstract class RozszerzeniaBase
    {
        #region not implemented

        protected TRepository PodajDaneTypu<TRepository>()
            where TRepository : class
            => throw new NotImplementedException();

        protected IDbConnectionFactory FabrykaPolaczenia => throw new NotImplementedException();

        protected IPolaWlasneAdv2AccessorFactory PolaWlasneAccessorFactory => throw new NotImplementedException();

        protected TZmienna PobierzLubDodajZmiennaUzytkownika<TZmienna>(string nazwa, Func<TZmienna> nowaWartosc) => throw new NotImplementedException();

        protected void DodajLubZmodyfikujZmiennaUzytkownika<TZmienna>(string nazwa, TZmienna nowaWartosc) => throw new NotImplementedException();

        #endregion
    }
}
