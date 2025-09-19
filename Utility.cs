using System;
using System.Collections.Generic;
using System.Reflection;

namespace InternalOrm
{
    internal class Utility
    {
        internal static Type GetActualType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return Nullable.GetUnderlyingType(type);
            return type;
        }

        internal PropertyInfo[] GetCachedProperties(Type type, Dictionary<Type, PropertyInfo[]> cache)
        {
            PropertyInfo[] props;
            if (!cache.TryGetValue(type, out props))
            {
                props = type.GetProperties();
                cache[type] = props;
            }
            return props;
        }

        internal bool IsValueTuple(Type t)
        {
            return t.IsValueType && t.FullName != null && t.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal);
        }

        internal bool IsSimple(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t == typeof(DateTime) ||
                   t == typeof(decimal) || t.IsEnum || t == typeof(Guid) ||
                   t == typeof(DateTimeOffset) || t == typeof(byte[]);
        }

        internal object ConvertToType(object value, Type targetType)
        {
            if (value == null || value is DBNull) return null;
            var actual = GetActualType(targetType);

            if (actual.IsEnum)
            {
                if (value is string) return Enum.Parse(actual, (string)value, true);
                return Enum.ToObject(actual, value);
            }

            if (actual == typeof(Guid))
            {
                if (value is Guid) return value;
                return new Guid(value.ToString());
            }

            if (actual == typeof(byte[])) return (byte[])value;

            if (actual == typeof(DateTimeOffset))
            {
                if (value is DateTimeOffset) return value;
                if (value is DateTime) return new DateTimeOffset((DateTime)value);
                return DateTimeOffset.Parse(value.ToString());
            }

            return Convert.ChangeType(value, actual);
        }

        internal object GetDefault(Type t)
        {
            var u = Nullable.GetUnderlyingType(t);
            if (u != null) return null;
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }

    }
}
