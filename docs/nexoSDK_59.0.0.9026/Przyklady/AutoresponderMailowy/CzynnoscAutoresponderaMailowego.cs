using HtmlAgilityPack;
using InsERT.Moria;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Dzialania;
using InsERT.Moria.KlientPoczty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AutoresponderMailowy
{
	public class CzynnoscAutoresponderaMailowego : ICzynnoscReguly
	{
		private readonly IWiadomosciPocztowe _wiadomosciPocztowe;
		private readonly IAsortymenty _asortymenty;
		private readonly IDzialania _dzialania;
		private readonly ISzablonyDzialan _szablonyDzialan;
		private readonly ITypyDzialan _typyDzialan;
		private readonly IPozycjeCennika _pozycjeCennika;
		private readonly IKontekstBiznesowy _kontekstBiznesowy;
		private readonly InsERT.Mox.Runtime.IInjectionContainer _container;

		public CzynnoscAutoresponderaMailowego(IWiadomosciPocztowe wiadomosciPocztowe,
			IAsortymenty asortymentyDane,
			IDzialania dzialania,
			ISzablonyDzialan szablonyDzialan,
			ITypyDzialan typyDzialan,
			IPozycjeCennika pozycjeCennika,
			InsERT.Mox.Runtime.IInjectionContainer container,
			IKontekstBiznesowy kontekstBiznesowy)
		{
			_wiadomosciPocztowe = wiadomosciPocztowe;
			_asortymenty = asortymentyDane;
			_dzialania = dzialania;
			_szablonyDzialan = szablonyDzialan;
			_typyDzialan = typyDzialan;
			_pozycjeCennika = pozycjeCennika;
			_container = container;
			_kontekstBiznesowy = kontekstBiznesowy;
		}

		public IEnumerable<IDefinicjaParametruReguly> WymaganeParametry
		{
			get
			{
				yield return new MinimalnaCenaDlaGenerowaniaDzialania();
				yield return new ParametrKategoriaDzialania();
			}
		}

		public bool Waliduj(IKontekstCzynnosciReguly kontekst)
		{
			return true; // zawsze poprawna reguła
		}

		public void Wykonaj(IKontekstWykonywaniaCzynnosciReguly kontekst)
		{
			var symboleTowarow = PobierzSymboleTowarowZTematu(kontekst);
			string trescOdpowiedzi;

			if (symboleTowarow.Any())
			{
				var klient = kontekst.DaneWiadomosci.Klient;
				var informacje = WyszukajInformacjeOTowarach(symboleTowarow, klient);
				trescOdpowiedzi = GenerujOdpowiedzHTML(informacje);
				WygenerujDzialaniaDlaZapytanODrogieTowary(kontekst, informacje);
			}
			else
			{
				trescOdpowiedzi = "Nieprawidłowy format/brak towarów o podanych symbolach";
			}

			using (var kopiaBO = _wiadomosciPocztowe.UtworzNowaOdpowiedz(kontekst.DaneWiadomosci))
			{
				kopiaBO.Dane.Wiadomosc.Temat = "Informacje o towarach";
				kopiaBO.Dane.Wiadomosc.Tresc.HTML = true;
				kopiaBO.Dane.Wiadomosc.Tresc.Tekst = Pakuj(trescOdpowiedzi);

				kopiaBO.WyslijWiadomosc();
				kopiaBO.Zapisz();
			}
		}

		private IList<string> PobierzSymboleTowarowZTematu(IKontekstWykonywaniaCzynnosciReguly kontekst)
		{
			string temat = kontekst.DaneWiadomosci.Temat;
			return temat.Replace("TowarInfo:", "")
				.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
				.ToList();
		}

		public IList<InformacjeOTowarze> WyszukajInformacjeOTowarach(IList<string> symboleTowarow, Podmiot klient)
		{
			return symboleTowarow.Where(TowarIstnieje).Select(symbol =>
				{
					var informacja = new InformacjeOTowarze
					{
						Symbol = symbol
					};
					ZnajdzCene(informacja, klient);
					WyliczStanMagazynowy(informacja, symbol);
					return informacja;
				}).ToList();
		}

		private bool TowarIstnieje(string symbolTowaru)
		{
			return _asortymenty.Dane.Wszystkie().Any(x => x.Symbol == symbolTowaru);
		}

		private void ZnajdzCene(InformacjeOTowarze info, Podmiot klient)//klient może być pusty, jeżeli mail od nieznanej osoby przyjdzie
		{
			decimal? cena = null;

			var aso = _asortymenty.Dane.Wszystkie().Where(a => a.Symbol == info.Symbol).Single();
			if (klient == null || (!klient.FunkcjaWyliczaniaCeny.HasValue && klient.PoziomCen == null && klient.CennikDodatkowy == null))
			{
				//bierzemy ceny dla oddziału
				cena = PodajCeneAsortymentu(aso,
					_kontekstBiznesowy.Oddzial.MiejscaSprzedazy.Single());
			}
			else
			{
				cena = PodajCeneAsortymentu(aso, klient);
			}

			info.CenaJednostkowa = cena ?? decimal.Zero;
			info.Waluta = "PLN";
		}

		private decimal? PodajCeneAsortymentu(Asortyment aso, MiejsceSprzedazy miejsceSprzedazy)
		{
			return PodajCeneAsortymentu(aso, miejsceSprzedazy.PoziomCen,
				miejsceSprzedazy.CennikDodatkowy, miejsceSprzedazy.FunkcjaWyliczaniaCeny);
		}

		private decimal? PodajCeneAsortymentu(Asortyment aso, Podmiot klient)
		{
			return PodajCeneAsortymentu(aso, klient.PoziomCen, klient.CennikDodatkowy,
				klient.FunkcjaWyliczaniaCeny, klient);
		}

		private decimal? PodajCeneAsortymentu(Asortyment aso, PoziomCen poziomCen, Cennik cennikDodatkowy,
			Guid? guidFunkcjiWyliczaniaCenyBazowej, Podmiot kontekstWyliczania = null)
		{
			decimal? cena = null;
			//bierzemy ceny dla klienta
			if (guidFunkcjiWyliczaniaCenyBazowej.HasValue)
			{
				using (var uow = new InsERT.Mox.Work.UnitOfWork())
				{
					var funkcja = _container.PodajZarejestrowaneFunkcje<IFunkcjaWyliczaniaCenyBazowej>(uow)
						.Where(f => f.Identyfikator == guidFunkcjiWyliczaniaCenyBazowej.Value).Single();
					var kwota = funkcja.WyliczCeneBazowa(aso, new PrywatnyKontekstWyliczeniaCeny() { Podmiot = kontekstWyliczania });
					if (kwota != null)
					{
						cena = kwota.Wartosc;
					}
				}
			}
			else
			{
				if (cennikDodatkowy != null)
				{
					cena = PodajCene(aso.Id, cennikDodatkowy.Id);
				}
				if (poziomCen != null)
				{
					int idCennika = poziomCen.Cenniki.Where(c => c.Bazowy).Single().Id;
					decimal cena2 = PodajCene(aso.Id, idCennika).Value; // w cenniku głównym musi być taka pozycja
					if (cena.HasValue)
					{
						cena = Math.Min(cena.Value, cena2);
					}
					else
					{
						cena = cena2;
					}
				}

			}

			return cena;
		}

		private decimal? PodajCene(int idAsortymentu, int idCennika)
		{
			var pozycja = _pozycjeCennika.Dane.Wszystkie()
				.Where(pc => pc.Cennik.Id == idCennika && pc.Asortyment.Id == idAsortymentu).FirstOrDefault();
			return pozycja == null ? (decimal?)null : pozycja.CenaNetto;
		}

		private void WyliczStanMagazynowy(InformacjeOTowarze info, string symbolTowaru)//klient może być pusty, jeżeli mail od nieznanej osoby przyjdzie
		{
			var aso = _asortymenty.Dane.WyszukajPoSymbolu(symbolTowaru);
			var magazyny = _kontekstBiznesowy.Oddzial.Magazyny.Select(m => m.Id);
			decimal stan = aso.StanyMagazynowe
				.Where(sm => magazyny.Contains(sm.Magazyn.Id))
				.Sum(sm => sm.IloscDostepna);
			info.StanMagazynowy = stan;
			info.JednostkaMiary = aso.PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Symbol;
			info.PrecyzjaJednostki =
				aso.PodstawowaJednostkaMiaryAsortymentu.Precyzja.HasValue
				? aso.PodstawowaJednostkaMiaryAsortymentu.Precyzja.Value
				: aso.PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Precyzja ?? 6;
		}

		private string GenerujOdpowiedzHTML(IList<InformacjeOTowarze> informacjeOTowarach)
		{
			var dokumentHtml = new HtmlDocument();

			var tabela = HtmlNode.CreateNode("<table>");
			tabela.SetAttributeValue("style", "width:100%; border-collapse: collapse; font-family:Verdana,Tahoma,'Lucida Sans',Arial,sans-serif");

			var naglowek = HtmlNode.CreateNode("<tr>");
			DodajKomorke(naglowek, "Lp.", "border: 1px solid #000; padding: 3px; text-align: center; background-color: lightgrey; font-weight: bold;");
			DodajKomorke(naglowek, "Symbol towaru", "border: 1px solid #000; padding: 3px; text-align: center; background-color: lightgrey; font-weight: bold;");
			DodajKomorke(naglowek, "Stan magazynowy", "border: 1px solid #000; padding: 3px; text-align: center; background-color: lightgrey; font-weight: bold;");
			DodajKomorke(naglowek, "Cena netto", "border: 1px solid #000; padding: 3px; text-align: center; background-color: lightgrey; font-weight: bold;");
			tabela.AppendChild(naglowek);

			foreach (var zawartoscWiersza in informacjeOTowarach.Select((info, index) => new { Info = info, Index = index + 1 }))
			{
				var wiersz = HtmlNode.CreateNode("<tr>");
				DodajKomorke(wiersz, zawartoscWiersza.Index.ToString(),
					"border: 1px solid #000; padding: 3px; text-align: center;");
				DodajKomorke(wiersz, zawartoscWiersza.Info.Symbol,
					"border: 1px solid #000; padding: 3px;");
				DodajKomorke(wiersz, string.Format("{0} {1}",
						zawartoscWiersza.Info.StanMagazynowy.ToString("0." + new string('0', zawartoscWiersza.Info.PrecyzjaJednostki)),
						zawartoscWiersza.Info.JednostkaMiary),
					"border: 1px solid #000; padding: 3px; text-align: right;");
				DodajKomorke(wiersz, string.Format("{0} {1}",
						zawartoscWiersza.Info.CenaJednostkowa.ToString("0.00"),
						zawartoscWiersza.Info.Waluta),
					"border: 1px solid #000; padding: 3px; text-align: right;");
				tabela.AppendChild(wiersz);
			}
			dokumentHtml.DocumentNode.AppendChild(tabela);

			return dokumentHtml.DocumentNode.OuterHtml;
		}

		private void DodajKomorke(HtmlNode wiersz, string tresc, string style)
		{
			var komorka = HtmlNode.CreateNode("<td>");
			komorka.SetAttributeValue("style", style);
			komorka.InnerHtml = tresc;
			wiersz.AppendChild(komorka);
		}

		private static byte[] Pakuj(string tresc)
		{
			using (var strumienWyjsciowy = new MemoryStream())
			{
				using (var strumienWejsciowy = new MemoryStream(Encoding.UTF8.GetBytes(tresc)))
				{
					using (var strumienPakujacy = new GZipStream(strumienWyjsciowy, CompressionMode.Compress))
					{
						strumienWejsciowy.CopyTo(strumienPakujacy);
					}

					return strumienWyjsciowy.ToArray();
				}
			}
		}

		private void WygenerujDzialaniaDlaZapytanODrogieTowary(IKontekstWykonywaniaCzynnosciReguly kontekst, IList<InformacjeOTowarze> informacje)
		{
			if (kontekst.Parametry.Any())
			{
				decimal progCenowy = (decimal)kontekst.Parametry.First();
				foreach (var drogiTowar in informacje.Where(i => i.CenaJednostkowa >= progCenowy))
				{
					WygenerujDzialanieDlaTowaru(kontekst, drogiTowar.Symbol);
				}
			}
		}

		private void WygenerujDzialanieDlaTowaru(IKontekstWykonywaniaCzynnosciReguly kontekst, string towar)
		{
			using (var dzialanie = _dzialania.UtworzNaPodstawieSzablonu(_szablonyDzialan.DomyslnySzablonDlaTypu(_typyDzialan.DaneDomyslne.Zadanie)))
			{
				if (kontekst.DaneWiadomosci.Klient != null) //wiadomość od znanego klienta
				{
					var uczestnik = dzialanie.DodajUczestnika(kontekst.DaneWiadomosci.Klient);

					if (uczestnik.Podmiot.OpiekunPodstawowy != null)
					{
						dzialanie.UstawWykonawce(uczestnik.Podmiot.OpiekunPodstawowy.Uzytkownik);
					}
				}
				else
				{
					string nadawca = string.Format("{0} ({1})", kontekst.DaneWiadomosci.NadawcaNazwa, kontekst.DaneWiadomosci.NadawcaAdres);
					dzialanie.Dane.OpisKrotki = string.Format("Zapytanie od nieznanego klienta {0}", nadawca);
				}

				dzialanie.Dane.Temat = string.Format("Zapytanie o towar {0}", towar);
				dzialanie.Dane.Kategoria = kontekst.Parametry.ElementAt(1) as KategoriaDzialania;

				if (dzialanie.Zapisz())
				{
					kontekst.WykonajNaWiadomosci(w => w.Dane.Dzialanie = dzialanie.Dane); // wiązanie dodanego działania z wiadomością
				}
			}
		}

		public Guid Identyfikator => new Guid("1eef8745-3952-46d0-bd37-a4549a13d653");

		public string Nazwa => "Autoresponder z cenami towarów";

		public string Opis => "Zwraca informacje o cenie i dostępności towarów, na podstawie symboli podanych w temacie wiadomości (rozdzielone średnikiem)";

		public IDostawcaPluginow Dostawca => new DostawcaPluginow();

	}
}
