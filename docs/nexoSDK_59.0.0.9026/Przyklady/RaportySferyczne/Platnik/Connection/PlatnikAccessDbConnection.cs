using System;
using System.Data.Common;
using System.Data.OleDb;

namespace RaportySferyczne.Platnik
{
    internal class PlatnikAccessDbConnection : PlatnikDbConnection
    {
        private readonly string _connectionString;

        public PlatnikAccessDbConnection(string filePath, string password)
        {
            if (String.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (String.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

            var connectionStringBuilder = new OleDbConnectionStringBuilder();
            connectionStringBuilder.Provider = "Microsoft.Jet.OLEDB.4.0";
            connectionStringBuilder.DataSource = filePath;
            connectionStringBuilder.Add("Jet OLEDB:Database Password", password);

            _connectionString = connectionStringBuilder.ConnectionString;
        }

        protected override DbCommand CreateCommand(DbConnection connection, string sql)
        {
            return new OleDbCommand(sql, connection as OleDbConnection);
        }

        protected override DbConnection CreateConnection()
        {
            return new OleDbConnection(_connectionString);
        }

        protected override DbParameter CreateParameter(string name, object value)
        {
            return new OleDbParameter(name, value);
        }
    }
}
