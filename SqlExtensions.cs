using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

internal static class SqlExtensions
{
    public static void AddParametersFromObject(this SqlCommand command, object parameters)
    {
        foreach (PropertyInfo prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters, null);
            if (value == null)
                continue;

            string paramName = prop.Name.StartsWith("@") ? prop.Name : "@" + prop.Name;

            // Se è già un SqlParameter: usalo direttamente
            if (value is SqlParameter sqlParam)
            {
                if (string.IsNullOrEmpty(sqlParam.ParameterName))
                    sqlParam.ParameterName = paramName;

                command.Parameters.Add(sqlParam);
            }
            // Se è una collezione (ma non stringa), gestisci come IN-clause
            else if (value is IEnumerable enumerable && !(value is string))
            {
                var valuesArray = enumerable.Cast<object>().ToArray();
                if (valuesArray.Length > 0)
                {
                    command.AddInClauseParameters(prop.Name, valuesArray);
                }
            }
            // Valore semplice
            else
            {
                command.Parameters.AddWithValue(paramName, value);
            }
        }
    }

    public static SqlParameter[] AddInClauseParameters<T>(this SqlCommand cmd, string paramNameRoot, IEnumerable<T> values, SqlDbType? dbType = null, int? size = null)
    {
        var parameters = new List<SqlParameter>();
        var parameterNames = new List<string>();
        int index = 1;

        foreach (var value in values)
        {
            string paramName = $"@{paramNameRoot}{index++}";
            parameterNames.Add(paramName);

            SqlParameter p = new SqlParameter(paramName, (object)value ?? DBNull.Value);
            if (dbType.HasValue) p.SqlDbType = dbType.Value;
            if (size.HasValue) p.Size = size.Value;

            cmd.Parameters.Add(p);
            parameters.Add(p);
        }

        cmd.CommandText = cmd.CommandText.Replace("{" + paramNameRoot + "}", string.Join(",", parameterNames));
        return parameters.ToArray();
    }

}
