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
    }
}
