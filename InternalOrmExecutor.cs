using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Reflection;

namespace InternalOrm
{

    internal class InternalOrmExecutor
    {
        public Utility ut = new Utility();
        internal static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new Dictionary<Type, PropertyInfo[]>();
        internal List<T> MapQueryResultsToList<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            using (SqlCommand command = new SqlCommand(query, con))
            {
                if (parameters != null)
                {
                    command.AddParametersFromObject(parameters);
                }

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return ReadEntitiesListFromReader<T>(reader);
                }
            }
        }

        internal T MapQueryResult<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            using (SqlCommand command = new SqlCommand(query, con))
            {
                if (parameters != null)
                {
                    command.AddParametersFromObject(parameters);
                }

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return ReadEntityFromReader<T>(reader);
                }
            }
        }

        internal int QueryNonQuery(SqlConnection con, string query, object parameters = null)
        {
            using (SqlCommand command = new SqlCommand(query, con))
            {
                if (parameters != null)
                {
                    command.AddParametersFromObject(parameters);
                }

                return command.ExecuteNonQuery();
            }
        }

        internal List<object> QueryAnonymousList(SqlConnection con, string query, object parameters = null)
        {
            var result = new List<object>();

            using (SqlCommand command = new SqlCommand(query, con))
            {
                if (parameters != null)
                {
                    command.AddParametersFromObject(parameters);
                }

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var resultObject = new ExpandoObject() as IDictionary<string, object>;

                        for (int index = 0; index < reader.FieldCount; index++)
                        {
                            string fieldName = reader.GetName(index);
                            object fieldValue = reader.IsDBNull(index) ? null : reader.GetValue(index);
                            resultObject[fieldName] = fieldValue;
                        }

                        result.Add(resultObject);
                    }
                }
            }

            return result;
        }

        internal List<T> ReadEntitiesListFromReader<T>(SqlDataReader reader)
        {
            var entities = new List<T>();
            var type = typeof(T);

            bool isSimpleType = type.IsPrimitive || type == typeof(string)
                || type == typeof(DateTime) || type == typeof(decimal)
                || type.IsEnum;

            while (reader.Read())
            {
                if (isSimpleType)
                {
                    object value = reader.GetValue(0);
                    entities.Add(value == DBNull.Value ? default(T) : (T)Convert.ChangeType(value, type));
                }
                else
                {
                    T entity = Activator.CreateInstance<T>();
                    var hashtable = new Hashtable();
                    var properties = ut.GetCachedProperties(type, _propertyCache);

                    foreach (PropertyInfo prop in properties)
                        hashtable[prop.Name.ToUpper()] = prop;

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string columnName = reader.GetName(i).ToUpper();
                        if (hashtable[columnName] is PropertyInfo prop && prop.CanWrite)
                        {
                            object value = reader.GetValue(i);
                            if (value != DBNull.Value)
                                prop.SetValue(entity, Convert.ChangeType(value, ut.GetActualType(prop.PropertyType)));
                        }
                    }

                    entities.Add(entity);
                }
            }

            return entities;
        }

        internal T ReadEntityFromReader<T>(SqlDataReader reader) where T : new()
        {
            var type = typeof(T);

            if (reader.Read())
            {
                if (type.IsPrimitive || type == typeof(string)
                    || type == typeof(DateTime) || type == typeof(decimal)
                    || type.IsEnum)
                {
                    object value = reader.GetValue(0);
                    if (value == DBNull.Value)
                        return default(T);

                    return (T)ut.ConvertToType(value, type);

                }

                T entity = new T();
                var hashtable = new Hashtable();
                var properties = ut.GetCachedProperties(type, _propertyCache);

                foreach (PropertyInfo prop in properties)
                    hashtable[prop.Name.ToUpper()] = prop;

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i).ToUpper();
                    if (hashtable[columnName] is PropertyInfo prop && prop.CanWrite)
                    {
                        object value = reader.GetValue(i);
                        if (value != DBNull.Value)
                        {
                            var actualType = ut.GetActualType(prop.PropertyType);
                            prop.SetValue(entity, ut.ConvertToType(value, actualType));
                        }
                    }
                }

                return entity;
            }

            return default(T);
        }


        internal T QueryFirstOrDefault<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            var list = MapQueryResultsToList<T>(con, query, parameters);
            return list.Count > 0 ? list[0] : default(T);
        }

        internal int ExecuteStored(SqlConnection con, string procedureName, object parameters = null)
        {
            using (SqlCommand command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    command.AddParametersFromObject(parameters);
                }

                return command.ExecuteNonQuery();
            }
        }
        internal T ExecuteStoredWithOutputs<T>(SqlConnection con, string procedureName, object parameters) where T : new()
        {
            using (SqlCommand command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                    command.AddParametersFromObject(parameters);

                using (var reader = command.ExecuteReader())
                {
                    return ReadEntityFromReader<T>(reader);
                }
            }
        }

        internal (T Output, object ReturnValue) ExecuteStoredWithOutputsAndReturn<T>(SqlConnection con, string procedureName, object parameters) where T : new()
        {
            using (SqlCommand command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                    command.AddParametersFromObject(parameters);

                var returnParam = new SqlParameter("@ReturnValue", SqlDbType.Int)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                command.Parameters.Add(returnParam);

                using (var reader = command.ExecuteReader())
                {
                    T output = ReadEntityFromReader<T>(reader);
                    return (output, returnParam.Value);
                }
            }
        }


    }
}
