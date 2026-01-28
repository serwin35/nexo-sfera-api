using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal class GeneratorNazwTowarow
	{
		private readonly List<string> _cechy;
		private readonly Random _random = new Random();
		private readonly List<string> _towary;
		private readonly List<string> _wzory;

		internal GeneratorNazwTowarow()
		{
			_towary = WczytajZPliku("Pliki\\Towary.txt");
			_wzory = WczytajZPliku("Pliki\\Wzory.txt");
			_cechy = WczytajZPliku("Pliki\\Cechy.txt");
		}

		internal static string GenerujSymbolNaPodstawieNazwy(string nazwa)
		{
			string[] skladowe = nazwa.Split(' ');
			return $"GEN_{string.Join("", skladowe.Select(x => x.Length > 3 ? x.Substring(0, 3) : x))}";
		}

		internal string GenerujNazweTowaru()
		{
			return $"{Losowanie.LosujZTablicy(_towary, _random)} {Losowanie.LosujZTablicy(_wzory, _random)} {Losowanie.LosujZTablicy(_cechy, _random)} {_random.Next(1000, int.MaxValue)}";
		}

		private static List<string> WczytajZPliku(string plikZDanymi)
		{
			if (!File.Exists(plikZDanymi))
			{
				throw new FileNotFoundException($"Nie znaleziono pliku {plikZDanymi}.");
			}

			return new List<string>(File.ReadAllLines(plikZDanymi));
		}
	}
}