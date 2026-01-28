using InsERT.Moria.Klasyfikatory;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Promocje;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace KlasyfikatoryPlugins
{
    public class AtrybutKlasyfikacji_TowaryObjetePromocja : AtrybutKlasyfikacji<Asortyment>
    {
        private readonly IPromocje _promocje;

        public AtrybutKlasyfikacji_TowaryObjetePromocja(
            IPromocje promocje)
        {
            _promocje = promocje;
        }

        public override string Nazwa => "Asortyment objęty promocją";

        public override ZrodloDanychAtrybutuKlasyfikacji UtworzZrodloDanych(
            IKontekstKlasyfikacji kontekst)
        {
            var aktywnePromocje = _promocje
                .Dane
                .Wszystkie()
                .Where(p => p.Status == (byte)StatusPromocji.Aktywna)
                .AsEnumerable()
                .Select(p => new WartoscAtrybutuKlasyfikacji(p.Id.ToString(), p.Nazwa))
                .ToArray();

            return ZrodloDanychAtrybutuKlasyfikacji.Utworz(aktywnePromocje);
        }

        protected override Expression<Func<Asortyment, bool>> UtworzWyrazenieFiltrujace(
            IWirtualnyElementKlasyfikujacy element,
            IKontekstKlasyfikacji kontekst)
        {
            if (!int.TryParse(element.Wartosc, out var id))
            {
                return null;
            }

            var kryterium = _promocje
                .Dane
                .Wszystkie()
                .Where(p => p.Id == id)
                .SelectMany(p => p.KryteriaWyboru)
                .Select(p => p.ParametrWyboruAsortymentu)
                .Where(p => p != null)
                .Select(p => new
                {
                    Id = p.Id,
                    Grupy = p.Grupy.Select(g => g.Id),
                    Cechy = p.Cechy.Select(c => c.Id),
                    Dzialy = p.DzialySprzedazy.Select(d => d.Id),
                    Asortymenty = p.Asortymenty.Select(a => a.Id),
                    Operacja = (OperacjaLogiczna)p.OperacjaLogiczna
                })
                .FirstOrDefault();

            if (kryterium == null)
            {
                return null;
            }

            var warunki = new List<Expression<Func<Asortyment, bool>>>();
            Expression<Func<Asortyment, bool>> warunekAsortyment = null;

            if (kryterium.Grupy.Any())
            {
                warunki.Add(WarunekGrupy(kryterium.Grupy));
            }

            if (kryterium.Cechy.Any())
            {
                warunki.Add(WarunekCechy(kryterium.Cechy));
            }

            if (kryterium.Dzialy.Any())
            {
                warunki.Add(WarunekDzialySprzedazy(kryterium.Dzialy));
            }

            if (kryterium.Asortymenty.Any())
            {
                warunekAsortyment = WarunekAsortymenty(kryterium.Asortymenty);
            }

            if (warunki.Count == 0)
            {
                return warunekAsortyment;
            }

            var parametr = Expression.Parameter(typeof(Asortyment));
            var body = PodmienParametr(warunki[0], parametr);

            if (warunki.Count > 1)
            {
                body = warunki
                    .Skip(1)
                    .Aggregate(
                        body,
                        (a, e) => kryterium.Operacja == OperacjaLogiczna.And ?
                                  Expression.AndAlso(a, PodmienParametr(e, parametr)) :
                                  Expression.OrElse(a, PodmienParametr(e, parametr)));
            }

            if (warunekAsortyment != null)
            {
                body = Expression.OrElse(body, PodmienParametr(warunekAsortyment, parametr));
            }

            return Expression.Lambda<Func<Asortyment, bool>>(body, parametr);
        }

        private static Expression PodmienParametr(
            Expression<Func<Asortyment, bool>> wyrazenie,
            ParameterExpression parametr)
        {
            return ReplacingVisitor.Replace(wyrazenie.Body, wyrazenie.Parameters[0], parametr);
        }

        private static Expression<Func<Asortyment, bool>> WarunekGrupy(
            IEnumerable<int> grupy)
        {
            return a => a.Grupa != null && grupy.Contains(a.Grupa.Id);
        }

        private static Expression<Func<Asortyment, bool>> WarunekCechy(
            IEnumerable<int> cechy)
        {
            return a => a.Cechy.Any(c => cechy.Contains(c.Id));
        }

        private static Expression<Func<Asortyment, bool>> WarunekDzialySprzedazy(
            IEnumerable<int> dzialy)
        {
            return a => a.DzialySprzedazy.Any(d => dzialy.Contains(d.Id));
        }

        private static Expression<Func<Asortyment, bool>> WarunekAsortymenty(
            IEnumerable<int> asortymenty)
        {
            return a => asortymenty.Contains(a.Id);
        }

        private class ReplacingVisitor : ExpressionVisitor
        {
            private readonly Expression _nodeToReplace;
            private readonly Expression _replacement;

            private ReplacingVisitor(
                Expression nodeToReplace,
                Expression replacement)
            {
                _nodeToReplace = nodeToReplace;
                _replacement = replacement;
            }

            public static Expression Replace(
                Expression expression,
                Expression nodeToReplace,
                Expression replacement)
            {
                return new ReplacingVisitor(nodeToReplace, replacement)
                    .Visit(expression);
            }

            public override Expression Visit(
                Expression node)
            {
                if (node == _nodeToReplace)
                {
                    return _replacement;
                }

                return base.Visit(node);
            }
        }
    }
}
