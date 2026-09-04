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
    /// Automatically tries through .Dane sub-object if property not found directly
    /// (Nexo SDK entities often wrap data in a .Dane property).
    /// </summary>
    public static dynamic? GetProperty(dynamic obj, string propertyName)
    {
        if (obj == null) return null;
        try
        {
            object objRef = (object)obj;
            var type = objRef.GetType();
            var prop = type.GetProperty(propertyName);
            if (prop != null)
                return prop.GetValue(objRef);

            // Fallback: try through .Dane sub-object (Nexo SDK pattern)
            var daneProp = type.GetProperty("Dane");
            if (daneProp != null)
            {
                var dane = daneProp.GetValue(objRef);
                if (dane != null)
                {
                    var daneType = dane.GetType();
                    var dProp = daneType.GetProperty(propertyName);
                    if (dProp != null)
                        return dProp.GetValue(dane);
                }
            }

            return null;
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
    /// Gets a decimal value trying multiple property names in order.
    /// Returns the first non-zero value found, or 0 if none found.
    /// </summary>
    public static decimal GetDecimalFirstOf(dynamic obj, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var val = GetDecimal(obj, name);
            if (val != 0) return val;
        }
        return 0;
    }

    /// <summary>
    /// Gets a string value trying multiple property name paths in order.
    /// Returns the first non-null value found.
    /// </summary>
    public static string? GetStringFirstOf(dynamic obj, params (string prop1, string? prop2)[] paths)
    {
        foreach (var (prop1, prop2) in paths)
        {
            var val = GetString(obj, prop1, prop2);
            if (val != null) return val;
        }
        return null;
    }

    /// <summary>
    /// Gets the effective data object from an SDK entity.
    /// Nexo SDK business objects wrap data in a .Dane property.
    /// Returns .Dane if available, otherwise returns the object itself.
    /// </summary>
    public static dynamic GetDane(dynamic obj)
    {
        if (obj == null) return obj;
        try
        {
            object objRef = (object)obj;
            var daneProp = objRef.GetType().GetProperty("Dane");
            if (daneProp != null)
            {
                var dane = daneProp.GetValue(objRef);
                if (dane != null) return dane;
            }
        }
        catch { }
        return obj;
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

    #region Units of measure (verified against SDK 61.1.0.9431 metadata)

    /// <summary>
    /// Finds the product's unit of measure (JednostkaMiaryAsortymentu) whose dictionary unit symbol or name
    /// matches <paramref name="unitSymbol"/> (case-insensitive). Searches Asortyment.JednostkiMiar.
    /// Returns null when the product has no such unit or the symbol is empty.
    /// </summary>
    public static dynamic? FindProductUnit(dynamic? asortyment, string? unitSymbol)
    {
        if (asortyment == null || string.IsNullOrWhiteSpace(unitSymbol)) return null;
        var wanted = unitSymbol.Trim();
        try
        {
            var units = GetProperty(asortyment, "JednostkiMiar");
            if (units == null) return null;
            foreach (var jma in units)
            {
                var symbol = GetString(jma, "JednostkaMiary", "Symbol");
                var name = GetString(jma, "JednostkaMiary", "Nazwa");
                if (string.Equals(symbol, wanted, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return jma;
                }
                // dictionary aliases (JednostkaMiary.WszystkieAliasy is a ';'-separated string, Aliasy a collection)
                var aliases = GetString(jma, "JednostkaMiary", "WszystkieAliasy");
                if (!string.IsNullOrEmpty(aliases))
                {
                    foreach (var alias in aliases.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.Equals(alias.Trim(), wanted, StringComparison.OrdinalIgnoreCase)) return jma;
                    }
                }
            }
        }
        catch
        {
            // collection not loaded / unexpected shape
        }
        return null;
    }

    /// <summary>
    /// Symbol of the unit a document line is expressed in (PozycjaDokumentu.JednostkaMiaryAs.JednostkaMiary.Symbol).
    /// </summary>
    public static string? GetLineUnitSymbol(dynamic? pozycja)
    {
        if (pozycja == null) return null;
        var dane = GetDane(pozycja);
        return GetString(GetProperty(dane, "JednostkaMiaryAs"), "JednostkaMiary", "Symbol")
            ?? GetString(GetProperty(pozycja, "JednostkaMiaryAs"), "JednostkaMiary", "Symbol");
    }

    /// <summary>
    /// Symbol of the product's base (stock) unit: Asortyment.PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Symbol
    /// (fallback JednostkaMagazynowa).
    /// </summary>
    public static string? GetProductBaseUnitSymbol(dynamic? asortyment)
    {
        if (asortyment == null) return null;
        return GetString(GetProperty(asortyment, "PodstawowaJednostkaMiaryAsortymentu"), "JednostkaMiary", "Symbol")
            ?? GetString(GetProperty(asortyment, "JednostkaMagazynowa"), "JednostkaMiary", "Symbol");
    }

    #endregion
}
