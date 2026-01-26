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
    /// Investigate TransakcjaVAT table schema and IdWInstancji column type
    /// This diagnoses the "could not be set to Byte[]" error
    /// </summary>
    [HttpGet("transakcja-vat-schema")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> InvestigateTransakcjaVatSchema()
    {
        try
        {
            var connectionString = _sferaService.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(ApiResponse<Dictionary<string, object?>>.Error("Connection string not available"));
            }

            var results = new Dictionary<string, object?>();
            string? targetSchema = null;
            string? targetTable = null;

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                results["ConnectionState"] = connection.State.ToString();

                // 1. First find tables containing 'VAT' or 'Transakcja'
                const string findTablesQuery = @"
                    SELECT DISTINCT TABLE_SCHEMA, TABLE_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME LIKE '%VAT%' OR TABLE_NAME LIKE '%Transakcja%'
                    ORDER BY TABLE_NAME";

                var vatTables = new List<string>();
                await using (var findCmd = new SqlCommand(findTablesQuery, connection))
                await using (var findReader = await findCmd.ExecuteReaderAsync())
                {
                    while (await findReader.ReadAsync())
                    {
                        vatTables.Add($"{findReader.GetString(0)}.{findReader.GetString(1)}");
                    }
                }
                results["VATRelatedTables"] = vatTables;

                // 2. Find tables with IdWInstancji column
                const string findIdWInstancjiQuery = @"
                    SELECT TABLE_SCHEMA, TABLE_NAME, DATA_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE COLUMN_NAME = 'IdWInstancji'
                    ORDER BY TABLE_NAME";

                var tablesWithIdWInstancji = new List<Dictionary<string, object?>>();
                await using (var idCmd = new SqlCommand(findIdWInstancjiQuery, connection))
                await using (var idReader = await idCmd.ExecuteReaderAsync())
                {
                    while (await idReader.ReadAsync())
                    {
                        tablesWithIdWInstancji.Add(new Dictionary<string, object?>
                        {
                            ["Schema"] = idReader.GetString(0),
                            ["Table"] = idReader.GetString(1),
                            ["IdWInstancji_DataType"] = idReader.GetString(2)
                        });
                    }
                }
                results["TablesWithIdWInstancji"] = tablesWithIdWInstancji;

                // Use first VAT-related table with IdWInstancji, or first table with IdWInstancji
                var vatTableWithId = tablesWithIdWInstancji.FirstOrDefault(t =>
                    t["Table"]?.ToString()?.Contains("VAT", StringComparison.OrdinalIgnoreCase) == true ||
                    t["Table"]?.ToString()?.Contains("Transakcja", StringComparison.OrdinalIgnoreCase) == true);

                if (vatTableWithId != null)
                {
                    targetSchema = vatTableWithId["Schema"]?.ToString();
                    targetTable = vatTableWithId["Table"]?.ToString();
                }
                else if (tablesWithIdWInstancji.Any())
                {
                    targetSchema = tablesWithIdWInstancji[0]["Schema"]?.ToString();
                    targetTable = tablesWithIdWInstancji[0]["Table"]?.ToString();
                }

                results["TargetTable"] = targetTable != null ? $"{targetSchema}.{targetTable}" : "No suitable table found";

                if (targetTable == null)
                {
                    results["Error"] = "No table with IdWInstancji column found";
                    return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
                }

                // 3. Get target table schema
                var schemaQuery = $@"
                    SELECT
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.CHARACTER_MAXIMUM_LENGTH,
                        c.NUMERIC_PRECISION,
                        c.IS_NULLABLE,
                        c.COLUMN_DEFAULT
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    WHERE c.TABLE_SCHEMA = '{targetSchema}' AND c.TABLE_NAME = '{targetTable}'
                    ORDER BY c.ORDINAL_POSITION";

                var columns = new List<Dictionary<string, object?>>();
                await using (var cmd = new SqlCommand(schemaQuery, connection))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add(new Dictionary<string, object?>
                        {
                            ["ColumnName"] = reader.GetString(0),
                            ["DataType"] = reader.GetString(1),
                            ["MaxLength"] = reader.IsDBNull(2) ? null : reader.GetValue(2),
                            ["Precision"] = reader.IsDBNull(3) ? null : reader.GetValue(3),
                            ["IsNullable"] = reader.GetString(4),
                            ["DefaultValue"] = reader.IsDBNull(5) ? null : reader.GetValue(5)
                        });
                    }
                }
                results["TargetTableColumns"] = columns;

                // 4. Find IdWInstancji specifically
                var idWInstancjiCol = columns.FirstOrDefault(c =>
                    c["ColumnName"]?.ToString()?.Equals("IdWInstancji", StringComparison.OrdinalIgnoreCase) == true);
                results["IdWInstancjiColumn"] = idWInstancjiCol;

                // 5. Try to read actual data from the target table
                var dataQuery = $@"SELECT TOP 1 * FROM [{targetSchema}].[{targetTable}]";
                await using (var dataCmd = new SqlCommand(dataQuery, connection))
                await using (var dataReader = await dataCmd.ExecuteReaderAsync())
                {
                    var columnTypes = new List<Dictionary<string, object?>>();
                    for (int i = 0; i < dataReader.FieldCount; i++)
                    {
                        columnTypes.Add(new Dictionary<string, object?>
                        {
                            ["Name"] = dataReader.GetName(i),
                            ["FieldType"] = dataReader.GetFieldType(i)?.FullName,
                            ["DataTypeName"] = dataReader.GetDataTypeName(i)
                        });
                    }
                    results["ADO_ColumnTypes"] = columnTypes;

                    // Find IdWInstancji in ADO.NET results
                    var idWInstancjiIdx = -1;
                    for (int i = 0; i < dataReader.FieldCount; i++)
                    {
                        if (dataReader.GetName(i).Equals("IdWInstancji", StringComparison.OrdinalIgnoreCase))
                        {
                            idWInstancjiIdx = i;
                            break;
                        }
                    }

                    if (idWInstancjiIdx >= 0 && await dataReader.ReadAsync())
                    {
                        var idValue = dataReader.GetValue(idWInstancjiIdx);
                        results["IdWInstancji_Value"] = idValue?.ToString();
                        results["IdWInstancji_CLRType"] = idValue?.GetType().FullName;
                        results["IdWInstancji_IsDBNull"] = dataReader.IsDBNull(idWInstancjiIdx);

                        // Try getting as Int32
                        try
                        {
                            var intValue = dataReader.GetInt32(idWInstancjiIdx);
                            results["IdWInstancji_GetInt32"] = intValue;
                        }
                        catch (Exception ex)
                        {
                            results["IdWInstancji_GetInt32Error"] = ex.Message;
                        }
                    }
                    else
                    {
                        results["IdWInstancji_Note"] = idWInstancjiIdx < 0
                            ? "Column IdWInstancji not found in result set"
                            : "No data in TransakcjaVAT table";
                    }
                }

                // 6. Check for any uniqueidentifier or binary columns that could cause issues
                var binaryQuery = $@"
                    SELECT COLUMN_NAME, DATA_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = '{targetSchema}' AND TABLE_NAME = '{targetTable}'
                    AND DATA_TYPE IN ('binary', 'varbinary', 'uniqueidentifier', 'timestamp', 'rowversion')";

                var binaryColumns = new List<string>();
                await using (var binCmd = new SqlCommand(binaryQuery, connection))
                await using (var binReader = await binCmd.ExecuteReaderAsync())
                {
                    while (await binReader.ReadAsync())
                    {
                        binaryColumns.Add($"{binReader.GetString(0)} ({binReader.GetString(1)})");
                    }
                }
                results["BinaryTypeColumns"] = binaryColumns;
            }

            // 7. Compare with System.Data.SqlClient provider
            try
            {
                await using var sysConnection = new System.Data.SqlClient.SqlConnection(connectionString);
                await sysConnection.OpenAsync();

                var sysDataQuery = $@"SELECT TOP 1 IdWInstancji FROM [{targetSchema}].[{targetTable}]";
                await using var sysCmd = new System.Data.SqlClient.SqlCommand(sysDataQuery, sysConnection);
                await using var sysReader = await sysCmd.ExecuteReaderAsync();

                if (await sysReader.ReadAsync())
                {
                    var sysValue = sysReader.GetValue(0);
                    results["SystemDataSqlClient_Value"] = sysValue?.ToString();
                    results["SystemDataSqlClient_CLRType"] = sysValue?.GetType().FullName;
                    results["SystemDataSqlClient_FieldType"] = sysReader.GetFieldType(0)?.FullName;
                }
            }
            catch (Exception sysEx)
            {
                results["SystemDataSqlClient_Error"] = sysEx.Message;
            }

            results["Diagnosis"] = "Compare Microsoft.Data.SqlClient vs System.Data.SqlClient results. " +
                "If IdWInstancji shows as INT in schema but CLRType is Byte[], " +
                "the issue is in EF6 model mapping or provider type conversion, not database schema.";

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error investigating TransakcjaVAT schema");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error investigating TransakcjaVAT schema",
                new List<string> { ex.Message, ex.InnerException?.Message ?? "" }));
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

    /// <summary>
    /// Test PZ (Przyjęcie Zewnętrzne) document creation to diagnose TransakcjaVAT EF6 errors
    /// This endpoint attempts document creation without saving to identify where errors occur
    /// </summary>
    [HttpGet("test-pz-creation")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> TestPzCreation()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var testResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var stepResults = new Dictionary<string, object?>();

                // Step 1: Get PrzyjeciaZewnetrzne manager
                stepResults["Step1_GetManager"] = "Starting...";
                try
                {
                    var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                    stepResults["Step1_GetManager"] = przyjecia != null ? "SUCCESS" : "NULL_MANAGER";
                    stepResults["ManagerType"] = przyjecia?.GetType().FullName;

                    if (przyjecia == null)
                    {
                        stepResults["Error"] = "Could not get PrzyjeciaZewnetrzne manager";
                        return stepResults;
                    }

                    // Step 2: Get PZ configuration
                    stepResults["Step2_GetConfig"] = "Starting...";
                    try
                    {
                        var konfiguracje = _sferaService.GetManager("Konfiguracje");
                        if (konfiguracje?.DaneDomyslne != null)
                        {
                            var pzConfig = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzne;
                            stepResults["Step2_GetConfig"] = pzConfig != null ? "SUCCESS" : "NULL_CONFIG";
                            stepResults["ConfigSymbol"] = pzConfig?.Symbol?.ToString();
                            stepResults["ConfigId"] = pzConfig?.Id?.ToString();

                            // Check if config has financial aspect
                            try
                            {
                                stepResults["ConfigPosiadaAspektFinansowy"] = pzConfig?.PosiadaAspektFinansowy?.ToString();
                            }
                            catch { stepResults["ConfigPosiadaAspektFinansowy"] = "CANNOT_READ"; }
                        }
                    }
                    catch (Exception configEx)
                    {
                        stepResults["Step2_GetConfig"] = "ERROR: " + configEx.Message;
                    }

                    // Step 3: Try to create PZ using Utworz(Konfiguracja)
                    stepResults["Step3_CreateWithConfig"] = "Starting...";
                    try
                    {
                        var konfiguracje = _sferaService.GetManager("Konfiguracje");
                        var pzConfig = konfiguracje?.DaneDomyslne?.PrzyjecieZewnetrzne;
                        if (pzConfig != null)
                        {
                            using (var pz = przyjecia.Utworz(pzConfig))
                            {
                                stepResults["Step3_CreateWithConfig"] = "SUCCESS";
                                stepResults["DocumentType"] = pz?.GetType().FullName;

                                // Step 4: Try to set PosiadaAspektFinansowy
                                stepResults["Step4_SetAspektFinansowy"] = "Starting...";
                                try
                                {
                                    pz.Dane.PosiadaAspektFinansowy = false;
                                    stepResults["Step4_SetAspektFinansowy"] = "SUCCESS";
                                }
                                catch (Exception setEx)
                                {
                                    stepResults["Step4_SetAspektFinansowy"] = "ERROR: " + setEx.Message;
                                }

                                // Step 5: Try to reserve number (this might trigger VAT loading)
                                stepResults["Step5_ReserveNumber"] = "Starting...";
                                try
                                {
                                    pz.ZarezerwujNumer();
                                    var numberPreview = pz.PodajPodgladNumeru()?.ToString();
                                    stepResults["Step5_ReserveNumber"] = "SUCCESS";
                                    stepResults["ReservedNumber"] = numberPreview;
                                }
                                catch (Exception numEx)
                                {
                                    stepResults["Step5_ReserveNumber"] = "ERROR: " + numEx.Message;
                                    if (numEx.ToString().Contains("TransakcjaVAT"))
                                    {
                                        stepResults["TransakcjaVAT_Error"] = true;
                                        stepResults["TransakcjaVAT_FullError"] = numEx.ToString().Substring(0, Math.Min(2000, numEx.ToString().Length));
                                    }
                                }

                                // Don't save - just testing creation
                                stepResults["TestComplete"] = true;
                            }
                        }
                        else
                        {
                            stepResults["Step3_CreateWithConfig"] = "SKIPPED - no config";
                        }
                    }
                    catch (Exception createEx)
                    {
                        stepResults["Step3_CreateWithConfig"] = "ERROR: " + createEx.Message;
                        if (createEx.ToString().Contains("TransakcjaVAT") || createEx.ToString().Contains("IdWInstancji"))
                        {
                            stepResults["TransakcjaVAT_Error"] = true;
                            stepResults["TransakcjaVAT_FullError"] = createEx.ToString().Substring(0, Math.Min(2000, createEx.ToString().Length));
                        }

                        // Step 3b: Fallback to UtworzPrzyjecieZewnetrzne
                        stepResults["Step3b_FallbackCreate"] = "Starting...";
                        try
                        {
                            using (var pz = przyjecia.UtworzPrzyjecieZewnetrzne())
                            {
                                stepResults["Step3b_FallbackCreate"] = "SUCCESS";
                                stepResults["FallbackDocumentType"] = pz?.GetType().FullName;
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            stepResults["Step3b_FallbackCreate"] = "ERROR: " + fallbackEx.Message;
                            if (fallbackEx.ToString().Contains("TransakcjaVAT"))
                            {
                                stepResults["FallbackTransakcjaVAT_Error"] = true;
                            }
                        }
                    }
                }
                catch (Exception managerEx)
                {
                    stepResults["Step1_GetManager"] = "ERROR: " + managerEx.Message;
                }

                return stepResults;
            });

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(testResult));
        }
        catch (Exception ex)
        {
            results["OuterError"] = ex.Message;
            results["OuterStackTrace"] = ex.StackTrace?.Substring(0, Math.Min(1000, ex.StackTrace?.Length ?? 0));
            if (ex.ToString().Contains("TransakcjaVAT"))
            {
                results["TransakcjaVAT_Error"] = true;
            }
            _logger.LogError(ex, "Error in TestPzCreation diagnostic");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }

    /// <summary>
    /// List all document configurations and their financial aspect settings
    /// This helps identify configurations that might work without triggering VAT loading
    /// </summary>
    [HttpGet("document-configurations")]
    [ProducesResponseType(typeof(ApiResponse<List<Dictionary<string, object?>>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Dictionary<string, object?>>>>> ListDocumentConfigurations()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<List<Dictionary<string, object?>>>(() =>
            {
                var configurations = new List<Dictionary<string, object?>>();

                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                if (konfiguracje?.Dane != null)
                {
                    foreach (var config in konfiguracje.Dane.Wszystkie())
                    {
                        try
                        {
                            var configInfo = new Dictionary<string, object?>
                            {
                                ["Id"] = config.Id?.ToString(),
                                ["Symbol"] = config.Symbol?.ToString(),
                                ["Nazwa"] = config.Nazwa?.ToString()
                            };

                            // Try to get PosiadaAspektFinansowy
                            try
                            {
                                configInfo["PosiadaAspektFinansowy"] = config.PosiadaAspektFinansowy?.ToString();
                            }
                            catch
                            {
                                configInfo["PosiadaAspektFinansowy"] = "N/A";
                            }

                            // Check if it's a PZ-related configuration
                            string symbol = config.Symbol?.ToString() ?? "";
                            string nazwa = config.Nazwa?.ToString() ?? "";
                            bool isPzRelated = symbol.Contains("PZ") || nazwa.Contains("Przyjęcie") || nazwa.Contains("przyjęcie");
                            configInfo["IsPzRelated"] = isPzRelated;

                            configurations.Add(configInfo);
                        }
                        catch (Exception itemEx)
                        {
                            configurations.Add(new Dictionary<string, object?>
                            {
                                ["Error"] = itemEx.Message
                            });
                        }
                    }
                }

                return configurations;
            });

            // Sort to show PZ-related first
            var sorted = result
                .OrderByDescending(c => c.ContainsKey("IsPzRelated") && (bool)c["IsPzRelated"]!)
                .ThenBy(c => c.ContainsKey("Symbol") ? c["Symbol"]?.ToString() : "")
                .ToList();

            return Ok(ApiResponse<List<Dictionary<string, object?>>>.Ok(sorted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing document configurations");
            return StatusCode(500, ApiResponse<List<Dictionary<string, object?>>>.Error(
                "Error listing configurations",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Attempt to disable financial aspect for PZ configuration to work around TransakcjaVAT error
    /// WARNING: This modifies the system configuration!
    /// </summary>
    [HttpPost("disable-pz-financial-aspect")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> DisablePzFinancialAspect()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var modifyResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var stepResults = new Dictionary<string, object?>();

                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                if (konfiguracje == null)
                {
                    stepResults["Error"] = "Could not get Konfiguracje manager";
                    return stepResults;
                }

                // Get PZ configuration
                dynamic? pzConfig = konfiguracje.DaneDomyslne?.PrzyjecieZewnetrzne;
                if (pzConfig == null)
                {
                    stepResults["Error"] = "Could not get PZ default configuration";
                    return stepResults;
                }

                stepResults["ConfigId"] = pzConfig.Id?.ToString();
                stepResults["ConfigSymbol"] = pzConfig.Symbol?.ToString();

                try
                {
                    stepResults["BeforePosiadaAspektFinansowy"] = pzConfig.PosiadaAspektFinansowy?.ToString();
                }
                catch
                {
                    stepResults["BeforePosiadaAspektFinansowy"] = "CANNOT_READ";
                }

                // Try to find and modify the configuration
                stepResults["Step1_FindConfig"] = "Starting...";
                try
                {
                    Guid configId = pzConfig.Id;
                    using (var configEdit = konfiguracje.Znajdz(c => c.Id == configId))
                    {
                        stepResults["Step1_FindConfig"] = "SUCCESS";
                        stepResults["ConfigEditType"] = configEdit?.GetType().FullName;

                        // Try to access ParametryKonfiguracji
                        stepResults["Step2_ModifyConfig"] = "Starting...";
                        try
                        {
                            // Method 1: Try ParametryKonfiguracji.PosiadaAspektFinansowy
                            configEdit.ParametryKonfiguracji.PosiadaAspektFinansowy = false;
                            stepResults["Step2_ModifyConfig"] = "Set via ParametryKonfiguracji";
                        }
                        catch (Exception paramEx)
                        {
                            stepResults["ParametryKonfiguracji_Error"] = paramEx.Message;

                            // Method 2: Try direct property
                            try
                            {
                                configEdit.Dane.PosiadaAspektFinansowy = false;
                                stepResults["Step2_ModifyConfig"] = "Set via Dane";
                            }
                            catch (Exception daneEx)
                            {
                                stepResults["Dane_Error"] = daneEx.Message;
                                stepResults["Step2_ModifyConfig"] = "FAILED";
                            }
                        }

                        // Try to save
                        stepResults["Step3_SaveConfig"] = "Starting...";
                        try
                        {
                            bool saved = configEdit.Zapisz();
                            stepResults["Step3_SaveConfig"] = saved ? "SUCCESS" : "FAILED (Zapisz returned false)";

                            if (!saved)
                            {
                                // Try to get errors
                                try
                                {
                                    var errors = new List<string>();
                                    foreach (var error in configEdit.Bledy)
                                    {
                                        errors.Add(error?.ToString() ?? "Unknown error");
                                    }
                                    stepResults["SaveErrors"] = errors;
                                }
                                catch { }
                            }
                        }
                        catch (Exception saveEx)
                        {
                            stepResults["Step3_SaveConfig"] = "ERROR: " + saveEx.Message;
                        }
                    }
                }
                catch (Exception findEx)
                {
                    stepResults["Step1_FindConfig"] = "ERROR: " + findEx.Message;

                    // The Znajdz might trigger the same VAT error
                    if (findEx.ToString().Contains("TransakcjaVAT"))
                    {
                        stepResults["TransakcjaVAT_Error_During_Find"] = true;
                        stepResults["Recommendation"] = "Configuration modification also triggers VAT loading. " +
                            "You need to modify the configuration directly in the database using SQL: " +
                            "UPDATE ModelDanychContainer.Konfiguracje SET PosiadaAspektFinansowy = 0 WHERE Symbol = 'PZ'";
                    }
                }

                // Verify the change
                stepResults["Step4_Verify"] = "Starting...";
                try
                {
                    var verifyConfig = konfiguracje.DaneDomyslne?.PrzyjecieZewnetrzne;
                    stepResults["AfterPosiadaAspektFinansowy"] = verifyConfig?.PosiadaAspektFinansowy?.ToString();
                    stepResults["Step4_Verify"] = "SUCCESS";
                }
                catch (Exception verifyEx)
                {
                    stepResults["Step4_Verify"] = "ERROR: " + verifyEx.Message;
                }

                return stepResults;
            });

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(modifyResult));
        }
        catch (Exception ex)
        {
            results["OuterError"] = ex.Message;
            _logger.LogError(ex, "Error disabling PZ financial aspect");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }

    /// <summary>
    /// Get SQL command to manually disable PZ financial aspect in database
    /// Use this if the SDK-based modification doesn't work
    /// </summary>
    [HttpGet("pz-financial-aspect-sql")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<Dictionary<string, object?>>> GetPzFinancialAspectSql()
    {
        var result = new Dictionary<string, object?>
        {
            ["Description"] = "SQL commands to disable financial aspect for PZ documents. " +
                "This will prevent VAT transaction loading during document creation. " +
                "WARNING: This affects ALL PZ documents in the system!",
            ["CheckCurrentState"] = @"
-- Check current PZ configuration
SELECT Id, Symbol, Nazwa, PosiadaAspektFinansowy
FROM ModelDanychContainer.Konfiguracje
WHERE Symbol LIKE '%PZ%' OR Nazwa LIKE '%Przyjęcie%'
ORDER BY Symbol",
            ["DisableFinancialAspect"] = @"
-- Disable financial aspect for standard PZ
UPDATE ModelDanychContainer.Konfiguracje
SET PosiadaAspektFinansowy = 0
WHERE Symbol = 'PZ'

-- Verify the change
SELECT Id, Symbol, Nazwa, PosiadaAspektFinansowy
FROM ModelDanychContainer.Konfiguracje
WHERE Symbol = 'PZ'",
            ["ReEnableFinancialAspect"] = @"
-- To re-enable financial aspect later:
UPDATE ModelDanychContainer.Konfiguracje
SET PosiadaAspektFinansowy = 1
WHERE Symbol = 'PZ'",
            ["AlternativeApproach"] = @"
-- Alternative: Create a new PZ configuration without financial aspect
-- This preserves the original PZ configuration
-- (Requires more complex SQL to copy the configuration)",
            ["Note"] = "After running the SQL, restart the API application to clear any cached configurations."
        };

        return Ok(ApiResponse<Dictionary<string, object?>>.Ok(result));
    }

    /// <summary>
    /// Explore available methods on PrzyjeciaZewnetrzne manager to find alternative creation methods
    /// This helps identify if there's a creation method that accepts parameters to control VAT loading
    /// </summary>
    [HttpGet("pz-manager-methods")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> ExplorePzManagerMethods()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var explorationResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var methodsInfo = new Dictionary<string, object?>();

                var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                if (przyjecia == null)
                {
                    methodsInfo["Error"] = "Could not get PrzyjeciaZewnetrzne manager";
                    return methodsInfo;
                }

                methodsInfo["ManagerType"] = przyjecia.GetType().FullName;

                // Get all methods on the manager
                var managerType = przyjecia.GetType();
                var methods = managerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                    .OrderBy(m => m.Name)
                    .ToList();

                var methodsList = new List<Dictionary<string, object?>>();
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    var paramsList = parameters.Select(p => new Dictionary<string, object?>
                    {
                        ["Name"] = p.Name,
                        ["Type"] = p.ParameterType.Name,
                        ["FullType"] = p.ParameterType.FullName,
                        ["IsOptional"] = p.IsOptional,
                        ["HasDefaultValue"] = p.HasDefaultValue,
                        ["DefaultValue"] = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                    }).ToList();

                    methodsList.Add(new Dictionary<string, object?>
                    {
                        ["Name"] = method.Name,
                        ["ReturnType"] = method.ReturnType.Name,
                        ["FullReturnType"] = method.ReturnType.FullName,
                        ["Parameters"] = paramsList,
                        ["IsCreationMethod"] = method.Name.StartsWith("Utworz") || method.Name.StartsWith("Create")
                    });
                }
                methodsInfo["Methods"] = methodsList;

                // Specifically look for Utworz* methods
                var creationMethods = methodsList
                    .Where(m => m["Name"]?.ToString()?.StartsWith("Utworz") == true)
                    .ToList();
                methodsInfo["CreationMethods"] = creationMethods;

                // Also check interfaces implemented by this manager
                var interfaces = managerType.GetInterfaces();
                var interfacesList = interfaces.Select(i => new Dictionary<string, object?>
                {
                    ["Name"] = i.Name,
                    ["FullName"] = i.FullName
                }).ToList();
                methodsInfo["Interfaces"] = interfacesList;

                // Check if there's an IPrzyjeciaZewnetrzne interface with specific methods
                var iPrzyjeciaZewnetrzne = interfaces.FirstOrDefault(i =>
                    i.Name == "IPrzyjeciaZewnetrzne" || i.Name.Contains("PrzyjeciaZewnetrzne"));
                if (iPrzyjeciaZewnetrzne != null)
                {
                    methodsInfo["MainInterface"] = iPrzyjeciaZewnetrzne.FullName;

                    var interfaceMethods = iPrzyjeciaZewnetrzne.GetMethods();
                    var interfaceMethodsList = interfaceMethods.Select(m => new Dictionary<string, object?>
                    {
                        ["Name"] = m.Name,
                        ["ReturnType"] = m.ReturnType.Name,
                        ["Parameters"] = m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList()
                    }).ToList();
                    methodsInfo["InterfaceMethods"] = interfaceMethodsList;
                }

                return methodsInfo;
            });

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(explorationResult));
        }
        catch (Exception ex)
        {
            results["Error"] = ex.Message;
            results["StackTrace"] = ex.StackTrace?.Substring(0, Math.Min(1000, ex.StackTrace?.Length ?? 0));
            _logger.LogError(ex, "Error exploring PZ manager methods");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }

    /// <summary>
    /// Test PZ creation with explicit date parameters to diagnose VAT initialization issues
    /// </summary>
    [HttpPost("test-pz-creation-with-dates")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> TestPzCreationWithDates(
        [FromQuery] DateTime? issueDate = null,
        [FromQuery] DateTime? externalDocDate = null,
        [FromQuery] string? externalDocNumber = null)
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var testResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var stepResults = new Dictionary<string, object?>
                {
                    ["InputIssueDate"] = issueDate?.ToString("o") ?? "Not provided (will use current date)",
                    ["InputExternalDocDate"] = externalDocDate?.ToString("o") ?? "Not provided",
                    ["InputExternalDocNumber"] = externalDocNumber ?? "Not provided"
                };

                var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                if (przyjecia == null)
                {
                    stepResults["Error"] = "Could not get PrzyjeciaZewnetrzne manager";
                    return stepResults;
                }

                // Get configuration
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                var pzConfig = konfiguracje?.DaneDomyslne?.PrzyjecieZewnetrzne;

                stepResults["ConfigFound"] = pzConfig != null;
                if (pzConfig != null)
                {
                    try { stepResults["ConfigPosiadaAspektFinansowy"] = pzConfig.PosiadaAspektFinansowy?.ToString(); }
                    catch { stepResults["ConfigPosiadaAspektFinansowy"] = "Cannot read"; }
                }

                // Try creating with Utworz(Konfiguracja)
                stepResults["Step1_CreateDocument"] = "Starting...";
                try
                {
                    using (var pz = pzConfig != null ? przyjecia.Utworz(pzConfig) : przyjecia.UtworzPrzyjecieZewnetrzne())
                    {
                        stepResults["Step1_CreateDocument"] = "SUCCESS";
                        stepResults["DocumentCreated"] = true;

                        // Step 2: Set dates BEFORE reserving number
                        var effectiveIssueDate = issueDate ?? DateTime.Now;
                        var effectiveExternalDate = externalDocDate ?? issueDate ?? DateTime.Now;

                        stepResults["Step2_SetDates"] = "Starting...";
                        try
                        {
                            pz.Dane.DataWydaniaWystawienia = effectiveIssueDate;
                            stepResults["SetDataWydaniaWystawienia"] = effectiveIssueDate.ToString("o");
                        }
                        catch (Exception ex) { stepResults["DataWydaniaWystawienia_Error"] = ex.Message; }

                        try
                        {
                            pz.Dane.DataWprowadzenia = effectiveIssueDate;
                            stepResults["SetDataWprowadzenia"] = effectiveIssueDate.ToString("o");
                        }
                        catch (Exception ex) { stepResults["DataWprowadzenia_Error"] = ex.Message; }

                        // Try setting external document date (critical for VAT)
                        try
                        {
                            pz.Dane.DataDokumentuZewnetrznego = effectiveExternalDate;
                            stepResults["SetDataDokumentuZewnetrznego"] = effectiveExternalDate.ToString("o");
                        }
                        catch (Exception ex)
                        {
                            stepResults["DataDokumentuZewnetrznego_Error"] = ex.Message;
                            // Try alternative property names
                            try
                            {
                                pz.Dane.DataZewnetrzna = effectiveExternalDate;
                                stepResults["SetDataZewnetrzna_Alternative"] = effectiveExternalDate.ToString("o");
                            }
                            catch { }
                        }

                        // Set external document number
                        if (!string.IsNullOrEmpty(externalDocNumber))
                        {
                            try
                            {
                                pz.Dane.NumerZewnetrzny = externalDocNumber;
                                stepResults["SetNumerZewnetrzny"] = externalDocNumber;
                            }
                            catch (Exception ex) { stepResults["NumerZewnetrzny_Error"] = ex.Message; }
                        }

                        stepResults["Step2_SetDates"] = "SUCCESS";

                        // Step 3: Reserve number (this may trigger VAT loading)
                        stepResults["Step3_ReserveNumber"] = "Starting...";
                        try
                        {
                            pz.ZarezerwujNumer();
                            var numberPreview = pz.PodajPodgladNumeru()?.ToString();
                            stepResults["Step3_ReserveNumber"] = "SUCCESS";
                            stepResults["ReservedNumber"] = numberPreview;
                        }
                        catch (Exception numEx)
                        {
                            stepResults["Step3_ReserveNumber"] = "ERROR: " + numEx.Message;
                            if (numEx.ToString().Contains("TransakcjaVAT"))
                            {
                                stepResults["TransakcjaVAT_Error"] = true;
                                stepResults["Recommendation"] = "The error occurs during ZarezerwujNumer(), not document creation. " +
                                    "VAT transactions are loaded when the document number is reserved. " +
                                    "Consider disabling financial aspect on the configuration.";
                            }
                        }

                        // Step 4: List available properties on pz.Dane to find VAT-related ones
                        stepResults["Step4_ListDaneProperties"] = "Starting...";
                        try
                        {
                            var daneType = ((object)pz.Dane).GetType();
                            var daneProperties = daneType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.Name.Contains("VAT", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Contains("Transakcja", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Contains("Aspekt", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Contains("Finansow", StringComparison.OrdinalIgnoreCase) ||
                                           p.Name.Contains("Data", StringComparison.OrdinalIgnoreCase))
                                .Select(p => new { p.Name, Type = p.PropertyType.Name })
                                .ToList();
                            stepResults["Step4_ListDaneProperties"] = "SUCCESS";
                            stepResults["VatRelatedProperties"] = daneProperties;
                        }
                        catch (Exception propEx)
                        {
                            stepResults["Step4_ListDaneProperties"] = "ERROR: " + propEx.Message;
                        }
                    }
                }
                catch (Exception createEx)
                {
                    stepResults["Step1_CreateDocument"] = "ERROR: " + createEx.Message;
                    stepResults["DocumentCreated"] = false;

                    if (createEx.ToString().Contains("TransakcjaVAT"))
                    {
                        stepResults["TransakcjaVAT_Error"] = true;
                        stepResults["ErrorOccurredAt"] = "Document creation (UtworzPrzyjecieZewnetrzne/Utworz)";
                        stepResults["Recommendation"] = "The error occurs DURING document creation, before any properties can be set. " +
                            "This is an SDK/EF6 compatibility issue. Consider: " +
                            "1) Disabling financial aspect on the PZ configuration in the database, or " +
                            "2) Using a PZ configuration without VAT aspect (if available).";
                    }
                }

                return stepResults;
            });

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(testResult));
        }
        catch (Exception ex)
        {
            results["OuterError"] = ex.Message;
            if (ex.ToString().Contains("TransakcjaVAT"))
            {
                results["TransakcjaVAT_Error"] = true;
            }
            _logger.LogError(ex, "Error in TestPzCreationWithDates diagnostic");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }
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
