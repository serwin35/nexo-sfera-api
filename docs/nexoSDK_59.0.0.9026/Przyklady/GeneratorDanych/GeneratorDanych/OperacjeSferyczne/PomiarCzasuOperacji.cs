using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GeneratorDanych.OperacjeSferyczne
{
	internal class PomiarCzasuOperacji
	{
		private readonly List<TimeSpan> _czasy = new List<TimeSpan>();
		private readonly Stopwatch _stopwatch = new Stopwatch();

		internal (int IleOperacji, double SredniCzasOperacji, int IleOperacjiNaMinute) PobierzStatystyki()
		{
			if (_czasy.Count < 7)
			{
				return (0, double.NaN, 0);
			}

			int odrzucZJednegoKonca = Math.Max(3, _czasy.Count / 20);
			int ileOperacjiDoSredniej = _czasy.Count - odrzucZJednegoKonca * 2;

			double srednia = _czasy.OrderBy(x => x.TotalSeconds).Skip(odrzucZJednegoKonca).Take(ileOperacjiDoSredniej).Average(x => x.TotalSeconds);
			int naMinute = (int)(60 / srednia);
						
			return (ileOperacjiDoSredniej, srednia, naMinute);
		}

		internal void Start()
		{
			_stopwatch.Restart();
		}

		internal TimeSpan Stop()
		{
			_stopwatch.Stop();
			_czasy.Add(_stopwatch.Elapsed);
			return _stopwatch.Elapsed;
		}
	}
}