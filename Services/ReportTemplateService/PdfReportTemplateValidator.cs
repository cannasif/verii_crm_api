using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;

namespace crm_api.Services
{
    /// <summary>
    /// Validates PDF report template data: element limits, ids, types, table columns, path format, style ranges.
    /// </summary>
    public class PdfReportTemplateValidator : IPdfReportTemplateValidator
    {
        private const int MaxElements = 500;
        private const int MaxPathLength = 256;
        private const decimal MinColumnWidth = 1;
        private const decimal MaxColumnWidth = 10000;
        private const decimal OpacityMin = 0;
        private const decimal OpacityMax = 1;
        private const decimal RotationMin = -360;
        private const decimal RotationMax = 360;
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "text", "field", "image", "table" };
        private static readonly Regex PathSegmentRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public IReadOnlyList<string> ValidateTemplateData(ReportTemplateData? data, DocumentRuleType ruleType)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("TemplateData is required.");
                return errors;
            }

            if (data.SchemaVersion < 1)
                errors.Add("TemplateData.SchemaVersion must be greater than or equal to 1.");

            // Page
            if (data.Page == null)
                errors.Add("TemplateData.Page is required.");
            else
            {
                if (data.Page.Width <= 0 || data.Page.Height <= 0)
                    errors.Add("Page width and height must be positive.");
                if (string.IsNullOrWhiteSpace(data.Page.Unit))
                    errors.Add("Page unit is required.");
            }

            // Elements
            if (data.Elements == null)
            {
                errors.Add("TemplateData.Elements is required.");
                return errors;
            }

            if (data.Elements.Count > MaxElements)
                errors.Add($"Elements count must not exceed {MaxElements}.");

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < data.Elements.Count; i++)
            {
                var el = data.Elements[i];
                var prefix = $"Elements[{i}]";

                if (string.IsNullOrWhiteSpace(el.Id))
                    errors.Add($"{prefix}: Id is required and cannot be empty.");
                else
                {
                    if (seenIds.Contains(el.Id))
                        errors.Add($"{prefix}: Duplicate element Id '{el.Id}'.");
                    seenIds.Add(el.Id);
                }

                if (string.IsNullOrWhiteSpace(el.Type))
                    errors.Add($"{prefix}: Type is required.");
                else if (!AllowedTypes.Contains(el.Type))
                    errors.Add($"{prefix}: Unknown type '{el.Type}'. Allowed: text, field, image, table.");

                if (el.X < 0 || el.Y < 0)
                    errors.Add($"{prefix}: X and Y must be non-negative.");
                if (el.Width < 0 || el.Height < 0)
                    errors.Add($"{prefix}: Width and Height must be non-negative.");

                if (el.Type.Equals("field", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(el.Path))
                {
                    if (!ValidatePathFormat(el.Path, out var pathError))
                        errors.Add($"{prefix}: Path - {pathError}");
                }

                if (el.Type.Equals("table", StringComparison.OrdinalIgnoreCase))
                {
                    if (el.Columns == null || el.Columns.Count == 0)
                        errors.Add($"{prefix}: Table must have at least one column.");
                    else
                    {
                        foreach (var col in el.Columns)
                        {
                            if (string.IsNullOrWhiteSpace(col.Path))
                                errors.Add($"{prefix}: Table column Path is required.");
                            else if (!ValidatePathFormat(col.Path, out var colPathError))
                                errors.Add($"{prefix}: Column path - {colPathError}");
                        }
                        if (el.ColumnWidths != null && el.ColumnWidths.Count != el.Columns.Count)
                            errors.Add($"{prefix}: columnWidths length must match columns count.");
                        else if (el.ColumnWidths != null)
                        {
                            for (var ci = 0; ci < el.ColumnWidths.Count; ci++)
                            {
                                var cw = el.ColumnWidths[ci];
                                if (cw < MinColumnWidth || cw > MaxColumnWidth)
                                    errors.Add($"{prefix}: columnWidths[{ci}] must be between {MinColumnWidth} and {MaxColumnWidth}.");
                            }
                        }
                    }
                }

                if (el.Style != null)
                {
                    if (el.Style.Opacity.HasValue && (el.Style.Opacity < OpacityMin || el.Style.Opacity > OpacityMax))
                        errors.Add($"{prefix}: style.opacity must be between {OpacityMin} and {OpacityMax}.");
                    if (el.Rotation < RotationMin || el.Rotation > RotationMax)
                        errors.Add($"{prefix}: rotation must be between {RotationMin} and {RotationMax}.");
                    if (el.Style.LineHeight.HasValue && (el.Style.LineHeight < 0.5m || el.Style.LineHeight > 5m))
                        errors.Add($"{prefix}: style.lineHeight must be between 0.5 and 5.");
                }
                else if (el.Rotation != 0 && (el.Rotation < RotationMin || el.Rotation > RotationMax))
                    errors.Add($"{prefix}: rotation must be between {RotationMin} and {RotationMax}.");
            }

            return errors;
        }

        public IReadOnlyList<string> GetPlacementWarnings(ReportTemplateData? data)
        {
            var warnings = new List<string>();
            if (data?.Elements == null || data.Elements.Count < 2) return warnings;

            var elements = data.Elements.Where(e => e.Width > 0 && e.Height > 0).ToList();
            for (var i = 0; i < elements.Count; i++)
            {
                for (var j = i + 1; j < elements.Count; j++)
                {
                    var a = elements[i];
                    var b = elements[j];
                    if (Overlaps(a.X, a.Y, a.Width, a.Height, b.X, b.Y, b.Width, b.Height))
                        warnings.Add($"Elements '{a.Id}' and '{b.Id}' may overlap (layout warning).");
                }
            }
            return warnings;
        }

        private static bool Overlaps(decimal ax, decimal ay, decimal aw, decimal ah, decimal bx, decimal by, decimal bw, decimal bh)
        {
            return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
        }

        public IReadOnlyList<string> ValidateForGenerate(ReportTemplateData? data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Template data is required for generate.");
                return errors;
            }
            if (data.Page == null)
                errors.Add("Template data must have a valid page configuration.");
            if (data.Elements == null || data.Elements.Count == 0)
                errors.Add("Template data must have at least one element.");
            return errors;
        }

        private static bool ValidatePathFormat(string path, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength)
            {
                error = "Path must be non-empty and within length limit.";
                return false;
            }
            var segments = path.Split('.');
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg) || !PathSegmentRegex.IsMatch(seg))
                {
                    error = "Path must use valid identifiers (e.g. OfferNo, Lines.ProductCode).";
                    return false;
                }
            }
            return true;
        }
    }
}
