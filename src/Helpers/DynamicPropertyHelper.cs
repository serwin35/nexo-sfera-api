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
    public static List<dynamic> GetCollection(dynamic obj, string collectionPropertyName)
    {
        var result = new List<dynamic>();
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
        try
        {
            return (int)obj.Id;
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
            if (Guid.TryParse(val.ToString(), out var parsedGuid)) return parsedGuid;
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
            if (TimeSpan.TryParse(val.ToString(), out var parsedTime)) return parsedTime;
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
}
