using System.Text.RegularExpressions;
using crm_api.DTOs;
using crm_api.DTOs.ReportBuilderDto;
using crm_api.Interfaces;
using Microsoft.Data.SqlClient;

namespace crm_api.Services.ReportBuilderService
{
    public class ReportingCatalogService : IReportingCatalogService
    {
        private readonly IReportingConnectionService _connectionService;
        private readonly ILogger<ReportingCatalogService> _logger;
        private readonly ILocalizationService _localizationService;

        private static readonly Regex NameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

        public ReportingCatalogService(
            IReportingConnectionService connectionService,
            ILogger<ReportingCatalogService> logger,
            ILocalizationService localizationService)
        {
            _connectionService = connectionService;
            _logger = logger;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<List<FieldSchemaDto>>> CheckAndGetSchemaAsync(string connectionKey, string type, string name)
        {
            var connResp = _connectionService.ResolveConnectionString(connectionKey);
            if (!connResp.Success || string.IsNullOrEmpty(connResp.Data))
                return ApiResponse<List<FieldSchemaDto>>.ErrorResult(
                    connResp.Message ?? _localizationService.GetLocalizedString("ReportingCatalogService.InvalidConnection"),
                    null,
                    connResp.StatusCode);

            var typeNorm = type?.Trim().ToLowerInvariant() ?? "";
            if (typeNorm != "view" && typeNorm != "function")
                return ApiResponse<List<FieldSchemaDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportingCatalogService.InvalidType"),
                    null,
                    400);

            var nameTrim = name?.Trim() ?? "";
            if (!NameRegex.IsMatch(nameTrim))
                return ApiResponse<List<FieldSchemaDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportingCatalogService.InvalidDatasourceNameFormat"),
                    null,
                    400);

            var (schemaName, objectName) = ParseSchemaAndObject(nameTrim);

            try
            {
                await using var conn = new SqlConnection(connResp.Data);
                await conn.OpenAsync().ConfigureAwait(false);

                string objectTypeFilter = typeNorm == "view"
                    ? "AND o.type = 'V'"
                    : "AND o.type IN ('TF','IF')";

                var sql = @"
SELECT c.name AS ColumnName, t.name AS SqlTypeName, CAST(c.is_nullable AS BIT) AS IsNullable
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.columns c ON c.object_id = o.object_id
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE s.name = @schemaName AND o.name = @objectName " + objectTypeFilter + @"
ORDER BY c.column_id";

                var columns = new List<FieldSchemaDto>();
                await using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@schemaName", schemaName);
                    cmd.Parameters.AddWithValue("@objectName", objectName);
                    await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        columns.Add(new FieldSchemaDto
                        {
                            Name = reader.GetString(0),
                            DisplayName = ToDisplayName(reader.GetString(0)),
                            SemanticType = GetSemanticType(reader.GetString(1)),
                            DefaultAggregation = GetDefaultAggregation(reader.GetString(1)),
                            SqlType = reader.GetString(1),
                            DotNetType = MapSqlTypeToDotNet(reader.GetString(1)),
                            IsNullable = reader.GetBoolean(2)
                        });
                    }
                }

                if (columns.Count == 0)
                    return ApiResponse<List<FieldSchemaDto>>.SuccessResult(new List<FieldSchemaDto>(),
                        _localizationService.GetLocalizedString("ReportingCatalogService.ObjectNotFoundOrNoColumns"));

                return ApiResponse<List<FieldSchemaDto>>.SuccessResult(
                    columns,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckAndGetSchema {Key} {Type} {Name}", connectionKey, type, name);
                return ApiResponse<List<FieldSchemaDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportingCatalogService.CatalogReadError"),
                    ex.Message,
                    500);
            }
        }

        public async Task<ApiResponse<List<DataSourceCatalogItemDto>>> ListDataSourcesAsync(string connectionKey, string type, string? search)
        {
            var connResp = _connectionService.ResolveConnectionString(connectionKey);
            if (!connResp.Success || string.IsNullOrEmpty(connResp.Data))
                return ApiResponse<List<DataSourceCatalogItemDto>>.ErrorResult(
                    connResp.Message ?? _localizationService.GetLocalizedString("ReportingCatalogService.InvalidConnection"),
                    null,
                    connResp.StatusCode);

            var typeNorm = type?.Trim().ToLowerInvariant() ?? "";
            if (typeNorm != "view" && typeNorm != "function")
                return ApiResponse<List<DataSourceCatalogItemDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportingCatalogService.InvalidType"),
                    null,
                    400);

            try
            {
                await using var conn = new SqlConnection(connResp.Data);
                await conn.OpenAsync().ConfigureAwait(false);

                var searchTerm = search?.Trim();
                string sql;
                if (typeNorm == "view")
                {
                    sql = @"
SELECT TOP (100)
    s.name AS SchemaName,
    o.name AS ObjectName,
    CONCAT(s.name, '.', o.name) AS FullName,
    'view' AS Type
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type = 'V'
  AND (@search IS NULL OR o.name LIKE '%' + @search + '%' OR s.name LIKE '%' + @search + '%')
ORDER BY s.name, o.name";
                }
                else
                {
                    sql = @"
SELECT TOP (100)
    s.name AS SchemaName,
    o.name AS ObjectName,
    CONCAT(s.name, '.', o.name) AS FullName,
    'function' AS Type
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('TF','IF')
  AND (@search IS NULL OR o.name LIKE '%' + @search + '%' OR s.name LIKE '%' + @search + '%')
ORDER BY s.name, o.name";
                }

                var items = new List<DataSourceCatalogItemDto>();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(searchTerm) ? DBNull.Value : searchTerm);
                await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var schemaName = reader.GetString(0);
                    var objectName = reader.GetString(1);
                    var fullName = reader.GetString(2);
                    var itemType = reader.GetString(3);
                    items.Add(new DataSourceCatalogItemDto
                    {
                        SchemaName = schemaName,
                        ObjectName = objectName,
                        FullName = fullName,
                        Type = itemType,
                        DisplayName = $"{schemaName}.{objectName}"
                    });
                }

                return ApiResponse<List<DataSourceCatalogItemDto>>.SuccessResult(
                    items,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListDataSources {Key} {Type} {Search}", connectionKey, type, search);
                return ApiResponse<List<DataSourceCatalogItemDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportingCatalogService.CatalogReadError"),
                    ex.Message,
                    500);
            }
        }

        private static (string SchemaName, string ObjectName) ParseSchemaAndObject(string name)
        {
            var parts = name.Split('.');
            if (parts.Length >= 2)
                return (parts[0].Trim(), parts[^1].Trim());
            return ("dbo", name.Trim());
        }

        private static string MapSqlTypeToDotNet(string sqlType)
        {
            var t = sqlType.ToUpperInvariant();
            if (t.Contains("INT") && !t.Contains("BIGINT")) return "int";
            if (t == "BIGINT") return "long";
            if (t == "SMALLINT") return "short";
            if (t == "TINYINT") return "byte";
            if (t.Contains("DECIMAL") || t.Contains("NUMERIC")) return "decimal";
            if (t.Contains("FLOAT") || t == "REAL") return "double";
            if (t.Contains("CHAR") || t.Contains("TEXT") || t.Contains("XML")) return "string";
            if (t.Contains("DATE") || t.Contains("TIME")) return "DateTime";
            if (t == "BIT") return "bool";
            if (t.Contains("UNIQUEIDENTIFIER")) return "Guid";
            return "object";
        }

        private static string GetSemanticType(string sqlType)
        {
            var t = sqlType.ToUpperInvariant();
            if (t == "BIT") return "boolean";
            if (t.Contains("DATE") || t.Contains("TIME")) return "date";
            if (t.Contains("INT") || t.Contains("DECIMAL") || t.Contains("NUMERIC") || t.Contains("FLOAT") || t == "REAL" || t == "BIGINT" || t == "SMALLINT" || t == "TINYINT")
                return "number";
            if (t.Contains("CHAR") || t.Contains("TEXT") || t.Contains("XML") || t.Contains("UNIQUEIDENTIFIER"))
                return "text";
            return "other";
        }

        private static string GetDefaultAggregation(string sqlType)
        {
            return GetSemanticType(sqlType) == "number" ? "sum" : "count";
        }

        private static string ToDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var normalized = Regex.Replace(name.Replace("_", " "), "([a-z])([A-Z])", "$1 $2");
            return string.Join(" ", normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
        }
    }
}
