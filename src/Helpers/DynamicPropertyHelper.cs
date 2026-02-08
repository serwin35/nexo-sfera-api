namespace NexoSferaApi.Helpers;

/// <summary>
/// Helper class for safely accessing properties on dynamic SDK objects.
/// The Nexo Sfera SDK uses EntityObject types that don't expose properties
/// at compile time in the expected way, so we use dynamic + reflection.
/// </summary>
public static class DynamicPropertyHelper
{
    /// <summary>
    /// Gets a property value from a dynamic object using reflection.
    /// </summary>
    public static dynamic? GetProperty(dynamic obj, string propertyName)
    {
        if (obj == null) return null;
        try
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propertyName);
            return prop?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a string property value, optionally navigating through a nested property.
    /// </summary>
    public static string? GetString(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a DateTime property value, optionally navigating through a nested property.
    /// </summary>
    public static DateTime? GetDateTime(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return (DateTime?)val;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a decimal property value, optionally navigating through a nested property.
    /// Returns 0 if the property is not found or null.
    /// </summary>
    public static decimal GetDecimal(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return 0;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return 0;
            return Convert.ToDecimal(val);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a nullable decimal property value, optionally navigating through a nested property.
    /// </summary>
    public static decimal? GetNullableDecimal(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return Convert.ToDecimal(val);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a bool property value, optionally navigating through a nested property.
    /// Returns false if the property is not found or null.
    /// </summary>
    public static bool GetBool(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return false;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return false;
            return (bool)val;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a nullable bool property value, optionally navigating through a nested property.
    /// </summary>
    public static bool? GetNullableBool(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return (bool)val;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets an int property value, optionally navigating through a nested property.
    /// Returns 0 if the property is not found or null.
    /// </summary>
    public static int GetInt(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return 0;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return 0;
            return Convert.ToInt32(val);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a nullable int property value, optionally navigating through a nested property.
    /// </summary>
    public static int? GetNullableInt(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return Convert.ToInt32(val);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a long property value, optionally navigating through a nested property.
    /// Returns 0 if the property is not found or null.
    /// </summary>
    public static long GetLong(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return 0;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return 0;
            return Convert.ToInt64(val);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a nullable long property value, optionally navigating through a nested property.
    /// </summary>
    public static long? GetNullableLong(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return Convert.ToInt64(val);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a byte property value, optionally navigating through a nested property.
    /// Returns null if the property is not found or null.
    /// </summary>
    public static byte? GetByte(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            return Convert.ToByte(val);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets an enum value as string, optionally navigating through a nested property.
    /// </summary>
    public static string? GetEnumString(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely gets the Count of a collection property.
    /// </summary>
    public static int GetCount(dynamic obj, string collectionPropertyName)
    {
        try
        {
            dynamic? collection = GetProperty(obj, collectionPropertyName);
            if (collection == null) return 0;

            // Try Count property first (for ICollection)
            var countProp = collection.GetType().GetProperty("Count");
            if (countProp != null)
            {
                return (int)countProp.GetValue(collection);
            }

            // Try to enumerate
            int count = 0;
            foreach (var _ in collection) count++;
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Safely gets a collection as enumerable of dynamic objects.
    /// </summary>
    public static List<object> GetCollection(object obj, string collectionPropertyName)
    {
        var result = new List<object>();
        try
        {
            dynamic? collection = GetProperty(obj, collectionPropertyName);
            if (collection == null) return result;

            foreach (var item in collection)
            {
                result.Add(item);
            }
        }
        catch
        {
            // Return empty list on error
        }
        return result;
    }

    /// <summary>
    /// Gets the Id property from a dynamic object.
    /// </summary>
    public static int GetId(dynamic obj)
    {
        if (obj == null) return 0;
        try
        {
            // Try direct property access first
            var id = GetProperty(obj, "Id");
            if (id == null) return 0;
            return Convert.ToInt32(id);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a Guid property value, optionally navigating through a nested property.
    /// </summary>
    public static Guid? GetGuid(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            if (val is Guid guidVal) return guidVal;
            string? strVal = val.ToString();
            if (strVal != null && Guid.TryParse(strVal, out Guid parsedGuid)) return parsedGuid;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a TimeSpan property value (for Time fields), optionally navigating through a nested property.
    /// </summary>
    public static TimeSpan? GetTime(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic? val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            if (val == null) return null;
            if (val is TimeSpan timeVal) return timeVal;
            if (val is DateTime dateTimeVal) return dateTimeVal.TimeOfDay;
            string? strVal = val.ToString();
            if (strVal != null && TimeSpan.TryParse(strVal, out TimeSpan parsedTime)) return parsedTime;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely navigates through multiple levels of nested properties.
    /// </summary>
    public static dynamic? Navigate(dynamic obj, params string[] properties)
    {
        try
        {
            dynamic? current = obj;
            foreach (var prop in properties)
            {
                if (current == null) return null;
                current = GetProperty(current, prop);
            }
            return current;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a string value navigating through multiple levels.
    /// </summary>
    public static string? GetNestedString(dynamic obj, params string[] properties)
    {
        try
        {
            var val = Navigate(obj, properties);
            return val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to set a property value on a dynamic object.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public static bool TrySetProperty(dynamic obj, string propertyName, object value)
    {
        if (obj == null) return false;
        try
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                object? convertedValue = value;
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                // Handle specific type conversions
                if (value != null)
                {
                    if (targetType == typeof(DateTime) && value is DateTime)
                    {
                        convertedValue = (DateTime)value;
                    }
                    else if (targetType == typeof(decimal) && value is decimal)
                    {
                        convertedValue = (decimal)value;
                    }
                    else if (targetType == typeof(decimal))
                    {
                        convertedValue = Convert.ToDecimal(value);
                    }
                    else if (targetType == typeof(int))
                    {
                        convertedValue = Convert.ToInt32(value);
                    }
                    else if (targetType == typeof(long))
                    {
                        convertedValue = Convert.ToInt64(value);
                    }
                    else if (targetType == typeof(string))
                    {
                        convertedValue = value.ToString();
                    }
                    else if (targetType == typeof(bool) && value is bool)
                    {
                        convertedValue = (bool)value;
                    }
                    else
                    {
                        convertedValue = Convert.ChangeType(value, targetType);
                    }
                }

                prop.SetValue(obj, convertedValue);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a property exists on a dynamic object.
    /// </summary>
    public static bool HasProperty(dynamic obj, string propertyName)
    {
        if (obj == null) return false;
        try
        {
            var type = obj.GetType();
            return type.GetProperty(propertyName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely gets all items from a manager's Dane.Wszystkie() collection.
    /// Returns empty list if Dane is null (prevents RuntimeBinderException).
    /// </summary>
    public static List<object> SafeGetAll(object manager)
    {
        var result = new List<object>();
        if (manager == null) return result;
        try
        {
            dynamic dynManager = manager;
            var dane = dynManager.Dane;
            if (dane == null) return result;
            foreach (var item in dane.Wszystkie())
            {
                result.Add(item);
            }
        }
        catch
        {
            // Return empty list on any error
        }
        return result;
    }

    /// <summary>
    /// Finds an entity by ID in a manager's Dane.Wszystkie() collection.
    /// Returns null if not found or if Dane is null.
    /// </summary>
    public static dynamic? FindById(object manager, int id)
    {
        if (manager == null) return null;
        try
        {
            dynamic dynManager = manager;
            var dane = dynManager.Dane;
            if (dane == null) return null;
            foreach (var item in dane.Wszystkie())
            {
                if (GetId(item) == id)
                    return item;
            }
        }
        catch
        {
            // Return null on any error
        }
        return null;
    }
}
