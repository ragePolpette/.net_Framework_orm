using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Reflection;
using System.Collections.Concurrent;

namespace InternalOrm
{
    internal class InternalOrmExecutor
    {
        public Utility ut = new Utility();
        

        struct TupleMeta
        {
            public ConstructorInfo Ctor;
            public Type[] Args;
        }
        
        struct PropertyBind
        {
            public PropertyInfo Prop;
            public int Ordinal;
        }
        internal static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new Dictionary<Type, PropertyInfo[]>();
        static readonly ConcurrentDictionary<Type, TupleMeta> _tupleCache = new ConcurrentDictionary<Type, TupleMeta>();

        static TupleMeta GetTupleMeta(Type t)
        {
            TupleMeta meta;
            if (_tupleCache.TryGetValue(t, out meta)) return meta;
            var args = t.GetGenericArguments();
            var ctors = t.GetConstructors();
            meta = new TupleMeta { Ctor = ctors[0], Args = args };
            _tupleCache[t] = meta;
            return meta;
        }

        internal List<T> MapQueryResultsToList<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            using (var command = new SqlCommand(query, con))
            {
                if (parameters != null) command.AddParametersFromObject(parameters);
                using (var reader = command.ExecuteReader())
                {
                    return ReadEntitiesListFromReader<T>(reader);
                }
            }
        }

        internal T MapQueryResult<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            using (var command = new SqlCommand(query, con))
            {
                if (parameters != null) command.AddParametersFromObject(parameters);
                using (var reader = command.ExecuteReader())
                {
                    return ReadEntityFromReader<T>(reader);
                }
            }
        }

        internal int QueryNonQuery(SqlConnection con, string query, object parameters = null)
        {
            using (var command = new SqlCommand(query, con))
            {
                if (parameters != null) command.AddParametersFromObject(parameters);
                return command.ExecuteNonQuery();
            }
        }

        internal List<object> QueryAnonymousList(SqlConnection con, string query, object parameters = null)
        {
            var result = new List<object>();
            using (var command = new SqlCommand(query, con))
            {
                if (parameters != null) command.AddParametersFromObject(parameters);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.HasRows) return result;
                    var names = new string[reader.FieldCount];
                    for (int i = 0; i < names.Length; i++) names[i] = reader.GetName(i);

                    while (reader.Read())
                    {
                        var obj = new ExpandoObject() as IDictionary<string, object>;
                        for (int i = 0; i < names.Length; i++)
                            obj[names[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        result.Add(obj);
                    }
                }
            }
            return result;
        }

        internal List<T> ReadEntitiesListFromReader<T>(SqlDataReader reader)
        {
            var t = typeof(T);
            if (ut.IsSimple(t)) return ReadSimpleListFromReader<T>(reader);
            if (ut.IsValueTuple(t)) return ReadTupleListFromReader<T>(reader);
            return ReadDtoListFromReader<T>(reader);
        }

        internal T ReadEntityFromReader<T>(SqlDataReader reader) where T : new()
        {
            var t = typeof(T);
            if (ut.IsSimple(t)) return ReadSimpleFirst<T>(reader);
            if (ut.IsValueTuple(t)) return ReadTupleFirst<T>(reader);
            return ReadDtoFirst<T>(reader);
        }

        internal List<T> ReadSimpleListFromReader<T>(SqlDataReader reader)
        {
            var list = new List<T>();
            var t = typeof(T);
            while (reader.Read())
            {
                var raw = reader.GetValue(0);
                list.Add(raw == DBNull.Value ? default(T) : (T)ut.ConvertToType(raw, t));
            }
            return list;
        }

        internal T ReadSimpleFirst<T>(SqlDataReader reader)
        {
            var t = typeof(T);
            if (!reader.Read()) return default(T);
            var raw = reader.GetValue(0);
            return raw == DBNull.Value ? default(T) : (T)ut.ConvertToType(raw, t);
        }

        internal List<T> ReadTupleListFromReader<T>(SqlDataReader reader)
        {
            var list = new List<T>();
            var meta = GetTupleMeta(typeof(T));
            while (reader.Read())
            {
                var values = new object[meta.Args.Length];
                int take = reader.FieldCount < meta.Args.Length ? reader.FieldCount : meta.Args.Length;
                for (int i = 0; i < take; i++)
                {
                    var raw = reader.GetValue(i);
                    values[i] = raw == DBNull.Value ? ut.GetDefault(meta.Args[i]) : ut.ConvertToType(raw, meta.Args[i]);
                }
                for (int i = take; i < meta.Args.Length; i++) values[i] = ut.GetDefault(meta.Args[i]);
                list.Add((T)meta.Ctor.Invoke(values));
            }
            return list;
        }

        internal T ReadTupleFirst<T>(SqlDataReader reader)
        {
            var meta = GetTupleMeta(typeof(T));
            if (!reader.Read()) return default(T);
            var values = new object[meta.Args.Length];
            int take = reader.FieldCount < meta.Args.Length ? reader.FieldCount : meta.Args.Length;
            for (int i = 0; i < take; i++)
            {
                var raw = reader.GetValue(i);
                values[i] = raw == DBNull.Value ? ut.GetDefault(meta.Args[i]) : ut.ConvertToType(raw, meta.Args[i]);
            }
            for (int i = take; i < meta.Args.Length; i++) values[i] = ut.GetDefault(meta.Args[i]);
            return (T)meta.Ctor.Invoke(values);
        }

        internal List<T> ReadDtoListFromReader<T>(SqlDataReader reader)
        {
            var list = new List<T>();
            var type = typeof(T);
            var props = ut.GetCachedProperties(type, _propertyCache);

            var ordinalByName = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) ordinalByName[reader.GetName(i)] = i;

            var bind = new List<PropertyBind>(props.Length);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanWrite) continue;
                int ord;
                if (ordinalByName.TryGetValue(p.Name, out ord)) bind.Add(new PropertyBind { Prop = p, Ordinal = ord });
            }

            while (reader.Read())
            {
                var entity = Activator.CreateInstance<T>();
                for (int i = 0; i < bind.Count; i++)
                {
                    var b = bind[i];
                    var raw = reader.GetValue(b.Ordinal);
                    if (raw != DBNull.Value) b.Prop.SetValue(entity, ut.ConvertToType(raw, b.Prop.PropertyType), null);
                }
                list.Add(entity);
            }
            return list;
        }

        internal T ReadDtoFirst<T>(SqlDataReader reader) where T : new()
        {
            var type = typeof(T);
            var props = ut.GetCachedProperties(type, _propertyCache);

            var ordinalByName = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) ordinalByName[reader.GetName(i)] = i;

            var bind = new List<PropertyBind>(props.Length);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanWrite) continue;
                int ord;
                if (ordinalByName.TryGetValue(p.Name, out ord)) bind.Add(new PropertyBind { Prop = p, Ordinal = ord });
            }

            if (!reader.Read()) return default(T);
            var entity = new T();
            for (int i = 0; i < bind.Count; i++)
            {
                var b = bind[i];
                var raw = reader.GetValue(b.Ordinal);
                if (raw != DBNull.Value) b.Prop.SetValue(entity, ut.ConvertToType(raw, b.Prop.PropertyType), null);
            }
            return entity;
        }



        internal T QueryFirstOrDefault<T>(SqlConnection con, string query, object parameters = null) where T : new()
        {
            var list = MapQueryResultsToList<T>(con, query, parameters);
            return list.Count > 0 ? list[0] : default(T);
        }

        internal int ExecuteStored(SqlConnection con, string procedureName, object parameters = null)
        {
            using (var command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;
                if (parameters != null) command.AddParametersFromObject(parameters);
                return command.ExecuteNonQuery();
            }
        }

        internal T ExecuteStoredWithOutputs<T>(SqlConnection con, string procedureName, object parameters) where T : new()
        {
            using (var command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;
                if (parameters != null) command.AddParametersFromObject(parameters);
                using (var reader = command.ExecuteReader())
                {
                    return ReadEntityFromReader<T>(reader);
                }
            }
        }
        internal (T Output, object ReturnValue) ExecuteStoredWithOutputsAndReturn<T>(SqlConnection con, string procedureName, object parameters) where T : new()
        {
            using (var command = new SqlCommand(procedureName, con))
            {
                command.CommandType = CommandType.StoredProcedure;
                if (parameters != null) command.AddParametersFromObject(parameters);

                var returnParam = new SqlParameter("@ReturnValue", SqlDbType.Int)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                command.Parameters.Add(returnParam);

                using (var reader = command.ExecuteReader())
                {
                    var output = ReadEntityFromReader<T>(reader);
                    return (Output: output, ReturnValue: returnParam.Value);
                }
            }
        }

    }
}
