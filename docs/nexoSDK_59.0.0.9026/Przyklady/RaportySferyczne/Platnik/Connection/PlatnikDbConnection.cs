using System;
using System.Data.Common;
using System.Linq;

namespace RaportySferyczne.Platnik
{
    internal abstract class PlatnikDbConnection : IPlatnikDbConnection
    {
        public void ExecuteReader(string sql, Action<DbDataReader> readAction, params (string Name, object Value)[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));
            if (readAction == null)
                throw new ArgumentNullException(nameof(readAction));
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var command = CreateCommand(conn, sql))
                {
                    if (parameters.Any())
                        command.Parameters.AddRange(parameters.Select(x => CreateParameter(x.Name, x.Value)).ToArray());
                    using (var reader = command.ExecuteReader())
                    {
                        readAction(reader);
                    }
                }
            }
        }

        public TScalar ExecuteScalar<TScalar>(string sql, params (string Name, object Value)[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var command = CreateCommand(conn, sql))
                {
                    if (parameters.Any())
                        command.Parameters.AddRange(parameters.Select(x => CreateParameter(x.Name, x.Value)).ToArray());
                    return (TScalar)command.ExecuteScalar();
                }
            }
        }

        protected abstract DbConnection CreateConnection();

        protected abstract DbCommand CreateCommand(DbConnection connection, string sql);

        protected abstract DbParameter CreateParameter(string name, object value);
    }
}
