using Newtonsoft.Json;
using System.IO;

namespace GeneratorDanych
{
	public class DomyslneUstawienia
	{
		public string BazaDanych { get; set; }
		public string HasloNexo { get; set; }
		public string HasloSql { get; set; }
		public string LoginNexo { get; set; }
		public string LoginSql { get; set; }
		public string SerwerSql { get; set; }
		public bool UwierzytelnienieWindows { get; set; }

		public static DomyslneUstawienia Pobierz()
		{
			return PobierzZPliku() ?? PobierzWbudowane();
		}

		public static DomyslneUstawienia PobierzWbudowane()
		{
			return new DomyslneUstawienia()
			{
				SerwerSql = "(local)",
				BazaDanych = "Nexo_Demo_1",
				UwierzytelnienieWindows = true
			};
		}

		public static DomyslneUstawienia PobierzZPliku()
		{
			if (File.Exists("DomyslneUstawienia.json"))
			{
				var json = File.ReadAllText("DomyslneUstawienia.json");
				return JsonConvert.DeserializeObject<DomyslneUstawienia>(json);
			}
			return null;
		}
	}
}