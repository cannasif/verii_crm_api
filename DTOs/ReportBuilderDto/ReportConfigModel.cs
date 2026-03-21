using System.Text.Json.Serialization;

namespace crm_api.DTOs.ReportBuilderDto
{
    public class ReportConfig
    {
        [JsonPropertyName("chartType")]
        public string ChartType { get; set; } = "table";

        [JsonPropertyName("axis")]
        public AxisConfig? Axis { get; set; }

        [JsonPropertyName("values")]
        public List<ValueMapping> Values { get; set; } = new();

        [JsonPropertyName("legend")]
        public LegendConfig? Legend { get; set; }

        [JsonPropertyName("sorting")]
        public SortingConfig? Sorting { get; set; }

        [JsonPropertyName("filters")]
        public List<FilterRule> Filters { get; set; } = new();

        [JsonPropertyName("calculatedFields")]
        public List<CalculatedFieldConfig> CalculatedFields { get; set; } = new();
    }

    public class AxisConfig
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("dateGrouping")]
        public string? DateGrouping { get; set; }
    }

    public class ValueMapping
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("aggregation")]
        public string Aggregation { get; set; } = "none";
    }

    public class LegendConfig
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;
    }

    public class SortingConfig
    {
        [JsonPropertyName("by")]
        public string By { get; set; } = "axis";

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "asc";

        [JsonPropertyName("valueField")]
        public string? ValueField { get; set; }
    }

    public class FilterRule
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "eq";

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("values")]
        public List<object?>? Values { get; set; }

        [JsonPropertyName("from")]
        public object? From { get; set; }

        [JsonPropertyName("to")]
        public object? To { get; set; }
    }

    public class CalculatedFieldConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("leftField")]
        public string LeftField { get; set; } = string.Empty;

        [JsonPropertyName("rightField")]
        public string RightField { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "add";
    }
}
