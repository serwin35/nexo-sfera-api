using System;
using System.Collections.Generic;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal static class Losowanie
	{
		internal static string LosujZTablicy(List<string> tablica, Random random)
		{
			return tablica[random.Next(0, tablica.Count)];
		}
	}
}