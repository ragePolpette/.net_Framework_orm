using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace InternalOrm
{
    public class InternalOrmService
    {
        public string ConnectionString { get; set; }

        private readonly InternalOrmExecutor _executor;

        public InternalOrmService()
        {
            _executor = new InternalOrmExecutor();
        }

        public InternalOrmService(string connectionString)
        {
            ConnectionString = connectionString;
            _executor = new InternalOrmExecutor();
        }

        protected T WithConnection<T>(Func<SqlConnection, T> getData)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                return getData(connection);
            }
        }

        protected void WithConnection(Action<SqlConnection> useConnection)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                useConnection(connection);
            }
        }

        public List<T> MapQueryResultsToList<T>(string query, object parameters = null) where T : new()
        {
            return WithConnection(con => _executor.MapQueryResultsToList<T>(con, query, parameters));
        }

        public T MapQueryResult<T>(string query, object parameters = null) where T : new()
        {
            return WithConnection(con => _executor.MapQueryResult<T>(con, query, parameters));
        }

        public int QueryNonQuery(string query, object parameters = null)
        {
            return WithConnection(con => _executor.QueryNonQuery(con, query, parameters));
        }

        public List<object> QueryAnonymousList(string query, object parameters = null)
        {
            return WithConnection(con => _executor.QueryAnonymousList(con, query, parameters));
        }
        public T QueryFirstOrDefault<T>(string query, object parameters = null) where T : new()
        {
            return WithConnection(con => _executor.QueryFirstOrDefault<T>(con, query, parameters));
        }
        
        public int ExecuteStored(string procedureName, object parameters = null)
        {
            return WithConnection(con => _executor.ExecuteStored(con, procedureName, parameters));
        }

        /// <summary>
        /// Esegue una stored procedure con output parameters e restituisce un oggetto di tipo T.
        /// !!! è importante che T abbia un costruttore senza parametri e cge la stored restituisca con select e non ouput
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="procedureName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public T ExecuteStoredWithOutputs<T>(string procedureName, object parameters) where T : new()
        {
            return WithConnection(con =>
                _executor.ExecuteStoredWithOutputs<T>(con, procedureName, parameters)
            );
        }

        /// <summary>
        /// Esegue una stored procedure con output parameters e restituisce un oggetto di tipo T e un valore di ritorno.
        /// !!! è importante che T abbia un costruttore senza parametri e cge la stored restituisca con select e non ouput
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="procedureName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public (T Output, object ReturnValue) ExecuteStoredWithOutputsAndReturn<T>(string procedureName, object parameters) where T : new()
        {
            return WithConnection(con =>
                _executor.ExecuteStoredWithOutputsAndReturn<T>(con, procedureName, parameters)
            );
        }

    }
}
