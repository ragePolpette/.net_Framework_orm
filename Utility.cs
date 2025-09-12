using System;
using System.Collections.Generic;
using System.Reflection;

namespace InternalOrm
{
    internal class Utility
    {
        internal Type GetActualType(Type propertyType)
        {
            return Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        }

      
        internal PropertyInfo[] GetCachedProperties(Type type, Dictionary<Type, PropertyInfo[]> _propertyCache)
        {
            if (!_propertyCache.ContainsKey(type))
            {
                _propertyCache[type] = type.GetProperties();
            }
            return _propertyCache[type];
        }


        internal object ConvertToType(object value, Type targetType)
        {
            if (targetType.IsEnum)
            {
                return value is string stringValue
                    ? Enum.Parse(targetType, stringValue, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}
