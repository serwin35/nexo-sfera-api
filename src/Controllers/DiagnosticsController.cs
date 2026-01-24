using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;
using NexoSferaApi.Configuration;
using System.Reflection;
using System.Data;
using Microsoft.Data.SqlClient;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Diagnostic endpoints for exploring Sfera SDK capabilities
/// Provides comprehensive insight into available integration options
/// </summary>
[ApiController]
[Route("api/diagnostics")]
[Authorize]
[Tags("Diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(ISferaService sferaService, ILogger<DiagnosticsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get EF6 configuration diagnostics - check if the SDK's Entity Framework is properly configured
    /// </summary>
    [HttpGet("ef6-config")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<Dictionary<string, object?>>> GetEf6ConfigDiagnostics()
    {
        try
        {
            var diagnostics = EF6Initializer.GetDiagnostics();
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(diagnostics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EF6 config diagnostics");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error getting EF6 diagnostics",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Test BIT type mapping by querying the database directly with ADO.NET
    /// This helps diagnose if the issue is at the ADO.NET level or EF6 level
    /// </summary>
    [HttpGet("test-bit-mapping")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> TestBitTypeMapping()
    {
        try
        {
            var connectionString = _sferaService.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(ApiResponse<Dictionary<string, object?>>.Error("Connection string not available"));
            }

            var results = new Dictionary<string, object?>();

            // Test with Microsoft.Data.SqlClient
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                results["ConnectionState"] = connection.State.ToString();
                results["ServerVersion"] = connection.ServerVersion;

                // First, find the table with the problematic column
                const string findTableQuery = @"
                    SELECT TOP 1
                        TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE COLUMN_NAME = 'CzyPobieracAutomatycznieRachunkiBankowe'";

                string? tableName = null;
                string? schemaName = null;
                await using (var findCmd = new SqlCommand(findTableQuery, connection))
                await using (var findReader = await findCmd.ExecuteReaderAsync())
                {
                    if (await findReader.ReadAsync())
                    {
                        schemaName = findReader.GetString(0);
                        tableName = findReader.GetString(1);
                        results["FoundTable"] = $"{schemaName}.{tableName}";
                        results["ColumnDataType"] = findReader.GetString(3);
                    }
                }

                if (tableName == null)
                {
                    // Fallback: find any table with BIT columns
                    const string findBitQuery = @"
                        SELECT TOP 1 TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE DATA_TYPE = 'bit'
                        ORDER BY TABLE_NAME";

                    await using var bitCmd = new SqlCommand(findBitQuery, connection);
                    await using var bitReader = await bitCmd.ExecuteReaderAsync();
                    if (await bitReader.ReadAsync())
                    {
                        schemaName = bitReader.GetString(0);
                        tableName = bitReader.GetString(1);
                        results["FoundTable"] = $"{schemaName}.{tableName} (fallback)";
                    }
                }

                if (tableName == null)
                {
                    results["Error"] = "No tables with BIT columns found";
                    return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
                }

                // Query the found table
                var query = $@"
                    SELECT TOP 1 *
                    FROM [{schemaName}].[{tableName}]";

                await using var cmd = new SqlCommand(query, connection);
                await using var reader = await cmd.ExecuteReaderAsync();

                var columnInfo = new List<Dictionary<string, object?>>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnInfo.Add(new Dictionary<string, object?>
                    {
                        ["Name"] = reader.GetName(i),
                        ["FieldType"] = reader.GetFieldType(i)?.FullName,
                        ["DataTypeName"] = reader.GetDataTypeName(i)
                    });
                }
                results["ColumnInfo"] = columnInfo;

                if (await reader.ReadAsync())
                {
                    var rowData = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        rowData[reader.GetName(i)] = new Dictionary<string, object?>
                        {
                            ["Value"] = value?.ToString(),
                            ["ClrType"] = value?.GetType().FullName,
                            ["IsDBNull"] = reader.IsDBNull(i)
                        };

                        // Try GetBoolean for BIT columns
                        if (reader.GetDataTypeName(i).ToLower() == "bit" && !reader.IsDBNull(i))
                        {
                            try
                            {
                                var boolValue = reader.GetBoolean(i);
                                ((Dictionary<string, object?>)rowData[reader.GetName(i)]!)["GetBooleanResult"] = boolValue;
                            }
                            catch (Exception ex)
                            {
                                ((Dictionary<string, object?>)rowData[reader.GetName(i)]!)["GetBooleanError"] = ex.Message;
                            }
                        }
                    }
                    results["SampleRow"] = rowData;
                }
                else
                {
                    results["SampleRow"] = $"No data in {schemaName}.{tableName} table";
                }
            }

            results["Conclusion"] = "If FieldType shows System.Boolean and GetBooleanResult works, " +
                                   "the issue is in EF6 type mapping, not ADO.NET.";

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing BIT type mapping");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error testing BIT type mapping",
                new List<string> { ex.Message, ex.InnerException?.Message ?? "" }));
        }
    }

    /// <summary>
    /// Get comprehensive SDK structure - all assemblies, types, managers, and schemas
    /// This is the main diagnostic endpoint for understanding Sfera integration capabilities
    /// </summary>
    [HttpGet("sdk-structure")]
    [ProducesResponseType(typeof(ApiResponse<SdkStructureResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SdkStructureResponse>> GetSdkStructure(
        [FromQuery] bool includeProperties = true,
        [FromQuery] bool includeMethods = true,
        [FromQuery] bool includeManagerSchemas = true,
        [FromQuery] int maxEntitiesPerManager = 1)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var response = new SdkStructureResponse();

            // 1. Discover all InsERT assemblies
            response.Assemblies = DiscoverInsertAssemblies();

            // 2. Analyze Sfera (Uchwyt) object
            response.SferaObject = AnalyzeSferaObject(sfera, includeProperties, includeMethods);

            // 3. Get available managers and their schemas
            response.Managers = DiscoverManagers(includeManagerSchemas, maxEntitiesPerManager);

            // 4. Discover available services/interfaces
            response.AvailableServices = DiscoverAvailableServices();

            // 5. Get extension method namespaces
            response.ExtensionNamespaces = new List<string>
            {
                "InsERT.Moria.Asortymenty",
                "InsERT.Moria.Klienci",
                "InsERT.Moria.Dokumenty",
                "InsERT.Moria.Logistyka"
            };

            return Ok(ApiResponse<SdkStructureResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SDK structure");
            return StatusCode(500, ApiResponse<SdkStructureResponse>.Error(
                "Error analyzing SDK structure",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get detailed information about a specific manager
    /// </summary>
    [HttpGet("manager/{managerName}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerDetailResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ManagerDetailResponse>> GetManagerDetails(
        string managerName,
        [FromQuery] int sampleEntities = 3)
    {
        try
        {
            var manager = _sferaService.GetManager(managerName);
            if (manager == null)
            {
                return NotFound(ApiResponse<ManagerDetailResponse>.Error($"Manager '{managerName}' not found"));
            }

            var response = new ManagerDetailResponse
            {
                Name = managerName,
                ManagerType = ((object)manager).GetType().FullName,
                Properties = new List<PropertyInfo_>(),
                Methods = new List<MethodInfo_>(),
                EntitySchema = new EntitySchema(),
                SampleEntities = new List<Dictionary<string, object?>>()
            };

            // Analyze manager object
            var managerType = ((object)manager).GetType();

            foreach (var prop in managerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                response.Properties.Add(new PropertyInfo_
                {
                    Name = prop.Name,
                    Type = prop.PropertyType.Name,
                    CanRead = prop.CanRead,
                    CanWrite = prop.CanWrite
                });
            }

            foreach (var method in managerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName) continue;
                response.Methods.Add(new MethodInfo_
                {
                    Name = method.Name,
                    ReturnType = method.ReturnType.Name,
                    Parameters = method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList()
                });
            }

            // Get entity schema from first entity
            try
            {
                var dane = DynamicPropertyHelper.GetProperty(manager, "Dane");
                if (dane != null)
                {
                    dynamic? wszystkie = dane.Wszystkie();
                    if (wszystkie != null)
                    {
                        int count = 0;
                        foreach (var entity in wszystkie)
                        {
                            if (count == 0)
                            {
                                // Get schema from first entity
                                response.EntitySchema = GetEntitySchema(entity);
                            }

                            if (count < sampleEntities)
                            {
                                // Get sample entity data
                                response.SampleEntities.Add(GetEntityData(entity, response.EntitySchema));
                            }
                            count++;

                            if (count >= sampleEntities) break;
                        }
                        response.TotalEntityCount = count;

                        // Get actual count
                        try
                        {
                            int totalCount = 0;
                            foreach (var _ in wszystkie)
                            {
                                totalCount++;
                            }
                            response.TotalEntityCount = totalCount;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                response.SchemaError = ex.Message;
            }

            return Ok(ApiResponse<ManagerDetailResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manager details for {ManagerName}", managerName);
            return StatusCode(500, ApiResponse<ManagerDetailResponse>.Error(
                $"Error analyzing manager '{managerName}'",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Explore an entity by ID from a specific manager
    /// </summary>
    [HttpGet("manager/{managerName}/entity/{id}")]
    [ProducesResponseType(typeof(ApiResponse<EntityExplorerResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<EntityExplorerResponse>> ExploreEntity(
        string managerName,
        int id,
        [FromQuery] bool deep = false)
    {
        try
        {
            var manager = _sferaService.GetManager(managerName);
            if (manager == null)
            {
                return NotFound(ApiResponse<EntityExplorerResponse>.Error($"Manager '{managerName}' not found"));
            }

            dynamic? entity = null;
            var dane = DynamicPropertyHelper.GetProperty(manager, "Dane");
            if (dane != null)
            {
                // Try Znajdz method first
                try
                {
                    entity = dane.Znajdz(id);
                }
                catch
                {
                    // Fallback to iteration
                    dynamic? wszystkie = dane.Wszystkie();
                    if (wszystkie != null)
                    {
                        foreach (var e in wszystkie)
                        {
                            if (DynamicPropertyHelper.GetId(e) == id)
                            {
                                entity = e;
                                break;
                            }
                        }
                    }
                }
            }

            if (entity == null)
            {
                return NotFound(ApiResponse<EntityExplorerResponse>.Error($"Entity with ID {id} not found in {managerName}"));
            }

            var response = new EntityExplorerResponse
            {
                ManagerName = managerName,
                EntityId = id,
                EntityType = ((object)entity).GetType().FullName,
                Schema = GetEntitySchema(entity),
                Data = new Dictionary<string, object?>(),
                NestedObjects = new Dictionary<string, object?>(),
                Collections = new Dictionary<string, List<Dictionary<string, object?>>>()
            };

            // Get all property values
            var entityType = ((object)entity).GetType();
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var value = prop.GetValue(entity);

                    if (value == null)
                    {
                        response.Data[prop.Name] = null;
                    }
                    else if (IsSimpleType(prop.PropertyType))
                    {
                        response.Data[prop.Name] = value;
                    }
                    else if (deep)
                    {
                        // Handle complex types and collections
                        if (IsCollection(prop.PropertyType))
                        {
                            var items = new List<Dictionary<string, object?>>();
                            try
                            {
                                foreach (var item in (dynamic)value)
                                {
                                    var itemData = new Dictionary<string, object?>();
                                    var itemType = ((object)item).GetType();
                                    foreach (var itemProp in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        try
                                        {
                                            if (IsSimpleType(itemProp.PropertyType))
                                            {
                                                itemData[itemProp.Name] = itemProp.GetValue(item);
                                            }
                                        }
                                        catch { }
                                    }
                                    if (itemData.Any())
                                        items.Add(itemData);
                                    if (items.Count >= 5) break;
                                }
                            }
                            catch { }
                            if (items.Any())
                                response.Collections[prop.Name] = items;
                        }
                        else
                        {
                            // Nested object
                            var nestedData = new Dictionary<string, object?>();
                            var nestedType = value.GetType();
                            foreach (var nestedProp in nestedType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                try
                                {
                                    if (IsSimpleType(nestedProp.PropertyType))
                                    {
                                        nestedData[nestedProp.Name] = nestedProp.GetValue(value);
                                    }
                                }
                                catch { }
                            }
                            if (nestedData.Any())
                                response.NestedObjects[prop.Name] = nestedData;
                        }
                    }
                    else
                    {
                        response.Data[prop.Name] = $"[{prop.PropertyType.Name}]";
                    }
                }
                catch (Exception ex)
                {
                    response.Data[prop.Name] = $"[Error: {ex.Message}]";
                }
            }

            return Ok(ApiResponse<EntityExplorerResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exploring entity {Id} in {ManagerName}", id, managerName);
            return StatusCode(500, ApiResponse<EntityExplorerResponse>.Error(
                $"Error exploring entity",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Discover available types/interfaces that can be retrieved via PodajObiektTypu
    /// </summary>
    [HttpGet("services")]
    [ProducesResponseType(typeof(ApiResponse<List<ServiceInfo>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<ServiceInfo>>> GetAvailableServices()
    {
        try
        {
            var services = DiscoverAvailableServices();
            return Ok(ApiResponse<List<ServiceInfo>>.Ok(services));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering services");
            return StatusCode(500, ApiResponse<List<ServiceInfo>>.Error(
                "Error discovering services",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Try to instantiate a service by type name
    /// </summary>
    [HttpGet("services/try/{typeName}")]
    [ProducesResponseType(typeof(ApiResponse<ServiceInstanceResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ServiceInstanceResponse>> TryGetService(string typeName)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var response = new ServiceInstanceResponse { TypeName = typeName };

            // Find the type
            Type? serviceType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    serviceType = asm.GetType(typeName);
                    if (serviceType != null)
                    {
                        response.AssemblyName = asm.GetName().Name;
                        break;
                    }
                }
                catch { }
            }

            if (serviceType == null)
            {
                response.Success = false;
                response.Error = "Type not found in any loaded assembly";
                return Ok(ApiResponse<ServiceInstanceResponse>.Ok(response));
            }

            response.TypeFound = true;
            response.IsInterface = serviceType.IsInterface;
            response.BaseType = serviceType.BaseType?.Name;

            // Try to get instance via PodajObiektTypu
            try
            {
                var sferaType = sfera.GetType();
                MethodInfo? genericMethod = null;
                foreach (var m in sferaType.GetMethods())
                {
                    if (m.Name == "PodajObiektTypu" && m.IsGenericMethod && m.GetParameters().Length == 0)
                    {
                        genericMethod = m;
                        break;
                    }
                }

                if (genericMethod != null)
                {
                    var concreteMethod = genericMethod.MakeGenericMethod(serviceType);
                    var instance = concreteMethod.Invoke(sfera, null);

                    if (instance != null)
                    {
                        response.Success = true;
                        response.InstanceType = instance.GetType().FullName;
                        response.Methods = new List<MethodInfo_>();
                        response.Properties = new List<PropertyInfo_>();

                        var instanceType = instance.GetType();
                        foreach (var prop in instanceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            response.Properties.Add(new PropertyInfo_
                            {
                                Name = prop.Name,
                                Type = prop.PropertyType.Name,
                                CanRead = prop.CanRead,
                                CanWrite = prop.CanWrite
                            });
                        }

                        foreach (var method in instanceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            if (method.IsSpecialName) continue;
                            response.Methods.Add(new MethodInfo_
                            {
                                Name = method.Name,
                                ReturnType = method.ReturnType.Name,
                                Parameters = method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList()
                            });
                        }
                    }
                    else
                    {
                        response.Success = false;
                        response.Error = "PodajObiektTypu returned null";
                    }
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.InnerException?.Message ?? ex.Message;
            }

            return Ok(ApiResponse<ServiceInstanceResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trying service {TypeName}", typeName);
            return StatusCode(500, ApiResponse<ServiceInstanceResponse>.Error(
                $"Error trying service '{typeName}'",
                new List<string> { ex.Message }));
        }
    }

    #region Helper Methods

    private List<AssemblyInfo> DiscoverInsertAssemblies()
    {
        var assemblies = new List<AssemblyInfo>();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var name = asm.GetName().Name ?? "";
                if (name.StartsWith("InsERT") || name.Contains("Moria") || name.Contains("Mox"))
                {
                    var info = new AssemblyInfo
                    {
                        Name = name,
                        Version = asm.GetName().Version?.ToString(),
                        PublicTypes = new List<string>(),
                        Interfaces = new List<string>()
                    };

                    try
                    {
                        foreach (var type in asm.GetExportedTypes())
                        {
                            if (type.IsInterface)
                            {
                                info.Interfaces.Add(type.FullName ?? type.Name);
                            }
                            else if (type.IsPublic && !type.IsAbstract)
                            {
                                info.PublicTypes.Add(type.FullName ?? type.Name);
                            }
                        }
                    }
                    catch { }

                    assemblies.Add(info);
                }
            }
            catch { }
        }

        return assemblies.OrderBy(a => a.Name).ToList();
    }

    private SferaObjectInfo AnalyzeSferaObject(dynamic sfera, bool includeProperties, bool includeMethods)
    {
        var info = new SferaObjectInfo
        {
            Type = ((object)sfera).GetType().FullName,
            Properties = new List<PropertyInfo_>(),
            Methods = new List<MethodInfo_>(),
            ExtensionMethods = new List<string>()
        };

        var sferaType = ((object)sfera).GetType();

        if (includeProperties)
        {
            foreach (var prop in sferaType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                info.Properties.Add(new PropertyInfo_
                {
                    Name = prop.Name,
                    Type = prop.PropertyType.Name,
                    CanRead = prop.CanRead,
                    CanWrite = prop.CanWrite
                });
            }
        }

        if (includeMethods)
        {
            foreach (var method in sferaType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsSpecialName) continue;
                if (method.DeclaringType == typeof(object)) continue;

                info.Methods.Add(new MethodInfo_
                {
                    Name = method.Name,
                    ReturnType = method.ReturnType.Name,
                    Parameters = method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList(),
                    IsGeneric = method.IsGenericMethod
                });
            }
        }

        // Known extension methods
        info.ExtensionMethods = new List<string>
        {
            "Asortymenty()", "SzablonyAsortymentu()",
            "Podmioty()",
            "Dokumenty()", "DokumentySprzedazy()", "DokumentyZakupu()", "DokumentyElektroniczne()",
            "KorektyDokumentowSprzedazy()", "KorektyDokumentowZakupu()",
            "Magazyny()", "WydaniaZewnetrzne()", "PrzyjeciaZewnetrzne()", "WydaniaMiedzymagazynowe()",
            "RozchodyWewnetrzne()", "ZamowieniaOdKlientow()", "ZamowieniaDoDostawcow()", "Oferty()"
        };

        return info;
    }

    private List<ManagerInfo> DiscoverManagers(bool includeSchemas, int maxEntities)
    {
        var managers = new List<ManagerInfo>();
        var managerNames = new[]
        {
            "Asortymenty", "SzablonyAsortymentu",
            "Podmioty",
            "Dokumenty", "DokumentySprzedazy", "DokumentyZakupu", "DokumentyElektroniczne",
            "KorektyDokumentowSprzedazy", "KorektyDokumentowZakupu",
            "Magazyny", "WydaniaZewnetrzne", "PrzyjeciaZewnetrzne", "WydaniaMiedzymagazynowe",
            "RozchodyWewnetrzne", "ZamowieniaOdKlientow", "ZamowieniaDoDostawcow", "Oferty"
        };

        foreach (var name in managerNames)
        {
            var info = new ManagerInfo { Name = name };

            try
            {
                var manager = _sferaService.GetManager(name);
                if (manager != null)
                {
                    info.Available = true;
                    info.ManagerType = ((object)manager).GetType().FullName;

                    if (includeSchemas)
                    {
                        try
                        {
                            var dane = DynamicPropertyHelper.GetProperty(manager, "Dane");
                            if (dane != null)
                            {
                                dynamic? wszystkie = dane.Wszystkie();
                                if (wszystkie != null)
                                {
                                    int count = 0;
                                    foreach (var entity in wszystkie)
                                    {
                                        if (count == 0)
                                        {
                                            info.EntitySchema = GetEntitySchema(entity);
                                            info.EntityType = ((object)entity).GetType().FullName;
                                        }
                                        count++;
                                        if (count > 1000) break; // Cap counting at 1000
                                    }
                                    info.EntityCount = count > 1000 ? "1000+" : count.ToString();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            info.SchemaError = ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                info.Available = false;
                info.Error = ex.Message;
            }

            managers.Add(info);
        }

        return managers;
    }

    private List<ServiceInfo> DiscoverAvailableServices()
    {
        var services = new List<ServiceInfo>();

        // Known service interfaces
        var knownServices = new[]
        {
            "InsERT.Moria.EgzekutorMagazynowy.IMagazynier",
            "InsERT.Moria.Sfera.IKontekstPracy",
            "InsERT.Moria.Sfera.IInformacjeOFirmie",
            "InsERT.Moria.Sfera.IOperator",
            "InsERT.Moria.Konfiguracja.IKonfiguracja",
            "InsERT.Moria.Cenniki.ICennikiManager",
            "InsERT.Moria.Cenniki.IPolitykaCenowa",
            "InsERT.Moria.Rabaty.IRabatyManager",
            "InsERT.Moria.Slowniki.ISlowniki",
            "InsERT.Moria.Waluty.IKursyWalut",
            "InsERT.Moria.ModelDanych.IDaneWydrukow",
            "InsERT.Moria.Stawki.IStawkiVAT",
            "InsERT.Moria.Jednostki.IJednostki",
            "InsERT.Moria.Ksef.IKsefService",
            "InsERT.Moria.Ksef.IKonfiguratorKsef"
        };

        foreach (var typeName in knownServices)
        {
            var info = new ServiceInfo { TypeName = typeName };

            // Check if type exists
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(typeName);
                    if (type != null)
                    {
                        info.Found = true;
                        info.AssemblyName = asm.GetName().Name;
                        info.IsInterface = type.IsInterface;

                        // List interface members
                        if (type.IsInterface)
                        {
                            info.Members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                .Where(m => m.MemberType == MemberTypes.Method || m.MemberType == MemberTypes.Property)
                                .Select(m => m.MemberType == MemberTypes.Method
                                    ? $"{((MethodInfo)m).ReturnType.Name} {m.Name}()"
                                    : $"{((PropertyInfo)m).PropertyType.Name} {m.Name}")
                                .Take(20)
                                .ToList();
                        }
                        break;
                    }
                }
                catch { }
            }

            services.Add(info);
        }

        return services;
    }

    private EntitySchema GetEntitySchema(dynamic entity)
    {
        var schema = new EntitySchema
        {
            Properties = new List<PropertySchemaInfo>()
        };

        var entityType = ((object)entity).GetType();
        schema.TypeName = entityType.FullName;

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propInfo = new PropertySchemaInfo
            {
                Name = prop.Name,
                Type = prop.PropertyType.Name,
                FullType = prop.PropertyType.FullName,
                IsCollection = IsCollection(prop.PropertyType),
                IsComplex = !IsSimpleType(prop.PropertyType) && !IsCollection(prop.PropertyType)
            };
            schema.Properties.Add(propInfo);
        }

        return schema;
    }

    private Dictionary<string, object?> GetEntityData(dynamic entity, EntitySchema schema)
    {
        var data = new Dictionary<string, object?>();
        var entityType = ((object)entity).GetType();

        foreach (var propSchema in schema.Properties)
        {
            try
            {
                if (propSchema.Name == null) continue;
                var prop = entityType.GetProperty(propSchema.Name);
                if (prop != null)
                {
                    var value = prop.GetValue(entity);
                    if (value == null)
                    {
                        data[propSchema.Name] = null;
                    }
                    else if (IsSimpleType(prop.PropertyType))
                    {
                        data[propSchema.Name] = value;
                    }
                    else if (propSchema.IsCollection)
                    {
                        data[propSchema.Name] = $"[Collection]";
                    }
                    else
                    {
                        data[propSchema.Name] = $"[{prop.PropertyType.Name}]";
                    }
                }
            }
            catch
            {
                if (propSchema.Name != null)
                    data[propSchema.Name] = "[Error]";
            }
        }

        return data;
    }

    private static bool IsSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsPrimitive
            || underlyingType == typeof(string)
            || underlyingType == typeof(decimal)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(DateTimeOffset)
            || underlyingType == typeof(TimeSpan)
            || underlyingType == typeof(Guid)
            || underlyingType.IsEnum;
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) &&
               typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    #endregion
}

#region Response DTOs

public class SdkStructureResponse
{
    public List<AssemblyInfo> Assemblies { get; set; } = new();
    public SferaObjectInfo SferaObject { get; set; } = new();
    public List<ManagerInfo> Managers { get; set; } = new();
    public List<ServiceInfo> AvailableServices { get; set; } = new();
    public List<string> ExtensionNamespaces { get; set; } = new();
}

public class AssemblyInfo
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public List<string> PublicTypes { get; set; } = new();
    public List<string> Interfaces { get; set; } = new();
}

public class SferaObjectInfo
{
    public string? Type { get; set; }
    public List<PropertyInfo_> Properties { get; set; } = new();
    public List<MethodInfo_> Methods { get; set; } = new();
    public List<string> ExtensionMethods { get; set; } = new();
}

public class PropertyInfo_
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
}

public class MethodInfo_
{
    public string? Name { get; set; }
    public string? ReturnType { get; set; }
    public List<string> Parameters { get; set; } = new();
    public bool IsGeneric { get; set; }
}

public class ManagerInfo
{
    public string? Name { get; set; }
    public bool Available { get; set; }
    public string? ManagerType { get; set; }
    public string? EntityType { get; set; }
    public string? EntityCount { get; set; }
    public EntitySchema? EntitySchema { get; set; }
    public string? SchemaError { get; set; }
    public string? Error { get; set; }
}

public class EntitySchema
{
    public string? TypeName { get; set; }
    public List<PropertySchemaInfo> Properties { get; set; } = new();
}

public class PropertySchemaInfo
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? FullType { get; set; }
    public bool IsCollection { get; set; }
    public bool IsComplex { get; set; }
}

public class ServiceInfo
{
    public string? TypeName { get; set; }
    public bool Found { get; set; }
    public string? AssemblyName { get; set; }
    public bool IsInterface { get; set; }
    public List<string> Members { get; set; } = new();
}

public class ManagerDetailResponse
{
    public string? Name { get; set; }
    public string? ManagerType { get; set; }
    public List<PropertyInfo_> Properties { get; set; } = new();
    public List<MethodInfo_> Methods { get; set; } = new();
    public EntitySchema EntitySchema { get; set; } = new();
    public List<Dictionary<string, object?>> SampleEntities { get; set; } = new();
    public int TotalEntityCount { get; set; }
    public string? SchemaError { get; set; }
}

public class EntityExplorerResponse
{
    public string? ManagerName { get; set; }
    public int EntityId { get; set; }
    public string? EntityType { get; set; }
    public EntitySchema Schema { get; set; } = new();
    public Dictionary<string, object?> Data { get; set; } = new();
    public Dictionary<string, object?> NestedObjects { get; set; } = new();
    public Dictionary<string, List<Dictionary<string, object?>>> Collections { get; set; } = new();
}

public class ServiceInstanceResponse
{
    public string? TypeName { get; set; }
    public bool TypeFound { get; set; }
    public string? AssemblyName { get; set; }
    public bool IsInterface { get; set; }
    public string? BaseType { get; set; }
    public bool Success { get; set; }
    public string? InstanceType { get; set; }
    public List<PropertyInfo_> Properties { get; set; } = new();
    public List<MethodInfo_> Methods { get; set; } = new();
    public string? Error { get; set; }
}

#endregion
