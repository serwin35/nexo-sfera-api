using System;
using System.Data.Common;

namespace RaportySferyczne.Platnik
{
    internal interface IPlatnikDbConnection
    {
        void ExecuteReader(string sql, Action<DbDataReader> readAction, params (string Name, object Value)[] parameters);

        TScalar ExecuteScalar<TScalar>(string sql, params (string Name, object Value)[] parameters);
    }
}
