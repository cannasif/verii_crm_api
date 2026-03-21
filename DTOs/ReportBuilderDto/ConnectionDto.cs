namespace crm_api.DTOs.ReportBuilderDto
{
    public class ConnectionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class FieldSchemaDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SemanticType { get; set; } = string.Empty;
        public string DefaultAggregation { get; set; } = string.Empty;
        public string SqlType { get; set; } = string.Empty;
        public string DotNetType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
    }

    public class DataSourceCatalogItemDto
    {
        public string SchemaName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
