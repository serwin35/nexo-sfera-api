using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoresponderMailowy
{
	public static class Rozszerzenia
	{
		/// <summary>
		/// Metoda pozwalająca uzyskać listę zainstalowanych funkcji (rozszerzeń) w określonym punkcie rozszerzania.
		/// Pomaga  obejść problem występujący w przypadku niektórych funkcji, wymagających szerszego kontekstu wykonania niż kod sferyczny.
		/// </summary>
		/// <typeparam name="T">Interfejs funkcji charakterystyczny dla punktu rozszerzania, dziedziczący po <see cref="T:InsERT.Moria.Rozszerzanie.IFunkcja"/>.</typeparam>
		/// <param name="container">parametr kontekstu 1</param>
		/// <param name="uow">parametr kontekstu 2</param>
		/// <returns>Zwraca listę funkcji (wbudowanych i rozszerzeń) implementujących podany interfejs (parametr T).</returns>
		public static IEnumerable<T> PodajZarejestrowaneFunkcje<T>(this InsERT.Mox.Runtime.IInjectionContainer container, InsERT.Mox.Work.UnitOfWork uow)
			where T : IFunkcja
		{
			Type typeFabryka = null;
			var assembliesToSearch = new System.Reflection.Assembly[] { typeof(IFunkcja).Assembly };
			var typeIFabryka = typeof(IFabrykaFunkcji<T>);
			var typesInAssemblies = assembliesToSearch.SelectMany(assembly => assembly.GetTypes());
			typeFabryka = typesInAssemblies.Where(typeIFabryka.IsAssignableFrom).FirstOrDefault();

			if (typeFabryka == null)
			{
				throw new InvalidOperationException("Nie zaimplementowano fabryki funkcji.");
			}

			var funcType = typeof(Func<,>).MakeGenericType(typeof(InsERT.Mox.Runtime.IInjectionScope), typeFabryka);

			var locatorFabryki = (Func<InsERT.Mox.Runtime.IInjectionScope, object>)container.GetObject(funcType);

			var fabryka = (IFabrykaFunkcji<T>)locatorFabryki(uow);

			return fabryka.Wszystkie().OfType<T>();
		}
	}
}
