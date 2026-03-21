using System.Text.Json;
using System.Text.RegularExpressions;
using crm_api.DTOs;
using crm_api.DTOs.ReportBuilderDto;
using crm_api.Interfaces;
using Microsoft.Data.SqlClient;

namespace crm_api.Services.ReportBuilderService
{
    public class ReportPreviewService : IReportPreviewService
    {
        private const int MaxRows = 5000;
        private const int CommandTimeoutSeconds = 15;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly IReportingConnectionService _connectionService;
        private readonly IReportingCatalogService _catalogService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<ReportPreviewService> _logger;

        private static readonly Regex IdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
        private static readonly HashSet<string> FilterOps = new(StringComparer.OrdinalIgnoreCase)
            { "eq", "ne", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith", "in", "between" };
        private static readonly HashSet<string> CalculatedFieldOps = new(StringComparer.OrdinalIgnoreCase)
            { "add", "subtract", "multiply", "divide" };

        public ReportPreviewService(
            IReportingConnectionService connectionService,
            IReportingCatalogService catalogService,
            ILocalizationService localizationService,
            ILogger<ReportPreviewService> logger)
        {
            _connectionService = connectionService;
            _catalogService = catalogService;
            _localizationService = localizationService;
            _logger = logger;
        }

        public async Task<ApiResponse<PreviewResponseDto>> PreviewAsync(PreviewRequestDto request)
        {
            var connResp = _connectionService.ResolveConnectionString(request.ConnectionKey);
            if (!connResp.Success || string.IsNullOrEmpty(connResp.Data))
                return ApiResponse<PreviewResponseDto>.ErrorResult(connResp.Message ?? _localizationService.GetLocalizedString("ReportPreviewService.InvalidConnection"), null, connResp.StatusCode);

            var schemaResp = await _catalogService.CheckAndGetSchemaAsync(request.ConnectionKey, request.DataSourceType, request.DataSourceName).ConfigureAwait(false);
            if (!schemaResp.Success || schemaResp.Data == null)
                return ApiResponse<PreviewResponseDto>.ErrorResult(schemaResp.Message ?? _localizationService.GetLocalizedString("ReportPreviewService.SchemaNotFound"), schemaResp.ExceptionMessage, schemaResp.StatusCode);
            if (schemaResp.Data.Count == 0)
                return ApiResponse<PreviewResponseDto>.ErrorResult(_localizationService.GetLocalizedString("ReportPreviewService.DatasourceNotFoundOrEmpty"), null, 400);

            var schemaDict = schemaResp.Data.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

            ReportConfig config;
            try
            {
                config = JsonSerializer.Deserialize<ReportConfig>(request.ConfigJson, JsonOptions) ?? new ReportConfig();
            }
            catch (JsonException)
            {
                return ApiResponse<PreviewResponseDto>.ErrorResult(_localizationService.GetLocalizedString("ReportPreviewService.InvalidConfigJson"), _localizationService.GetLocalizedString("ReportPreviewService.InvalidConfigJson"), 400);
            }

            var err = ValidateConfigAgainstSchema(config, schemaDict);
            if (!string.IsNullOrEmpty(err))
                return ApiResponse<PreviewResponseDto>.ErrorResult(err, null, 400);

            var (schemaName, objectName) = ParseSchemaAndObject(request.DataSourceName.Trim());
            var typeNorm = request.DataSourceType.Trim().ToLowerInvariant();
            var fromClause = typeNorm == "function"
                ? $"[{Escape(schemaName)}].[{Escape(objectName)}]()"
                : $"[{Escape(schemaName)}].[{Escape(objectName)}]";

            var chartType = (config.ChartType ?? "table").Trim().ToLowerInvariant();
            if (chartType != "table" && chartType != "bar" && chartType != "stackedbar" && chartType != "line" && chartType != "pie" && chartType != "donut" && chartType != "kpi" && chartType != "matrix")
                chartType = "table";

            string selectClause;
            string? groupByClause = null;
            var calculatedFieldMap = BuildCalculatedFieldMap(config, schemaDict);

            if (chartType == "table")
            {
                var cols = new List<string>();
                if (config.Axis != null && !string.IsNullOrWhiteSpace(config.Axis.Field) && schemaDict.ContainsKey(config.Axis.Field.Trim()))
                    cols.Add($"[{Escape(config.Axis.Field!.Trim())}]");
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && schemaDict.ContainsKey(v.Field!.Trim())))
                        cols.Add($"[{Escape(v.Field!.Trim())}]");
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && calculatedFieldMap.ContainsKey(v.Field!.Trim()) && !cols.Any(c => c.Contains($"[{Escape(v.Field!.Trim())}]"))))
                        cols.Add($"{calculatedFieldMap[v.Field!.Trim()]} AS [{Escape(v.Field.Trim())}]");
                if (config.Legend != null && !string.IsNullOrWhiteSpace(config.Legend.Field) && schemaDict.ContainsKey(config.Legend.Field.Trim()))
                    cols.Add($"[{Escape(config.Legend.Field!.Trim())}]");
                if (cols.Count == 0 && config.Values != null && config.Values.Count > 0)
                    foreach (var v in config.Values.Take(1))
                        if (schemaDict.ContainsKey(v.Field!.Trim()))
                            cols.Add($"[{Escape(v.Field.Trim())}]");
                        else if (calculatedFieldMap.ContainsKey(v.Field!.Trim()))
                            cols.Add($"{calculatedFieldMap[v.Field!.Trim()]} AS [{Escape(v.Field.Trim())}]");
                selectClause = cols.Count > 0 ? string.Join(", ", cols) : "*";
            }
            else if (chartType == "kpi")
            {
                var selectCols = new List<string>();
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && schemaDict.ContainsKey(v.Field!.Trim())))
                    {
                        var agg = (v.Aggregation ?? "sum").Trim().ToLowerInvariant();
                        if (agg == "none") agg = "sum";
                        selectCols.Add($"{AggSql(agg)}([{Escape(v.Field!.Trim())}]) AS [{Escape(v.Field.Trim())}]");
                    }
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && calculatedFieldMap.ContainsKey(v.Field!.Trim())))
                    {
                        var agg = (v.Aggregation ?? "sum").Trim().ToLowerInvariant();
                        if (agg == "none") agg = "sum";
                        selectCols.Add($"{AggSql(agg)}({calculatedFieldMap[v.Field!.Trim()]}) AS [{Escape(v.Field.Trim())}]");
                    }

                if (selectCols.Count == 0)
                    return ApiResponse<PreviewResponseDto>.ErrorResult(_localizationService.GetLocalizedString("ReportPreviewService.ChartRequiresAxisLegendAndValue"), null, 400);

                selectClause = string.Join(", ", selectCols);
            }
            else
            {
                var selectCols = new List<string>();
                var groupCols = new List<string>();
                if (config.Axis != null && !string.IsNullOrWhiteSpace(config.Axis.Field) && schemaDict.ContainsKey(config.Axis.Field.Trim()))
                {
                    selectCols.Add($"[{Escape(config.Axis.Field!.Trim())}]");
                    groupCols.Add($"[{Escape(config.Axis.Field.Trim())}]");
                }
                if (config.Legend != null && !string.IsNullOrWhiteSpace(config.Legend.Field) && schemaDict.ContainsKey(config.Legend.Field.Trim()))
                {
                    selectCols.Add($"[{Escape(config.Legend.Field!.Trim())}]");
                    groupCols.Add($"[{Escape(config.Legend.Field.Trim())}]");
                }
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && schemaDict.ContainsKey(v.Field!.Trim())))
                    {
                        var agg = (v.Aggregation ?? "sum").Trim().ToLowerInvariant();
                        if (agg == "none") agg = "sum";
                        selectCols.Add($"{AggSql(agg)}([{Escape(v.Field!.Trim())}]) AS [{Escape(v.Field.Trim())}]");
                    }
                if (config.Values != null)
                    foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field) && calculatedFieldMap.ContainsKey(v.Field!.Trim())))
                    {
                        var agg = (v.Aggregation ?? "sum").Trim().ToLowerInvariant();
                        if (agg == "none") agg = "sum";
                        selectCols.Add($"{AggSql(agg)}({calculatedFieldMap[v.Field!.Trim()]}) AS [{Escape(v.Field.Trim())}]");
                    }
                if (selectCols.Count == 0)
                    return ApiResponse<PreviewResponseDto>.ErrorResult(_localizationService.GetLocalizedString("ReportPreviewService.ChartRequiresAxisLegendAndValue"), null, 400);
                selectClause = string.Join(", ", selectCols);
                if (groupCols.Count > 0)
                    groupByClause = " GROUP BY " + string.Join(", ", groupCols);
            }

            var whereClause = "";
            var parameters = new List<ParamInfo>();
            if (config.Filters != null && config.Filters.Count > 0)
            {
                var conds = new List<string>();
                foreach (var f in config.Filters)
                {
                    var field = f.Field?.Trim();
                    if (string.IsNullOrEmpty(field) || !schemaDict.ContainsKey(field!)) continue;
                    var op = (f.Operator ?? "eq").Trim().ToLowerInvariant();
                    if (!FilterOps.Contains(op)) continue;
                    var paramName = $"@p{parameters.Count}";
                    if (op == "in" && f.Values != null)
                    {
                        var inParams = new List<string>();
                        foreach (var v in f.Values.Take(100))
                        {
                            var pn = $"@p{parameters.Count}";
                            parameters.Add(new ParamInfo { Name = pn, Value = v });
                            inParams.Add(pn);
                        }
                        if (inParams.Count > 0)
                            conds.Add($"[{Escape(field)}] IN ({string.Join(",", inParams)})");
                    }
                    else if (op == "between")
                    {
                        var fromVal = f.From ?? f.Values?.ElementAtOrDefault(0);
                        var toVal = f.To ?? f.Values?.ElementAtOrDefault(1);
                        var p0 = $"@p{parameters.Count}";
                        var p1 = $"@p{parameters.Count + 1}";
                        parameters.Add(new ParamInfo { Name = p0, Value = fromVal });
                        parameters.Add(new ParamInfo { Name = p1, Value = toVal });
                        conds.Add($"[{Escape(field)}] BETWEEN {p0} AND {p1}");
                    }
                    else
                    {
                        var value = f.Value;
                        parameters.Add(new ParamInfo { Name = paramName, Value = value });
                        conds.Add(FilterCondition(field!, op, paramName));
                    }
                }
                if (conds.Count > 0)
                    whereClause = " WHERE " + string.Join(" AND ", conds);
            }

            var orderClause = "";
            if (config.Sorting != null)
            {
                var by = (config.Sorting.By ?? "axis").Trim().ToLowerInvariant();
                var dir = (config.Sorting.Direction ?? "asc").Trim().ToLowerInvariant();
                if (dir != "asc" && dir != "desc") dir = "asc";
                if (by == "value" && !string.IsNullOrWhiteSpace(config.Sorting.ValueField) && schemaDict.ContainsKey(config.Sorting.ValueField.Trim()))
                    orderClause = $" ORDER BY [{Escape(config.Sorting.ValueField!.Trim())}] {dir}";
                else if (config.Axis != null && !string.IsNullOrWhiteSpace(config.Axis.Field) && schemaDict.ContainsKey(config.Axis.Field.Trim()))
                    orderClause = $" ORDER BY [{Escape(config.Axis.Field!.Trim())}] {dir}";
            }
            if (chartType == "kpi")
                orderClause = string.Empty;
            else if (string.IsNullOrEmpty(orderClause))
                orderClause = " ORDER BY (SELECT NULL)";

            var sql = $"SELECT TOP({MaxRows}) {selectClause} FROM {fromClause}{whereClause}{groupByClause}{orderClause}";

            try
            {
                await using var conn = new SqlConnection(connResp.Data);
                await conn.OpenAsync().ConfigureAwait(false);
                var columns = new List<FieldSchemaDto>();
                var rows = new List<Dictionary<string, object?>>();
                await using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    foreach (var p in parameters)
                    {
                        cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
                    }
                    await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(new FieldSchemaDto
                        {
                            Name = reader.GetName(i),
                            SqlType = reader.GetDataTypeName(i),
                            DotNetType = "object",
                            IsNullable = true
                        });
                    }
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++)
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }
                }
                return ApiResponse<PreviewResponseDto>.SuccessResult(
                    new PreviewResponseDto { Columns = columns, Rows = rows }, _localizationService.GetLocalizedString("ReportPreviewService.PreviewOk"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preview execution");
                return ApiResponse<PreviewResponseDto>.ErrorResult(_localizationService.GetLocalizedString("ReportPreviewService.PreviewFailed"), _localizationService.GetLocalizedString("ReportPreviewService.PreviewFailed"), 500);
            }
        }

        private static (string SchemaName, string ObjectName) ParseSchemaAndObject(string name)
        {
            var parts = name.Split('.');
            if (parts.Length >= 2)
                return (parts[0].Trim(), parts[^1].Trim());
            return ("dbo", name.Trim());
        }

        private static string Escape(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("]", "]]");
        }

        private string? ValidateConfigAgainstSchema(ReportConfig config, Dictionary<string, FieldSchemaDto> schemaDict)
        {
            var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.Axis?.Field != null) allFields.Add(config.Axis.Field.Trim());
            if (config.Legend?.Field != null) allFields.Add(config.Legend.Field.Trim());
            if (config.Values != null)
                foreach (var v in config.Values.Where(v => !string.IsNullOrWhiteSpace(v.Field)))
                    allFields.Add(v.Field!.Trim());
            if (config.Filters != null)
                foreach (var f in config.Filters.Where(f => !string.IsNullOrWhiteSpace(f.Field)))
                    allFields.Add(f.Field!.Trim());
            if (config.Sorting?.ValueField != null) allFields.Add(config.Sorting.ValueField.Trim());
            foreach (var f in allFields)
                if (!schemaDict.ContainsKey(f) && !(config.CalculatedFields?.Any(c => string.Equals(c.Name?.Trim(), f, StringComparison.OrdinalIgnoreCase)) ?? false))
                    return _localizationService.GetLocalizedString("ReportPreviewService.FieldNotInSchema", f);

            if (config.CalculatedFields != null)
            {
                foreach (var field in config.CalculatedFields)
                {
                    if (string.IsNullOrWhiteSpace(field.Name) || string.IsNullOrWhiteSpace(field.LeftField) || string.IsNullOrWhiteSpace(field.RightField))
                        return "Calculated field definition is incomplete.";
                    if (!CalculatedFieldOps.Contains(field.Operation ?? ""))
                        return "Calculated field operation is invalid.";
                    if (!schemaDict.ContainsKey(field.LeftField.Trim()) || !schemaDict.ContainsKey(field.RightField.Trim()))
                        return "Calculated field references a field outside schema.";
                }
            }
            return null;
        }

        private static Dictionary<string, string> BuildCalculatedFieldMap(ReportConfig config, Dictionary<string, FieldSchemaDto> schemaDict)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in config.CalculatedFields ?? new List<CalculatedFieldConfig>())
            {
                if (string.IsNullOrWhiteSpace(field.Name) || string.IsNullOrWhiteSpace(field.LeftField) || string.IsNullOrWhiteSpace(field.RightField))
                    continue;
                if (!schemaDict.ContainsKey(field.LeftField.Trim()) || !schemaDict.ContainsKey(field.RightField.Trim()))
                    continue;

                var left = $"TRY_CONVERT(decimal(18,4), [{Escape(field.LeftField.Trim())}])";
                var right = $"TRY_CONVERT(decimal(18,4), [{Escape(field.RightField.Trim())}])";
                var expr = (field.Operation ?? "add").Trim().ToLowerInvariant() switch
                {
                    "subtract" => $"({left} - {right})",
                    "multiply" => $"({left} * {right})",
                    "divide" => $"({left} / NULLIF({right}, 0))",
                    _ => $"({left} + {right})",
                };
                map[field.Name.Trim()] = expr;
            }

            return map;
        }

        private static string AggSql(string agg)
        {
            return agg switch { "sum" => "SUM", "count" => "COUNT", "avg" => "AVG", "min" => "MIN", "max" => "MAX", _ => "SUM" };
        }

        private static string FilterCondition(string field, string op, string paramName)
        {
            return op switch
            {
                "eq" => $"[{Escape(field)}] = {paramName}",
                "ne" => $"[{Escape(field)}] <> {paramName}",
                "gt" => $"[{Escape(field)}] > {paramName}",
                "gte" => $"[{Escape(field)}] >= {paramName}",
                "lt" => $"[{Escape(field)}] < {paramName}",
                "lte" => $"[{Escape(field)}] <= {paramName}",
                "contains" => $"[{Escape(field)}] LIKE '%' + {paramName} + '%'",
                "startswith" => $"[{Escape(field)}] LIKE {paramName} + '%'",
                "endswith" => $"[{Escape(field)}] LIKE '%' + {paramName}",
                _ => $"[{Escape(field)}] = {paramName}"
            };
        }

        private class ParamInfo
        {
            public string Name { get; set; } = "";
            public object? Value { get; set; }
        }
    }
}
