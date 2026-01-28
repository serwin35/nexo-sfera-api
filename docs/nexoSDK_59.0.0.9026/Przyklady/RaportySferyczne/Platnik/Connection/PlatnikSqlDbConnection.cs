using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace RaportySferyczne.Platnik
{
    internal class PlatnikSqlDbConnection : PlatnikDbConnection
    {
        private readonly string _connectionString;

        public PlatnikSqlDbConnection(string serverName, string databaseName)
        {
            if (String.IsNullOrWhiteSpace(serverName))
                throw new ArgumentNullException(nameof(serverName));
            if (String.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentNullException(nameof(databaseName));

            var connectionStringBuilder = new SqlConnectionStringBuilder();
            connectionStringBuilder.DataSource = serverName;
            connectionStringBuilder.InitialCatalog = databaseName;
            connectionStringBuilder.IntegratedSecurity = true;
			connectionStringBuilder.TrustServerCertificate = true;
			_connectionString = connectionStringBuilder.ConnectionString;
        }

        public PlatnikSqlDbConnection(string serverName, string databaseName, string login, string password)
        {
            if (String.IsNullOrWhiteSpace(serverName))
                throw new ArgumentNullException(nameof(serverName));
            if (String.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentNullException(nameof(databaseName));
            if (String.IsNullOrWhiteSpace(login))
                throw new ArgumentNullException(nameof(login));
            if (String.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

            var connectionStringBuilder = new SqlConnectionStringBuilder();
            connectionStringBuilder.DataSource = serverName;
            connectionStringBuilder.InitialCatalog = databaseName;
            connectionStringBuilder.IntegratedSecurity = false;
            connectionStringBuilder.UserID = login;
            connectionStringBuilder.Password = password;
			connectionStringBuilder.TrustServerCertificate = true;
			_connectionString = connectionStringBuilder.ConnectionString;
        }

        protected override DbCommand CreateCommand(DbConnection connection, string sql)
        {
            return new SqlCommand(sql, connection as SqlConnection);
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        protected override DbParameter CreateParameter(string name, object value)
        {
            return new SqlParameter(name, value);
        }
    }
}
