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
    /// Check if database has WystepowanieFakturKsef column (added in SDK v59)
    /// This is the key diagnostic for the TransakcjaVAT materialization error.
    /// If the database is MISSING this column but the SDK expects it, EF6 will read wrong ordinals.
    /// </summary>
    [HttpGet("check-sdk-v59-schema")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> CheckSdkV59Schema()
    {
        try
        {
            var connectionString = _sferaService.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(ApiResponse<Dictionary<string, object?>>.Error("Connection string not available"));
            }

            var results = new Dictionary<string, object?>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Find the TransakcjaVAT or similar table
            const string findTableQuery = @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME LIKE '%TransakcjaVAT%'
                   OR TABLE_NAME LIKE '%TransakcjeVAT%'
                   OR TABLE_NAME = 'TransakcjaVAT'
                ORDER BY TABLE_NAME";

            var tables = new List<string>();
            string? targetSchema = null;
            string? targetTable = null;

            await using (var cmd = new SqlCommand(findTableQuery, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var schema = reader.GetString(0);
                    var table = reader.GetString(1);
                    tables.Add($"{schema}.{table}");

                    // Prioritize ModelDanychContainer.TransakcjeVAT (the actual data table)
                    // over index tables (idx_*) or junction tables
                    if (schema == "ModelDanychContainer" && table == "TransakcjeVAT")
                    {
                        targetSchema = schema;
                        targetTable = table;
                    }
                    else if (targetTable == null && !table.StartsWith("idx_") && !table.Contains("_"))
                    {
                        // Fallback to first non-index table
                        targetSchema = schema;
                        targetTable = table;
                    }
                }
            }

            // If no good table found, use ModelDanychContainer.TransakcjeVAT if it exists
            if (targetTable == null)
            {
                var mdcTable = tables.FirstOrDefault(t => t == "ModelDanychContainer.TransakcjeVAT");
                if (mdcTable != null)
                {
                    targetSchema = "ModelDanychContainer";
                    targetTable = "TransakcjeVAT";
                }
            }

            results["TransakcjaVATTables"] = tables;
            results["TargetTable"] = targetTable != null ? $"{targetSchema}.{targetTable}" : "NOT FOUND";

            if (targetTable == null)
            {
                results["Error"] = "TransakcjaVAT table not found in database";
                results["Diagnosis"] = "The database might not have VAT tracking enabled or uses a different schema";
                return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
            }

            // 2. Get all columns with their ordinal positions
            var columnsQuery = $@"
                SELECT
                    ORDINAL_POSITION,
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                ORDER BY ORDINAL_POSITION";

            var columns = new List<Dictionary<string, object?>>();
            int? wystepowanieOrdinal = null;
            int? idWInstancjiOrdinal = null;
            bool hasWystepowanie = false;

            await using (var cmd = new SqlCommand(columnsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@schema", targetSchema);
                cmd.Parameters.AddWithValue("@table", targetTable);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ordinal = reader.GetInt32(0);
                    var colName = reader.GetString(1);
                    var dataType = reader.GetString(2);
                    var nullable = reader.GetString(3);

                    columns.Add(new Dictionary<string, object?>
                    {
                        ["Ordinal"] = ordinal,
                        ["ColumnName"] = colName,
                        ["DataType"] = dataType,
                        ["IsNullable"] = nullable
                    });

                    if (colName.Equals("WystepowanieFakturKsef", StringComparison.OrdinalIgnoreCase))
                    {
                        hasWystepowanie = true;
                        wystepowanieOrdinal = ordinal;
                    }
                    if (colName.Equals("IdWInstancji", StringComparison.OrdinalIgnoreCase))
                    {
                        idWInstancjiOrdinal = ordinal;
                    }
                }
            }

            results["TotalColumns"] = columns.Count;
            results["HasWystepowanieFakturKsef"] = hasWystepowanie;
            results["WystepowanieFakturKsef_Ordinal"] = wystepowanieOrdinal;
            results["IdWInstancji_Ordinal"] = idWInstancjiOrdinal;

            // 3. Show columns around the critical area
            var criticalColumns = columns.Where(c =>
            {
                var ord = (int?)c["Ordinal"];
                var name = c["ColumnName"]?.ToString() ?? "";
                return (wystepowanieOrdinal.HasValue && ord >= wystepowanieOrdinal - 2 && ord <= wystepowanieOrdinal + 2) ||
                       (idWInstancjiOrdinal.HasValue && ord >= idWInstancjiOrdinal - 2 && ord <= idWInstancjiOrdinal + 2) ||
                       name.Contains("Ksef", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("IdWInstancji", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            results["CriticalColumns"] = criticalColumns;

            // 4. Check ALL TransakcjeVAT tables for WystepowanieFakturKsef
            var allTablesCheck = new List<Dictionary<string, object?>>();
            foreach (var tableFullName in tables)
            {
                var parts = tableFullName.Split('.');
                if (parts.Length != 2) continue;

                var checkQuery = @"
                    SELECT COUNT(*) as HasColumn
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                    AND COLUMN_NAME = 'WystepowanieFakturKsef'";

                await using var checkCmd = new SqlCommand(checkQuery, connection);
                checkCmd.Parameters.AddWithValue("@schema", parts[0]);
                checkCmd.Parameters.AddWithValue("@table", parts[1]);
                var hasCol = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

                allTablesCheck.Add(new Dictionary<string, object?>
                {
                    ["Table"] = tableFullName,
                    ["HasWystepowanieFakturKsef"] = hasCol
                });
            }
            results["AllTablesWystepowanieCheck"] = allTablesCheck;

            // 5. Diagnosis
            if (!hasWystepowanie)
            {
                results["DIAGNOSIS"] = "CRITICAL: Database is MISSING the WystepowanieFakturKsef column! " +
                    "SDK v59 expects this column but it doesn't exist in the database. " +
                    "This causes EF6 to read columns at wrong ordinals, resulting in the 'could not be set to Byte[]' error. " +
                    "SOLUTION: Run Nexo desktop application (Subiekt) to upgrade the database schema, " +
                    "or contact InsERT support about database migration.";
                results["DatabaseNeedsUpgrade"] = true;
            }
            else
            {
                results["DIAGNOSIS"] = "Database has WystepowanieFakturKsef column. " +
                    "If PZ creation still fails, the issue might be: " +
                    "1) SDK DLL version mismatch with database " +
                    "2) EF6 context caching stale metadata " +
                    "3) Different SQL provider behavior";
                results["DatabaseSchemaOk"] = true;
            }

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SDK v59 schema");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error checking SDK v59 schema",
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
    /// Trace the actual column types returned by DbDataReader for TransakcjaVAT table.
    /// This diagnoses whether the issue is at ADO.NET level or EF6 level.
    /// Shows exact ordinal positions and CLR types for all columns.
    /// </summary>
    [HttpGet("trace-transakcja-vat-reader")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> TraceTransakcjaVatReader()
    {
        try
        {
            var connectionString = _sferaService.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(ApiResponse<Dictionary<string, object?>>.Error("Connection string not available"));
            }

            var results = new Dictionary<string, object?>();

            // Test with BOTH SQL providers to compare behavior
            var providerComparison = new Dictionary<string, object?>();

            // Test 1: Microsoft.Data.SqlClient (what we're using)
            try
            {
                await using var msConnection = new SqlConnection(connectionString);
                await msConnection.OpenAsync();

                var msResults = await TraceTransakcjaVatWithProvider(msConnection, "Microsoft.Data.SqlClient");
                providerComparison["Microsoft.Data.SqlClient"] = msResults;
            }
            catch (Exception ex)
            {
                providerComparison["Microsoft.Data.SqlClient"] = new Dictionary<string, object?>
                {
                    ["Error"] = ex.Message,
                    ["InnerError"] = ex.InnerException?.Message
                };
            }

            // Test 2: System.Data.SqlClient (the legacy provider SDK was built with)
            try
            {
                await using var sysConnection = new System.Data.SqlClient.SqlConnection(connectionString);
                await sysConnection.OpenAsync();

                var sysResults = await TraceTransakcjaVatWithLegacyProvider(sysConnection, "System.Data.SqlClient");
                providerComparison["System.Data.SqlClient"] = sysResults;
            }
            catch (Exception ex)
            {
                providerComparison["System.Data.SqlClient"] = new Dictionary<string, object?>
                {
                    ["Error"] = ex.Message,
                    ["InnerError"] = ex.InnerException?.Message
                };
            }

            results["ProviderComparison"] = providerComparison;

            // Analyze differences
            var analysis = new List<string>();
            if (providerComparison["Microsoft.Data.SqlClient"] is Dictionary<string, object?> msData &&
                providerComparison["System.Data.SqlClient"] is Dictionary<string, object?> sysData)
            {
                if (msData.ContainsKey("Columns") && sysData.ContainsKey("Columns"))
                {
                    var msColumns = msData["Columns"] as List<Dictionary<string, object?>>;
                    var sysColumns = sysData["Columns"] as List<Dictionary<string, object?>>;

                    if (msColumns != null && sysColumns != null && msColumns.Count == sysColumns.Count)
                    {
                        for (int i = 0; i < msColumns.Count; i++)
                        {
                            var msCol = msColumns[i];
                            var sysCol = sysColumns[i];
                            var colName = msCol["Name"]?.ToString();

                            if (msCol["ClrType"]?.ToString() != sysCol["ClrType"]?.ToString())
                            {
                                analysis.Add($"TYPE MISMATCH at ordinal {i} ({colName}): " +
                                    $"Microsoft.Data={msCol["ClrType"]}, System.Data={sysCol["ClrType"]}");
                            }
                        }
                    }
                }
            }

            if (analysis.Count == 0)
            {
                analysis.Add("No type mismatches detected between providers");
            }

            results["TypeMismatchAnalysis"] = analysis;
            results["Diagnosis"] = "If there are type mismatches, the issue is at ADO.NET provider level. " +
                "If no mismatches, the issue is in EF6 metadata/model mapping.";

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracing TransakcjaVAT reader");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error tracing TransakcjaVAT reader",
                new List<string> { ex.Message, ex.InnerException?.Message ?? "" }));
        }
    }

    private async Task<Dictionary<string, object?>> TraceTransakcjaVatWithProvider(
        SqlConnection connection, string providerName)
    {
        var results = new Dictionary<string, object?>
        {
            ["Provider"] = providerName,
            ["ConnectionState"] = connection.State.ToString(),
            ["ServerVersion"] = connection.ServerVersion
        };

        // Query TransakcjeVAT with SELECT TOP 1
        const string query = @"
            SELECT TOP 1 *
            FROM [ModelDanychContainer].[TransakcjeVAT]";

        await using var cmd = new SqlCommand(query, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var columns = new List<Dictionary<string, object?>>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var colInfo = new Dictionary<string, object?>
            {
                ["Ordinal"] = i,
                ["Name"] = reader.GetName(i),
                ["ClrType"] = reader.GetFieldType(i)?.FullName,
                ["DbTypeName"] = reader.GetDataTypeName(i)
            };

            // Flag special columns
            var name = reader.GetName(i);
            if (name == "IdWInstancji" || name == "TimeStamp" || name == "WystepowanieFakturKsef")
            {
                colInfo["CRITICAL"] = true;
            }

            columns.Add(colInfo);
        }
        results["Columns"] = columns;
        results["ColumnCount"] = reader.FieldCount;

        // Read one row to check actual value types
        if (await reader.ReadAsync())
        {
            var sampleValues = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (name == "IdWInstancji" || name == "TimeStamp" || name == "WystepowanieFakturKsef" ||
                    name == "Id" || name == "IdRodzajuTransakcji")
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    sampleValues[name] = new Dictionary<string, object?>
                    {
                        ["Ordinal"] = i,
                        ["IsNull"] = reader.IsDBNull(i),
                        ["Value"] = value?.ToString(),
                        ["ActualClrType"] = value?.GetType().FullName,
                        ["IsByteArray"] = value is byte[]
                    };
                }
            }
            results["CriticalColumnValues"] = sampleValues;
        }
        else
        {
            results["CriticalColumnValues"] = "No data in TransakcjeVAT table";
        }

        return results;
    }

    private async Task<Dictionary<string, object?>> TraceTransakcjaVatWithLegacyProvider(
        System.Data.SqlClient.SqlConnection connection, string providerName)
    {
        var results = new Dictionary<string, object?>
        {
            ["Provider"] = providerName,
            ["ConnectionState"] = connection.State.ToString(),
            ["ServerVersion"] = connection.ServerVersion
        };

        // Query TransakcjeVAT with SELECT TOP 1
        const string query = @"
            SELECT TOP 1 *
            FROM [ModelDanychContainer].[TransakcjeVAT]";

        await using var cmd = new System.Data.SqlClient.SqlCommand(query, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var columns = new List<Dictionary<string, object?>>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var colInfo = new Dictionary<string, object?>
            {
                ["Ordinal"] = i,
                ["Name"] = reader.GetName(i),
                ["ClrType"] = reader.GetFieldType(i)?.FullName,
                ["DbTypeName"] = reader.GetDataTypeName(i)
            };

            // Flag special columns
            var name = reader.GetName(i);
            if (name == "IdWInstancji" || name == "TimeStamp" || name == "WystepowanieFakturKsef")
            {
                colInfo["CRITICAL"] = true;
            }

            columns.Add(colInfo);
        }
        results["Columns"] = columns;
        results["ColumnCount"] = reader.FieldCount;

        // Read one row to check actual value types
        if (await reader.ReadAsync())
        {
            var sampleValues = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (name == "IdWInstancji" || name == "TimeStamp" || name == "WystepowanieFakturKsef" ||
                    name == "Id" || name == "IdRodzajuTransakcji")
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    sampleValues[name] = new Dictionary<string, object?>
                    {
                        ["Ordinal"] = i,
                        ["IsNull"] = reader.IsDBNull(i),
                        ["Value"] = value?.ToString(),
                        ["ActualClrType"] = value?.GetType().FullName,
                        ["IsByteArray"] = value is byte[]
                    };
                }
            }
            results["CriticalColumnValues"] = sampleValues;
        }
        else
        {
            results["CriticalColumnValues"] = "No data in TransakcjeVAT table";
        }

        return results;
    }

    /// <summary>
    /// Examine the SDK's EF6 model metadata for TransakcjaVAT entity.
    /// This shows what columns/properties EF6 expects and can help diagnose ordinal mismatches.
    /// Uses reflection to examine the compiled EF6 model inside the SDK DLLs.
    /// </summary>
    [HttpGet("examine-ef6-model")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<Dictionary<string, object?>>> ExamineEf6Model()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            // Find TransakcjaVAT type in loaded assemblies
            var transakcjaVatType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.Name == "TransakcjaVAT" && t.Namespace?.Contains("ModelDanych") == true);

            if (transakcjaVatType == null)
            {
                results["Error"] = "TransakcjaVAT type not found in loaded assemblies";

                // List assemblies that might contain it
                var modelAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.Contains("ModelDanych") == true)
                    .Select(a => a.FullName)
                    .ToList();
                results["ModelDanychAssemblies"] = modelAssemblies;

                return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
            }

            results["TypeFound"] = transakcjaVatType.FullName;
            results["Assembly"] = transakcjaVatType.Assembly.FullName;

            // Get all properties with their types
            var properties = transakcjaVatType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select((p, index) => new Dictionary<string, object?>
                {
                    ["Index"] = index,
                    ["Name"] = p.Name,
                    ["PropertyType"] = p.PropertyType.Name,
                    ["FullType"] = p.PropertyType.FullName,
                    ["CanRead"] = p.CanRead,
                    ["CanWrite"] = p.CanWrite
                })
                .ToList();

            results["Properties"] = properties;
            results["PropertyCount"] = properties.Count;

            // Find IdWInstancji and TimeStamp properties
            var idWInstancji = properties.FirstOrDefault(p => p["Name"]?.ToString() == "IdWInstancji");
            var timeStamp = properties.FirstOrDefault(p => p["Name"]?.ToString() == "TimeStamp");
            var wystepowanie = properties.FirstOrDefault(p => p["Name"]?.ToString() == "WystepowanieFakturKsef");

            results["IdWInstancji_Property"] = idWInstancji;
            results["TimeStamp_Property"] = timeStamp;
            results["WystepowanieFakturKsef_Property"] = wystepowanie;

            // Check for EF6 metadata attributes
            var customAttributes = transakcjaVatType.CustomAttributes
                .Select(a => new Dictionary<string, object?>
                {
                    ["AttributeType"] = a.AttributeType.Name,
                    ["FullType"] = a.AttributeType.FullName
                })
                .ToList();

            results["CustomAttributes"] = customAttributes;

            // Try to get EF6's ObjectContext or DbContext if available
            try
            {
                var ef6CoreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "InsERT.Mox.EntityFramework.Core");

                if (ef6CoreAssembly != null)
                {
                    results["EF6CoreAssembly"] = ef6CoreAssembly.FullName;

                    // List types that might be DbContext or ObjectContext
                    var contextTypes = ef6CoreAssembly.GetTypes()
                        .Where(t => t.Name.Contains("Context") || t.Name.Contains("Model"))
                        .Select(t => t.FullName)
                        .Take(20)
                        .ToList();
                    results["ContextTypes"] = contextTypes;
                }
            }
            catch (Exception ex)
            {
                results["EF6CoreError"] = ex.Message;
            }

            // Check if there's a ModelDanychContainer type (EF6 ObjectContext)
            var containerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.Name == "ModelDanychContainer");

            if (containerType != null)
            {
                results["ModelDanychContainer"] = containerType.FullName;
                results["ContainerAssembly"] = containerType.Assembly.GetName().Name;

                // Get DbSet/ObjectSet properties to see entity sets
                var entitySets = containerType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType.IsGenericType &&
                               (p.PropertyType.Name.Contains("ObjectSet") ||
                                p.PropertyType.Name.Contains("DbSet")))
                    .Select(p => new Dictionary<string, object?>
                    {
                        ["Name"] = p.Name,
                        ["PropertyType"] = p.PropertyType.Name,
                        ["EntityType"] = p.PropertyType.GenericTypeArguments.FirstOrDefault()?.Name
                    })
                    .Take(30)
                    .ToList();

                results["EntitySets"] = entitySets;
            }

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error examining EF6 model");
            return StatusCode(500, ApiResponse<Dictionary<string, object?>>.Error(
                "Error examining EF6 model",
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
                    // Cast to avoid CS1977 - lambda cannot be used with dynamic
                    Func<dynamic, bool> predicate = c => c.Id == configId;
                    using (var configEdit = konfiguracje.Znajdz(predicate))
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

                // Cast to object to get proper Type (avoiding dynamic dispatch issues)
                Type managerType = ((object)przyjecia).GetType();
                methodsInfo["ManagerType"] = managerType.FullName;

                // Get all methods on the manager
                MethodInfo[] allMethods = managerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var methods = new List<MethodInfo>();
                foreach (var m in allMethods)
                {
                    if (!m.IsSpecialName && m.DeclaringType != typeof(object))
                    {
                        methods.Add(m);
                    }
                }
                methods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

                var methodsList = new List<Dictionary<string, object?>>();
                foreach (var method in methods)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    var paramsList = new List<Dictionary<string, object?>>();
                    foreach (var p in parameters)
                    {
                        paramsList.Add(new Dictionary<string, object?>
                        {
                            ["Name"] = p.Name,
                            ["Type"] = p.ParameterType.Name,
                            ["FullType"] = p.ParameterType.FullName,
                            ["IsOptional"] = p.IsOptional,
                            ["HasDefaultValue"] = p.HasDefaultValue,
                            ["DefaultValue"] = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                        });
                    }

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
                var creationMethods = new List<Dictionary<string, object?>>();
                foreach (var m in methodsList)
                {
                    if (m["Name"]?.ToString()?.StartsWith("Utworz") == true)
                    {
                        creationMethods.Add(m);
                    }
                }
                methodsInfo["CreationMethods"] = creationMethods;

                // Also check interfaces implemented by this manager
                Type[] interfaces = managerType.GetInterfaces();
                var interfacesList = new List<Dictionary<string, object?>>();
                foreach (var i in interfaces)
                {
                    interfacesList.Add(new Dictionary<string, object?>
                    {
                        ["Name"] = i.Name,
                        ["FullName"] = i.FullName
                    });
                }
                methodsInfo["Interfaces"] = interfacesList;

                // Check if there's an IPrzyjeciaZewnetrzne interface with specific methods
                Type? iPrzyjeciaZewnetrzne = null;
                foreach (var i in interfaces)
                {
                    if (i.Name == "IPrzyjeciaZewnetrzne" || i.Name.Contains("PrzyjeciaZewnetrzne"))
                    {
                        iPrzyjeciaZewnetrzne = i;
                        break;
                    }
                }
                if (iPrzyjeciaZewnetrzne != null)
                {
                    methodsInfo["MainInterface"] = iPrzyjeciaZewnetrzne.FullName;

                    MethodInfo[] interfaceMethods = iPrzyjeciaZewnetrzne.GetMethods();
                    var interfaceMethodsList = new List<Dictionary<string, object?>>();
                    foreach (var m in interfaceMethods)
                    {
                        var paramStrings = new List<string>();
                        foreach (var p in m.GetParameters())
                        {
                            paramStrings.Add($"{p.ParameterType.Name} {p.Name}");
                        }
                        interfaceMethodsList.Add(new Dictionary<string, object?>
                        {
                            ["Name"] = m.Name,
                            ["ReturnType"] = m.ReturnType.Name,
                            ["Parameters"] = paramStrings
                        });
                    }
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

    /// <summary>
    /// Test both PZ creation methods to identify which one triggers TransakcjaVAT error
    /// This tests: UtworzPrzyjecieZewnetrzne() vs UtworzPrzyjecieZewnetrzneVAT()
    /// </summary>
    [HttpGet("test-pz-creation-methods")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> TestPzCreationMethods()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var testResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var methodResults = new Dictionary<string, object?>();

                var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                if (przyjecia == null)
                {
                    methodResults["Error"] = "Could not get PrzyjeciaZewnetrzne manager";
                    return methodResults;
                }

                methodResults["ManagerObtained"] = true;

                // Get available configurations first
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                if (konfiguracje != null)
                {
                    var configInfo = new Dictionary<string, object?>();
                    try
                    {
                        var pzConfig = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzne;
                        if (pzConfig != null)
                        {
                            configInfo["PrzyjecieZewnetrzne_Symbol"] = pzConfig.Symbol?.ToString();
                            configInfo["PrzyjecieZewnetrzne_Nazwa"] = pzConfig.Nazwa?.ToString();
                            try { configInfo["PrzyjecieZewnetrzne_PosiadaAspektFinansowy"] = pzConfig.PosiadaAspektFinansowy?.ToString(); }
                            catch { configInfo["PrzyjecieZewnetrzne_PosiadaAspektFinansowy"] = "Cannot read"; }
                        }
                    }
                    catch (Exception ex) { configInfo["PrzyjecieZewnetrzne_Error"] = ex.Message; }

                    try
                    {
                        var pzVatConfig = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzneZVAT;
                        if (pzVatConfig != null)
                        {
                            configInfo["PrzyjecieZewnetrzneZVAT_Symbol"] = pzVatConfig.Symbol?.ToString();
                            configInfo["PrzyjecieZewnetrzneZVAT_Nazwa"] = pzVatConfig.Nazwa?.ToString();
                            try { configInfo["PrzyjecieZewnetrzneZVAT_PosiadaAspektFinansowy"] = pzVatConfig.PosiadaAspektFinansowy?.ToString(); }
                            catch { configInfo["PrzyjecieZewnetrzneZVAT_PosiadaAspektFinansowy"] = "Cannot read"; }
                        }
                    }
                    catch (Exception ex) { configInfo["PrzyjecieZewnetrzneZVAT_Error"] = ex.Message; }

                    methodResults["Configurations"] = configInfo;
                }

                // Test Method 1: UtworzPrzyjecieZewnetrzne() - PZ WITHOUT VAT
                methodResults["Method1_UtworzPrzyjecieZewnetrzne"] = new Dictionary<string, object?>();
                var method1Result = (Dictionary<string, object?>)methodResults["Method1_UtworzPrzyjecieZewnetrzne"]!;
                try
                {
                    _logger.LogInformation("Testing UtworzPrzyjecieZewnetrzne() (without VAT)");
                    using (var pz = przyjecia.UtworzPrzyjecieZewnetrzne())
                    {
                        method1Result["CreateSuccess"] = true;
                        method1Result["DocumentType"] = ((object)pz).GetType().Name;

                        // Check configuration on created document
                        try
                        {
                            var konfiguracja = pz.Dane.Konfiguracja;
                            if (konfiguracja != null)
                            {
                                method1Result["DocumentConfigSymbol"] = konfiguracja.Symbol?.ToString();
                                method1Result["DocumentConfigName"] = konfiguracja.Nazwa?.ToString();
                                try { method1Result["DocumentConfigPosiadaAspektFinansowy"] = konfiguracja.PosiadaAspektFinansowy?.ToString(); }
                                catch { method1Result["DocumentConfigPosiadaAspektFinansowy"] = "Cannot read"; }
                            }
                        }
                        catch (Exception ex) { method1Result["ConfigReadError"] = ex.Message; }

                        // Try to reserve number - this may trigger VAT loading
                        try
                        {
                            pz.ZarezerwujNumer();
                            method1Result["ReserveNumberSuccess"] = true;
                            method1Result["NumberPreview"] = pz.PodajPodgladNumeru()?.ToString();
                        }
                        catch (Exception numEx)
                        {
                            method1Result["ReserveNumberSuccess"] = false;
                            method1Result["ReserveNumberError"] = numEx.Message;
                            if (numEx.ToString().Contains("TransakcjaVAT") || numEx.ToString().Contains("IdWInstancji"))
                            {
                                method1Result["IsTransakcjaVATError"] = true;
                            }
                        }

                        // Don't save - just dispose
                        method1Result["TestComplete"] = true;
                    }
                }
                catch (Exception ex)
                {
                    method1Result["CreateSuccess"] = false;
                    method1Result["CreateError"] = ex.Message;
                    if (ex.ToString().Contains("TransakcjaVAT") || ex.ToString().Contains("IdWInstancji"))
                    {
                        method1Result["IsTransakcjaVATError"] = true;
                    }
                    _logger.LogError(ex, "UtworzPrzyjecieZewnetrzne() failed");
                }

                // Test Method 2: UtworzPrzyjecieZewnetrzneVAT() - PZ WITH VAT
                methodResults["Method2_UtworzPrzyjecieZewnetrzneVAT"] = new Dictionary<string, object?>();
                var method2Result = (Dictionary<string, object?>)methodResults["Method2_UtworzPrzyjecieZewnetrzneVAT"]!;
                try
                {
                    _logger.LogInformation("Testing UtworzPrzyjecieZewnetrzneVAT() (with VAT)");
                    using (var pzVat = przyjecia.UtworzPrzyjecieZewnetrzneVAT())
                    {
                        method2Result["CreateSuccess"] = true;
                        method2Result["DocumentType"] = ((object)pzVat).GetType().Name;

                        // Check configuration on created document
                        try
                        {
                            var konfiguracja = pzVat.Dane.Konfiguracja;
                            if (konfiguracja != null)
                            {
                                method2Result["DocumentConfigSymbol"] = konfiguracja.Symbol?.ToString();
                                method2Result["DocumentConfigName"] = konfiguracja.Nazwa?.ToString();
                                try { method2Result["DocumentConfigPosiadaAspektFinansowy"] = konfiguracja.PosiadaAspektFinansowy?.ToString(); }
                                catch { method2Result["DocumentConfigPosiadaAspektFinansowy"] = "Cannot read"; }
                            }
                        }
                        catch (Exception ex) { method2Result["ConfigReadError"] = ex.Message; }

                        // Try to reserve number - this WILL likely trigger VAT loading
                        try
                        {
                            pzVat.ZarezerwujNumer();
                            method2Result["ReserveNumberSuccess"] = true;
                            method2Result["NumberPreview"] = pzVat.PodajPodgladNumeru()?.ToString();
                        }
                        catch (Exception numEx)
                        {
                            method2Result["ReserveNumberSuccess"] = false;
                            method2Result["ReserveNumberError"] = numEx.Message;
                            if (numEx.ToString().Contains("TransakcjaVAT") || numEx.ToString().Contains("IdWInstancji"))
                            {
                                method2Result["IsTransakcjaVATError"] = true;
                            }
                        }

                        // Don't save - just dispose
                        method2Result["TestComplete"] = true;
                    }
                }
                catch (Exception ex)
                {
                    method2Result["CreateSuccess"] = false;
                    method2Result["CreateError"] = ex.Message;
                    if (ex.ToString().Contains("TransakcjaVAT") || ex.ToString().Contains("IdWInstancji"))
                    {
                        method2Result["IsTransakcjaVATError"] = true;
                    }
                    _logger.LogError(ex, "UtworzPrzyjecieZewnetrzneVAT() failed");
                }

                // Summary
                var summary = new Dictionary<string, object?>();
                bool method1Works = method1Result.ContainsKey("CreateSuccess") && (bool)method1Result["CreateSuccess"]!;
                bool method2Works = method2Result.ContainsKey("CreateSuccess") && (bool)method2Result["CreateSuccess"]!;

                summary["Method1_UtworzPrzyjecieZewnetrzne_Works"] = method1Works;
                summary["Method2_UtworzPrzyjecieZewnetrzneVAT_Works"] = method2Works;

                if (method1Works && !method2Works)
                {
                    summary["Recommendation"] = "Use UtworzPrzyjecieZewnetrzne() for documents without VAT. " +
                        "For documents WITH VAT, the TransakcjaVAT error occurs due to SDK/EF6 version mismatch.";
                }
                else if (!method1Works && !method2Works)
                {
                    summary["Recommendation"] = "Both methods fail. Check if the default PZ configuration has PosiadaAspektFinansowy=true. " +
                        "If so, the TransakcjaVAT error is triggered even for 'without VAT' documents. " +
                        "Consider disabling PosiadaAspektFinansowy on the PZ configuration in the database.";
                }
                else if (method1Works && method2Works)
                {
                    summary["Recommendation"] = "Both methods work! The error might be caused by something else in your code path.";
                }

                methodResults["Summary"] = summary;
                return methodResults;
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
            _logger.LogError(ex, "Error in TestPzCreationMethods diagnostic");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }

    /// <summary>
    /// List all available PZ configurations and their financial aspect settings
    /// This helps identify which configurations can be used without triggering VAT loading
    /// </summary>
    [HttpGet("list-pz-configurations")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> ListPzConfigurations()
    {
        var results = new Dictionary<string, object?>();

        try
        {
            var configResult = await _sferaService.ExecuteWithLockAsync<Dictionary<string, object?>>(() =>
            {
                var configs = new Dictionary<string, object?>();

                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                if (konfiguracje == null)
                {
                    configs["Error"] = "Could not get Konfiguracje manager";
                    return configs;
                }

                // Get all configurations using Dane.Wszystkie()
                var allConfigsList = new List<Dictionary<string, object?>>();
                var pzConfigsList = new List<Dictionary<string, object?>>();

                try
                {
                    // Try to get all configurations
                    foreach (var config in konfiguracje.Dane.Wszystkie())
                    {
                        try
                        {
                            var configInfo = new Dictionary<string, object?>
                            {
                                ["Symbol"] = config.Symbol?.ToString(),
                                ["Nazwa"] = config.Nazwa?.ToString(),
                                ["Id"] = config.Id?.ToString()
                            };

                            // Try to read PosiadaAspektFinansowy
                            try
                            {
                                configInfo["PosiadaAspektFinansowy"] = config.PosiadaAspektFinansowy?.ToString();
                            }
                            catch
                            {
                                configInfo["PosiadaAspektFinansowy"] = "Cannot read";
                            }

                            // Try to read TypDokumentu
                            try
                            {
                                configInfo["TypDokumentu"] = config.TypDokumentu?.ToString();
                            }
                            catch
                            {
                                configInfo["TypDokumentu"] = "Cannot read";
                            }

                            allConfigsList.Add(configInfo);

                            // Check if this is a PZ-related configuration
                            string? symbol = config.Symbol?.ToString();
                            string? nazwa = config.Nazwa?.ToString();
                            if ((symbol != null && (symbol.Contains("PZ") || symbol.Contains("Przyjęcie") || symbol.Contains("Przyjecie"))) ||
                                (nazwa != null && (nazwa.Contains("PZ") || nazwa.Contains("przyjęci") || nazwa.Contains("przyjeci"))))
                            {
                                pzConfigsList.Add(configInfo);
                            }
                        }
                        catch (Exception configEx)
                        {
                            allConfigsList.Add(new Dictionary<string, object?>
                            {
                                ["Error"] = configEx.Message
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    configs["GetAllConfigsError"] = ex.Message;
                }

                configs["TotalConfigurationsCount"] = allConfigsList.Count;
                configs["PzRelatedConfigurations"] = pzConfigsList;

                // Also get default configurations
                var defaults = new Dictionary<string, object?>();
                try
                {
                    var pz = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzne;
                    if (pz != null)
                    {
                        defaults["PrzyjecieZewnetrzne"] = new Dictionary<string, object?>
                        {
                            ["Symbol"] = pz.Symbol?.ToString(),
                            ["Nazwa"] = pz.Nazwa?.ToString(),
                            ["Id"] = pz.Id?.ToString(),
                            ["PosiadaAspektFinansowy"] = SafeGetProperty(pz, "PosiadaAspektFinansowy")
                        };
                    }
                }
                catch (Exception ex)
                {
                    defaults["PrzyjecieZewnetrzne_Error"] = ex.Message;
                }

                try
                {
                    var pzVat = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzneZVAT;
                    if (pzVat != null)
                    {
                        defaults["PrzyjecieZewnetrzneZVAT"] = new Dictionary<string, object?>
                        {
                            ["Symbol"] = pzVat.Symbol?.ToString(),
                            ["Nazwa"] = pzVat.Nazwa?.ToString(),
                            ["Id"] = pzVat.Id?.ToString(),
                            ["PosiadaAspektFinansowy"] = SafeGetProperty(pzVat, "PosiadaAspektFinansowy")
                        };
                    }
                }
                catch (Exception ex)
                {
                    defaults["PrzyjecieZewnetrzneZVAT_Error"] = ex.Message;
                }

                configs["DefaultConfigurations"] = defaults;

                // Find configurations without financial aspect that could be used to avoid VAT loading
                var noFinancialAspect = new List<Dictionary<string, object?>>();
                foreach (var config in pzConfigsList)
                {
                    if (config.TryGetValue("PosiadaAspektFinansowy", out var value))
                    {
                        string? valueStr = value?.ToString()?.ToLower();
                        if (valueStr == "false" || valueStr == "0")
                        {
                            noFinancialAspect.Add(config);
                        }
                    }
                }
                configs["PzConfigurationsWithoutFinancialAspect"] = noFinancialAspect;

                if (noFinancialAspect.Count == 0)
                {
                    configs["Recommendation"] = "No PZ configurations found without financial aspect. " +
                        "To avoid TransakcjaVAT errors, consider: " +
                        "1) Setting PosiadaAspektFinansowy=false on an existing PZ configuration in the database, OR " +
                        "2) Creating a new PZ configuration without financial aspect. " +
                        "SQL: UPDATE ModelDanychContainer.Konfiguracje SET PosiadaAspektFinansowy = 0 WHERE Symbol = 'PZ'";
                }
                else
                {
                    configs["Recommendation"] = $"Found {noFinancialAspect.Count} PZ configuration(s) without financial aspect. " +
                        "Use Utworz(configuration) with one of these to avoid TransakcjaVAT loading issues.";
                }

                return configs;
            });

            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(configResult));
        }
        catch (Exception ex)
        {
            results["OuterError"] = ex.Message;
            _logger.LogError(ex, "Error in ListPzConfigurations diagnostic");
            return Ok(ApiResponse<Dictionary<string, object?>>.Ok(results));
        }
    }

    private static string? SafeGetProperty(dynamic obj, string propertyName)
    {
        try
        {
            var type = ((object)obj).GetType();
            var prop = type.GetProperty(propertyName);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                return value?.ToString();
            }
            return null;
        }
        catch
        {
            return "Cannot read";
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
