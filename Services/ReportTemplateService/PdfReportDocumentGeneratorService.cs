using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;

namespace crm_api.Services
{
    /// <summary>
    /// PDF report document generator: true absolute layout, SSRF-safe images, structured logging.
    /// </summary>
    public class PdfReportDocumentGeneratorService : IPdfReportDocumentGeneratorService
    {
        private static readonly QuotationTotalsLayoutSpec QuotationTotalsSpec = LoadQuotationTotalsLayoutSpec();
        private static readonly ReportRegionPaginationSpec ReportRegionSpec = LoadReportRegionPaginationSpec();
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PdfReportDocumentGeneratorService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PdfBuilderOptions _options;
        private readonly IErpService? _erpService;

        public PdfReportDocumentGeneratorService(
            IUnitOfWork unitOfWork,
            ILogger<PdfReportDocumentGeneratorService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<PdfBuilderOptions> options,
            IErpService? erpService = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = options?.Value ?? new PdfBuilderOptions();
            _erpService = erpService;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GeneratePdfAsync(DocumentRuleType ruleType, long entityId, ReportTemplateData templateData)
        {
            if (templateData == null)
                throw new ArgumentNullException(nameof(templateData));

            var sw = Stopwatch.StartNew();
            var elementCount = templateData.Elements?.Count ?? 0;
            var warningCount = 0;

            try
            {
                var entityData = await FetchEntityDataAsync(ruleType, entityId).ConfigureAwait(false);
                if (entityData == null)
                    throw new InvalidOperationException($"Entity with ID {entityId} not found for rule type {ruleType}");

                var pdfBytes = await GeneratePdfForEntityDataAsync(templateData, entityData).ConfigureAwait(false);
                sw.Stop();

                _logger.LogInformation(
                    "PdfReportDocumentGenerator completed: RuleType={RuleType}, EntityId={EntityId}, ElementCount={ElementCount}, DurationMs={DurationMs}, WarningCount={WarningCount}",
                    ruleType, entityId, elementCount, sw.ElapsedMilliseconds, warningCount);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "PdfReportDocumentGenerator failed: RuleType={RuleType}, EntityId={EntityId}, ElementCount={ElementCount}, DurationMs={DurationMs}",
                    ruleType, entityId, elementCount, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task<byte[]> GeneratePdfForEntityDataAsync(ReportTemplateData templateData, object entityData)
        {
            if (templateData == null)
                throw new ArgumentNullException(nameof(templateData));
            if (entityData == null)
                throw new ArgumentNullException(nameof(entityData));

            var warningCount = 0;

            var imageCache = await PreFetchImagesAsync(templateData.Elements ?? new List<ReportElement>(), entityData, (reason) =>
            {
                _logger.LogWarning("PdfReportDocumentGenerator SSRF reject: {Reason}", reason);
                warningCount++;
            }).ConfigureAwait(false);

            var page = templateData.Page ?? new PageConfig();
            var unit = page.Unit ?? "px";
            var pageWidthPt = PdfUnitConversion.ToPointsFloat(page.Width, unit);
            var pageHeightPt = PdfUnitConversion.ToPointsFloat(page.Height, unit);

            var orderedElements = ResolveLayoutElements(templateData.Elements ?? new List<ReportElement>())
                .OrderBy(e => e.ZIndex)
                .ThenBy(e => e.Y)
                .ThenBy(e => e.X)
                .ToList();

            var flowRegionPdf = TryGenerateFlowRegionPdf(orderedElements, entityData, unit, imageCache, pageWidthPt, pageHeightPt, () => warningCount++);
            if (flowRegionPdf != null)
                return flowRegionPdf;

            var totalPages = Math.Max(1, page.PageCount);
            var document = Document.Create(container =>
            {
                for (var pageNumber = 1; pageNumber <= totalPages; pageNumber++)
                {
                    var currentPage = pageNumber;
                    container.Page(p =>
                    {
                        p.Size(new PageSize(pageWidthPt, pageHeightPt));
                        p.Margin(0);

                        p.Content().Layers(layers =>
                        {
                            layers.PrimaryLayer().Width(pageWidthPt).Height(pageHeightPt).Background(Colors.Transparent);
                            foreach (var element in orderedElements.Where(element => ShouldRenderOnPage(element, currentPage)))
                            {
                                var xPt = PdfUnitConversion.ToPointsFloat(element.X, unit);
                                var yPt = PdfUnitConversion.ToPointsFloat(element.Y, unit);
                                var wPt = PdfUnitConversion.ToPointsFloat(element.Width > 0 ? element.Width : 200, unit);
                                var hPt = PdfUnitConversion.ToPointsFloat(element.Height > 0 ? element.Height : 50, unit);

                                var layer = layers.Layer()
                                    .TranslateX(xPt)
                                    .TranslateY(yPt)
                                    .Width(wPt);

                                layer = element.Type?.Equals("table", StringComparison.OrdinalIgnoreCase) == true
                                    ? layer.MinHeight(hPt)
                                    : layer.Height(hPt);

                                layer.Element(c => WrapElementWithStyle(c, element, inner =>
                                {
                                    RenderElement(inner, element, entityData, unit, imageCache, () => warningCount++);
                                }));
                            }
                        });
                    });
                }
            });

            return document.GeneratePdf();
        }

        private void WrapElementWithStyle(IContainer container, ReportElement element, Action<IContainer> renderContent)
        {
            var style = element.Style;
            var rotation = ClampRotation(element.Rotation);
            var padding = PdfUnitConversion.ToPointsFloat(style?.Padding ?? 0, "px");
            var bg = style?.Background;
            var border = style?.Border;

            var c = container;
            if (rotation != 0)
                c = c.Rotate(rotation);
            if (padding > 0)
                c = c.Padding(padding);
            if (!string.IsNullOrEmpty(bg))
            {
                try { c = c.Background(bg); }
                catch (Exception ex) { _logger.LogDebug(ex, "PdfReportDocumentGenerator style apply failed: Background={Bg}", bg); }
            }
            if (!string.IsNullOrEmpty(border))
            {
                try
                {
                    var (borderWidth, borderColor) = ParseBorderSpec(border, "px");
                    if (borderWidth > 0)
                        c = c.Border(borderWidth);
                    if (!string.IsNullOrWhiteSpace(borderColor))
                        c = c.BorderColor(borderColor);
                }
                catch (Exception ex) { _logger.LogDebug(ex, "PdfReportDocumentGenerator style apply failed: Border={Border}", border); }
            }

            c.Element(inner => renderContent(inner));
        }

        private static bool ShouldRenderOnPage(ReportElement element, int pageNumber)
        {
            if (element.PageNumbers == null || element.PageNumbers.Count == 0)
                return true;

            return element.PageNumbers.Contains(pageNumber);
        }

        private static float ClampRotation(decimal v)
        {
            var x = (float)v;
            while (x > 360) x -= 360;
            while (x < -360) x += 360;
            return x;
        }

        private async Task<Dictionary<string, byte[]>> PreFetchImagesAsync(
            List<ReportElement> elements,
            object entityData,
            Action<string> onReject)
        {
            var cache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var imageElements = elements.Where(e => "image".Equals(e.Type, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var el in imageElements)
            {
                var source = ResolveImageSource(el, entityData);
                if (string.IsNullOrWhiteSpace(source)) continue;
                var key = source.Trim();
                if (cache.ContainsKey(key)) continue;

                if (key.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!PdfImageUrlValidator.IsDataUri(key, _options.AllowedImageContentTypes, out var reason))
                    {
                        _logger.LogWarning("PdfReportDocumentGenerator SSRF/data reject: Reason={Reason}, SourceLength={Len}",
                            reason, key.Length);
                        onReject(reason ?? "Invalid data URI");
                        continue;
                    }
                    try
                    {
                        var base64 = key.IndexOf(',') >= 0 ? key.Split(',')[1] : key;
                        var bytes = Convert.FromBase64String(base64);
                        if (bytes.Length > _options.MaxImageSizeBytes)
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator image size exceeded: MaxBytes={Max}, Actual={Actual}",
                                _options.MaxImageSizeBytes, bytes.Length);
                            onReject("Image exceeds max size");
                            continue;
                        }
                        if (!ValidateImageContentType(bytes, out var ctReject))
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator Content-Type reject: {Reason}", ctReject);
                            onReject(ctReject ?? "Invalid content type");
                            continue;
                        }
                        cache[key] = bytes;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PdfReportDocumentGenerator data URI decode failed");
                        onReject("Failed to decode data URI");
                    }
                    continue;
                }

                // Local/relative file path (e.g. /uploads/stock-images/123/abc.jpg)
                if (!key.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(_options.LocalImageBasePath))
                    {
                        _logger.LogWarning("PdfReportDocumentGenerator local image skipped (LocalImageBasePath not configured): Path={Path}", key);
                        onReject("Local image base path not configured");
                        continue;
                    }

                    try
                    {
                        var sanitized = key.TrimStart('/').Replace('\\', '/');
                        if (sanitized.Contains(".."))
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator path traversal rejected: Path={Path}", key);
                            onReject("Path traversal not allowed");
                            continue;
                        }

                        var baseFull = System.IO.Path.GetFullPath(_options.LocalImageBasePath);
                        var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(_options.LocalImageBasePath, sanitized));
                        if (!fullPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator path outside base rejected: Path={Path}, Resolved={Resolved}", key, fullPath);
                            onReject("Path is outside allowed base directory");
                            continue;
                        }

                        if (!System.IO.File.Exists(fullPath))
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator local image not found: Path={Path}", fullPath);
                            onReject("Local image file not found");
                            continue;
                        }

                        var localBytes = await System.IO.File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
                        if (localBytes.Length > _options.MaxImageSizeBytes)
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator local image size exceeded: MaxBytes={Max}, Actual={Actual}, Path={Path}",
                                _options.MaxImageSizeBytes, localBytes.Length, key);
                            onReject("Image exceeds max size");
                            continue;
                        }
                        if (localBytes.Length > 0 && !ValidateImageContentType(localBytes, out var localCtReject))
                        {
                            _logger.LogWarning("PdfReportDocumentGenerator local image Content-Type reject: {Reason}, Path={Path}", localCtReject, key);
                            onReject(localCtReject ?? "Invalid content type");
                            continue;
                        }
                        if (localBytes.Length > 0)
                            cache[key] = localBytes;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PdfReportDocumentGenerator local image read failed: Path={Path}", key);
                        onReject("Failed to read local image");
                    }
                    continue;
                }

                if (!PdfImageUrlValidator.IsUrlAllowed(key, _options.AllowlistedImageHosts, out var urlReason))
                {
                    _logger.LogWarning("PdfReportDocumentGenerator SSRF URL reject: Reason={Reason}, Url={Url}",
                        urlReason, key);
                    onReject(urlReason ?? "URL not allowed");
                    continue;
                }

                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(_options.ImageFetchTimeoutSeconds));
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(_options.ImageFetchTimeoutSeconds);
                    var bytes = await httpClient.GetByteArrayAsync(key, cts.Token).ConfigureAwait(false);
                    if (bytes != null && bytes.Length > _options.MaxImageSizeBytes)
                    {
                        _logger.LogWarning("PdfReportDocumentGenerator URL image size exceeded: MaxBytes={Max}, Actual={Actual}, Url={Url}",
                            _options.MaxImageSizeBytes, bytes?.Length ?? 0, key);
                        onReject("Image exceeds max size");
                        continue;
                    }
                    if (bytes != null && bytes.Length > 0 && !ValidateImageContentType(bytes, out var ctReject2))
                    {
                        _logger.LogWarning("PdfReportDocumentGenerator URL Content-Type reject: {Reason}, Url={Url}", ctReject2, key);
                        onReject(ctReject2 ?? "Invalid content type");
                        continue;
                    }
                    if (bytes != null && bytes.Length > 0)
                        cache[key] = bytes;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PdfReportDocumentGenerator URL fetch failed: Url={Url}", key);
                    onReject("Failed to fetch image");
                }
            }

            return cache;
        }

        private string? ResolveImageSource(ReportElement element, object entityData)
        {
            if (!string.IsNullOrWhiteSpace(element.Value))
            {
                return element.Value?.Trim();
            }

            if (!string.IsNullOrWhiteSpace(element.Path))
            {
                var resolved = ResolvePropertyPath(entityData, element.Path);
                var resolvedString = resolved?.ToString();
                if (!string.IsNullOrWhiteSpace(resolvedString))
                    return resolvedString.Trim();
            }

            return null;
        }

        private bool ValidateImageContentType(byte[] bytes, out string? rejectReason)
        {
            rejectReason = null;
            if (bytes == null || bytes.Length < 12) return true;
            var allowed = _options.AllowedImageContentTypes;
            if (allowed == null || allowed.Count == 0) return true;

            string? detected = null;
            var isPng = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E;
            if (isPng) detected = "image/png";

            var isJpeg = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
            if (detected == null && isJpeg) detected = "image/jpeg";

            var isGif = bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46;
            if (detected == null && isGif) detected = "image/gif";

            var isWebp = bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
            if (detected == null && isWebp) detected = "image/webp";

            if (detected == null)
            {
                rejectReason = "Content is not a supported image format.";
                return false;
            }

            var allowedSet = allowed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .ToHashSet();

            if (allowedSet.Count == 0 || allowedSet.Contains(detected))
                return true;

            rejectReason = $"Content type '{detected}' is not allowed.";
            return false;
        }

        private void RenderElement(IContainer container, ReportElement element, object entityData, string unit,
            Dictionary<string, byte[]> imageCache, Action onWarning)
        {
            switch (element.Type?.ToLower())
            {
                case "text":
                    RenderText(container, element);
                    break;
                case "field":
                    RenderField(container, element, entityData);
                    break;
                case "image":
                    RenderImage(container, element, entityData, imageCache, onWarning);
                    break;
                case "table":
                    RenderTable(container, element, entityData, unit);
                    break;
                case "note":
                    RenderNote(container, element, entityData);
                    break;
                case "summary":
                    RenderSummary(container, element, entityData);
                    break;
                case "quotationtotals":
                    RenderQuotationTotals(container, element, entityData);
                    break;
                case "shape":
                case "container":
                    RenderShape(container, element);
                    break;
                default:
                    onWarning();
                    break;
            }
        }

        private void RenderShape(IContainer container, ReportElement element)
        {
            // Shape primitives are rendered by the wrapper style/background/border layer.
            container.MinHeight(1);
        }

        private byte[]? TryGenerateFlowRegionPdf(
            List<ReportElement> orderedElements,
            object entityData,
            string unit,
            Dictionary<string, byte[]> imageCache,
            float pageWidthPt,
            float pageHeightPt,
            Action onWarning)
        {
            var regionTable = orderedElements.FirstOrDefault(element =>
                string.Equals(element.Type, "table", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(element.TableOptions?.ReportRegionMode, "flow", StringComparison.OrdinalIgnoreCase));

            if (regionTable == null || regionTable.Columns == null || regionTable.Columns.Count == 0)
                return null;

            var rows = GetTableRows(regionTable, entityData);
            if (rows.Count == 0)
                return null;

            var flowIds = new HashSet<string>((regionTable.TableOptions?.FlowElementIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
            var continuationIds = new HashSet<string>((regionTable.TableOptions?.ContinuationElementIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
            var repeatedIds = new HashSet<string>((regionTable.TableOptions?.RepeatedElementIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);

            var flowElements = orderedElements.Where(element => flowIds.Contains(element.Id)).ToList();
            var continuationElements = orderedElements.Where(element => continuationIds.Contains(element.Id)).ToList();
            var repeatedElements = orderedElements.Where(element => repeatedIds.Contains(element.Id)).ToList();

            var excludedIds = new HashSet<string>(flowIds, StringComparer.OrdinalIgnoreCase);
            excludedIds.UnionWith(continuationIds);
            excludedIds.UnionWith(repeatedIds);
            excludedIds.Add(regionTable.Id);

            var firstPageElements = orderedElements.Where(element => !excludedIds.Contains(element.Id)).ToList();
            var pageRowChunks = PaginateTableRows(regionTable, rows);
            if (pageRowChunks.Count == 0)
                pageRowChunks.Add(rows);

            var document = Document.Create(container =>
            {
                for (var pageIndex = 0; pageIndex < pageRowChunks.Count; pageIndex++)
                {
                    var isFirstPage = pageIndex == 0;
                    var isLastPage = pageIndex == pageRowChunks.Count - 1;
                    var currentRows = pageRowChunks[pageIndex];

                    container.Page(page =>
                    {
                        page.Size(new PageSize(pageWidthPt, pageHeightPt));
                        page.Margin(0);
                        page.Content().Layers(layers =>
                        {
                            layers.PrimaryLayer().Width(pageWidthPt).Height(pageHeightPt).Background(Colors.Transparent);

                            foreach (var element in repeatedElements)
                                RenderLayeredElement(layers, element, entityData, unit, imageCache, onWarning);

                            if (isFirstPage)
                            {
                                foreach (var element in firstPageElements)
                                    RenderLayeredElement(layers, element, entityData, unit, imageCache, onWarning);
                            }
                            else if (continuationElements.Count > 0)
                            {
                                RenderContinuationHeaderRegion(layers, continuationElements, unit);
                            }

                            var tableY = isFirstPage ? regionTable.Y : GetFlowContinuationTableTop(unit);
                            RenderLayeredTablePage(layers, regionTable, currentRows, entityData, unit, tableY);

                            if (isLastPage)
                            {
                                if (flowElements.Count > 0)
                                    RenderFlowFooterRegion(layers, flowElements, entityData, unit, imageCache, onWarning);
                            }
                        });
                    });
                }
            });

            return document.GeneratePdf();
        }

        private void RenderLayeredElement(
            LayersDescriptor layers,
            ReportElement element,
            object entityData,
            string unit,
            Dictionary<string, byte[]> imageCache,
            Action onWarning)
        {
            var xPt = PdfUnitConversion.ToPointsFloat(element.X, unit);
            var yPt = PdfUnitConversion.ToPointsFloat(element.Y, unit);
            var wPt = PdfUnitConversion.ToPointsFloat(element.Width > 0 ? element.Width : 200, unit);
            var hPt = PdfUnitConversion.ToPointsFloat(element.Height > 0 ? element.Height : 50, unit);

            var layer = layers.Layer()
                .TranslateX(xPt)
                .TranslateY(yPt)
                .Width(wPt);

            layer = element.Type?.Equals("table", StringComparison.OrdinalIgnoreCase) == true
                ? layer.MinHeight(hPt)
                : layer.Height(hPt);

            layer.Element(c => WrapElementWithStyle(c, element, inner =>
            {
                RenderElement(inner, element, entityData, unit, imageCache, onWarning);
            }));
        }

        private void RenderLayeredTablePage(
            LayersDescriptor layers,
            ReportElement element,
            List<object> rows,
            object entityData,
            string unit,
            decimal yOverride)
        {
            var xPt = PdfUnitConversion.ToPointsFloat(element.X, unit);
            var yPt = PdfUnitConversion.ToPointsFloat(yOverride, unit);
            var wPt = PdfUnitConversion.ToPointsFloat(element.Width > 0 ? element.Width : 200, unit);
            var hPt = PdfUnitConversion.ToPointsFloat(element.Height > 0 ? element.Height : 50, unit);

            layers.Layer()
                .TranslateX(xPt)
                .TranslateY(yPt)
                .Width(wPt)
                .MinHeight(hPt)
                .Element(c => WrapElementWithStyle(c, element, inner =>
                {
                    RenderTable(inner, element, entityData, unit, rows);
                }));
        }

        private static decimal GetFlowContinuationTableTop(string unit)
        {
            if (string.Equals(unit, "mm", StringComparison.OrdinalIgnoreCase))
                return 8m;

            return 30m;
        }

        private void RenderContinuationHeaderRegion(
            LayersDescriptor layers,
            List<ReportElement> continuationElements,
            string unit)
        {
            var strip = continuationElements.FirstOrDefault(element => string.Equals(element.Type, "shape", StringComparison.OrdinalIgnoreCase));
            if (strip != null)
            {
                RenderLayeredElement(
                    layers,
                    new ReportElement
                    {
                        Id = strip.Id,
                        Type = "shape",
                        X = 0,
                        Y = 0,
                        Width = strip.Width,
                        Height = strip.Height,
                        Style = strip.Style,
                    },
                    new object(),
                    unit,
                    new Dictionary<string, byte[]>(),
                    () => { });
            }
        }

        private void RenderFlowFooterRegion(
            LayersDescriptor layers,
            List<ReportElement> flowElements,
            object entityData,
            string unit,
            Dictionary<string, byte[]> imageCache,
            Action onWarning)
        {
            var grossAmount = ToDecimal(ResolvePropertyPath(entityData, "Total")) + ToDecimal(ResolvePropertyPath(entityData, "GeneralDiscountAmount"));
            var discountAmount = ToDecimal(ResolvePropertyPath(entityData, "GeneralDiscountAmount"));
            var netAmount = ToDecimal(ResolvePropertyPath(entityData, "Total"));
            var grandAmount = ToDecimal(ResolvePropertyPath(entityData, "GrandTotal"));
            var vatAmount = grandAmount - netAmount;
            var currencyCode = ResolvePropertyPath(entityData, "Currency")?.ToString();
            var vatRate = ResolveFirstLineVatRate(entityData);
            var footerTitle = flowElements.FirstOrDefault(element => element.Id.Equals("footer-title", StringComparison.OrdinalIgnoreCase))?.Text
                ?? "TEKLİF ŞARTLARI VE ÖNEMLİ NOTLAR";
            var refsTitle = flowElements.FirstOrDefault(element => element.Id.Equals("refs-title", StringComparison.OrdinalIgnoreCase))?.Text
                ?? "SAHA VE KEŞİF GÖRSELLERİ (REFERANS)";
            var refsCopy = flowElements.FirstOrDefault(element => element.Id.Equals("refs-copy", StringComparison.OrdinalIgnoreCase))?.Text
                ?? "Bu görseller, teklifin montaj ve proje süreçlerine ait örnek başlıklardır. Referans niteliğiyle eklenmiştir.";
            var delivery = ResolvePropertyPath(entityData, "SalesTypeDefinitionName")?.ToString() ?? "Belirtilecektir";
            var noteLines = BuildFlowNoteLines(entityData);
            var pageScaleX = GetReferencePageScaleX(unit);
            var pageScaleY = GetReferencePageScaleY(unit);

            decimal X(decimal mm) => ToReferenceUnit(mm, pageScaleX);
            decimal Y(decimal mm) => ToReferenceUnit(mm, pageScaleY);
            decimal W(decimal mm) => ToReferenceUnit(mm, pageScaleX);
            decimal H(decimal mm) => ToReferenceUnit(mm, pageScaleY);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-approval-box",
                Type = "shape",
                X = X(12m),
                Y = Y(160m),
                Width = W(72m),
                Height = H(22m),
                Style = new ElementStyle { Background = "#ffffff", Border = "1px solid #c7d0dc", Radius = 6 },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-approval-title",
                Type = "text",
                X = X(16m),
                Y = Y(166m),
                Width = W(36m),
                Height = H(4m),
                Text = "MUSTERI ONAYI",
                FontSize = 7,
                Color = "#7b8494",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-approval-line",
                Type = "shape",
                X = X(18m),
                Y = Y(175m),
                Width = W(58m),
                Height = 1,
                Style = new ElementStyle { Background = "#d2d8e2" },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-approval-sign",
                Type = "text",
                X = X(32m),
                Y = Y(180m),
                Width = W(32m),
                Height = H(4m),
                Text = "Kase ve imza",
                FontSize = 6.5m,
                Color = "#94a3b8",
                FontFamily = "Arial",
                Style = new ElementStyle { TextAlign = "center" },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-summary-box",
                Type = "shape",
                X = X(134m),
                Y = Y(160m),
                Width = W(64m),
                Height = H(32m),
                Style = new ElementStyle { Background = "#ffffff", Border = "1px solid #c7d0dc", Radius = 6 },
            }, entityData, unit, imageCache, onWarning);

            var summaryRows = new (string Label, decimal Value)[]
            {
                ("Brut Toplam", grossAmount),
                ("Iskonto Toplam", discountAmount),
                ("Net Ara Toplam", netAmount),
                ($"KDV (%{vatRate})", vatAmount),
            };

            for (var index = 0; index < summaryRows.Length; index++)
            {
                var row = summaryRows[index];
                var y = Y(167m + (index * 5.5m));
                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-summary-label-{index}",
                    Type = "text",
                    X = X(138m),
                    Y = y,
                    Width = W(28m),
                    Height = H(4m),
                    Text = row.Label,
                    FontSize = 7m,
                    Color = "#7b8494",
                    FontFamily = "Arial",
                }, entityData, unit, imageCache, onWarning);
                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-summary-value-{index}",
                    Type = "text",
                    X = X(170m),
                    Y = y,
                    Width = W(24m),
                    Height = H(4m),
                    Text = FormatCurrencyValue(row.Value, currencyCode),
                    FontSize = 7m,
                    Color = "#0f172a",
                    FontFamily = "Arial",
                    Style = new ElementStyle { TextAlign = "right" },
                }, entityData, unit, imageCache, onWarning);
            }

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-grand-label",
                Type = "text",
                X = X(138m),
                Y = Y(188m),
                Width = W(28m),
                Height = H(5m),
                Text = "Genel Toplam:",
                FontSize = 11,
                Color = "#345A99",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);
            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-grand-value",
                Type = "text",
                X = X(170m),
                Y = Y(188m),
                Width = W(24m),
                Height = H(5m),
                Text = FormatCurrencyValue(grandAmount, currencyCode),
                FontSize = 11,
                Color = "#345A99",
                FontFamily = "Arial",
                Style = new ElementStyle { TextAlign = "right" },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-footer-bg",
                Type = "shape",
                X = 0,
                Y = Y(202m),
                Width = 794,
                Height = H(44m),
                Style = new ElementStyle { Background = "#f8fafc" },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-notes-strip",
                Type = "shape",
                X = X(12m),
                Y = Y(206m),
                Width = 4,
                Height = H(30m),
                Style = new ElementStyle { Background = "#345A99" },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-footer-title",
                Type = "text",
                X = X(16m),
                Y = Y(210m),
                Width = W(98m),
                Height = H(5m),
                Text = footerTitle,
                FontSize = 9,
                Color = "#345A99",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-delivery-badge",
                Type = "shape",
                X = X(16m),
                Y = Y(214m),
                Width = W(88m),
                Height = H(8m),
                Style = new ElementStyle { Background = "#f8fafc", Border = "1px solid #b6bfce", Radius = 4 },
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-delivery-text",
                Type = "text",
                X = X(18m),
                Y = Y(218m),
                Width = W(92m),
                Height = H(4m),
                Text = $"TESLİM ŞEKLİ (DELIVERY TERMS): {delivery}",
                FontSize = 7,
                Color = "#505a6e",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);

            var noteXPositions = new[] { X(16m), X(109m) };
            for (var index = 0; index < noteLines.Count && index < 6; index++)
            {
                var columnIndex = index < 3 ? 0 : 1;
                var rowIndex = index % 3;
                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-note-{index}",
                    Type = "text",
                    X = noteXPositions[columnIndex],
                    Y = Y(227m + (rowIndex * 5.8m)),
                    Width = W(84m),
                    Height = H(4.2m),
                    Text = $"• {noteLines[index]}",
                    FontSize = 6.6m,
                    Color = "#505a6e",
                    FontFamily = "Arial",
                }, entityData, unit, imageCache, onWarning);
            }

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-refs-title",
                Type = "text",
                X = X(16m),
                Y = Y(246m),
                Width = W(90m),
                Height = H(4m),
                Text = refsTitle,
                FontSize = 9,
                Color = "#345A99",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);

            RenderLayeredElement(layers, new ReportElement
            {
                Id = "flow-refs-copy",
                Type = "text",
                X = X(16m),
                Y = Y(251m),
                Width = W(150m),
                Height = H(4m),
                Text = refsCopy,
                FontSize = 6.8m,
                Color = "#7e8695",
                FontFamily = "Arial",
            }, entityData, unit, imageCache, onWarning);

            var imageElements = flowElements
                .Where(element => string.Equals(element.Type, "image", StringComparison.OrdinalIgnoreCase))
                .OrderBy(element => element.X)
                .ToList();

            var imageX = new[] { X(16m), X(74m), X(132m) };
            for (var index = 0; index < Math.Min(3, imageElements.Count); index++)
            {
                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-ref-box-{index}",
                    Type = "shape",
                    X = imageX[index],
                    Y = Y(255m),
                    Width = W(46m),
                    Height = H(24m),
                    Style = new ElementStyle { Background = "#ffffff", Border = "1px solid #c4ccd8", Radius = 6 },
                }, entityData, unit, imageCache, onWarning);

                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-ref-image-{index}",
                    Type = "image",
                    X = imageX[index] + W(1.5m),
                    Y = Y(256.5m),
                    Width = W(43m),
                    Height = H(17m),
                    Value = imageElements[index].Value,
                    Path = imageElements[index].Path,
                    Style = new ElementStyle { ImageFit = "contain" },
                }, entityData, unit, imageCache, onWarning);

                RenderLayeredElement(layers, new ReportElement
                {
                    Id = $"flow-ref-label-{index}",
                    Type = "text",
                    X = imageX[index],
                    Y = Y(276.5m),
                    Width = W(46m),
                    Height = H(4m),
                    Text = $"Referans {index + 1}",
                    FontSize = 6.2m,
                    Color = "#5a6274",
                    FontFamily = "Arial",
                    Style = new ElementStyle { TextAlign = "center" },
                }, entityData, unit, imageCache, onWarning);
            }
        }

        private static decimal ToReferenceUnit(decimal millimeters, decimal scale)
            => millimeters * scale;

        private static decimal GetReferencePageScaleX(string unit)
        {
            if (string.Equals(unit, "mm", StringComparison.OrdinalIgnoreCase))
                return 1m;

            return 794m / 210m;
        }

        private static decimal GetReferencePageScaleY(string unit)
        {
            if (string.Equals(unit, "mm", StringComparison.OrdinalIgnoreCase))
                return 1m;

            return 1123m / 297m;
        }

        private static List<string> BuildFlowNoteLines(object entityData)
        {
            var lines = new List<string>();
            var serial = ResolvePropertyPathStatic(entityData, "DocumentSerialTypeName")?.ToString();
            if (!string.IsNullOrWhiteSpace(serial))
                lines.Add($"Seri No: {serial}");

            var description = ResolvePropertyPathStatic(entityData, "Description")?.ToString();
            if (!string.IsNullOrWhiteSpace(description))
                lines.Add(description.Trim());

            for (var index = 1; index <= 6; index++)
            {
                var value = ResolvePropertyPathStatic(entityData, $"Note{index}")?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    lines.Add(value);
            }

            if (lines.Count == 0)
                lines.Add("Not belirtilmedi.");

            return lines
                .Select(line => StripHtml(line).Trim())
                .Where(line => line.Length > 0)
                .Take(6)
                .ToList();
        }

        private static int ResolveFirstLineVatRate(object entityData)
        {
            var lines = ResolvePropertyPathStatic(entityData, "Lines") as System.Collections.IEnumerable;
            if (lines == null)
                return 20;

            foreach (var line in lines)
            {
                var raw = ResolvePropertyPathStatic(line!, "VatRate");
                if (raw == null)
                    continue;
                if (int.TryParse(raw.ToString(), out var parsed))
                    return parsed;
            }

            return 20;
        }

        private List<List<object>> PaginateTableRows(ReportElement element, List<object> rows)
        {
            if (rows.Count == 0)
                return new List<List<object>>();

            var options = element.TableOptions;
            var firstPageBudget = options?.FirstPageBudget.HasValue == true
                ? (float)options.FirstPageBudget.Value
                : ReportRegionSpec.FirstPageBudget;
            var continuationBudget = options?.ContinuationPageBudget.HasValue == true
                ? (float)options.ContinuationPageBudget.Value
                : ReportRegionSpec.ContinuationBudget;
            var lastPageBudget = options?.LastPageBudget.HasValue == true
                ? (float)options.LastPageBudget.Value
                : ReportRegionSpec.LastPageBudget;

            var heights = rows.Select(row => EstimateTableRowHeight(element, row)).ToList();
            var lastPageStartIndex = rows.Count;
            var lastPageUsed = 0f;

            for (var index = rows.Count - 1; index >= 0; index--)
            {
                var nextHeight = heights[index];
                if (lastPageUsed + nextHeight > lastPageBudget && index < rows.Count - 1)
                    break;

                lastPageUsed += nextHeight;
                lastPageStartIndex = index;
            }

            var chunks = new List<List<object>>();
            var currentChunk = new List<object>();
            var currentBudget = firstPageBudget;
            var currentHeight = 0f;
            var forwardRows = rows.Take(lastPageStartIndex).ToList();

            for (var index = 0; index < forwardRows.Count; index++)
            {
                var row = forwardRows[index];
                var rowHeight = heights[index];
                if (currentChunk.Count > 0 && currentHeight + rowHeight > currentBudget)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new List<object>();
                    currentHeight = 0f;
                    currentBudget = continuationBudget;
                }

                currentChunk.Add(row);
                currentHeight += rowHeight;
            }

            if (currentChunk.Count > 0)
                chunks.Add(currentChunk);

            var lastChunkRows = rows.Skip(lastPageStartIndex).ToList();
            if (lastChunkRows.Count > 0)
                chunks.Add(lastChunkRows);

            return chunks;
        }

        private float EstimateTableRowHeight(ReportElement element, object row)
        {
            var detailPaths = element.TableOptions?.DetailPaths ?? new List<string>();
            var detailColumnPath = element.TableOptions?.DetailColumnPath;
            var lineCount = 0;

            if (!string.IsNullOrWhiteSpace(detailColumnPath))
            {
                var normalizedPath = detailColumnPath.Contains('.') ? detailColumnPath.Split('.', 2)[1] : detailColumnPath;
                var primaryText = ResolvePropertyPath(row, normalizedPath)?.ToString() ?? string.Empty;
                lineCount += EstimateWrappedLineCount(primaryText, ReportRegionSpec.DescriptionMaxCharacters);
            }

            var combinedDetails = BuildCombinedDetailText(row, detailPaths);
            lineCount += EstimateWrappedLineCount(combinedDetails, ReportRegionSpec.DescriptionMaxCharacters);

            var baseHeight = ReportRegionSpec.RowBaseHeight;
            return baseHeight + (Math.Max(0, lineCount - 1) * ReportRegionSpec.RowLineHeight);
        }

        private static int EstimateWrappedLineCount(string? text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var normalized = StripHtml(text).Trim();
            if (normalized.Length == 0)
                return 0;

            var lines = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)Math.Max(1, maxCharacters))))
                .ToList();

            return Math.Max(1, lines.Sum());
        }

        private static string BuildCombinedDetailText(object row, IReadOnlyCollection<string> detailPaths)
        {
            if (detailPaths.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var detailPath in detailPaths)
            {
                if (string.IsNullOrWhiteSpace(detailPath))
                    continue;

                var detailValue = ResolvePropertyPathStatic(row, detailPath)?.ToString();
                if (string.IsNullOrWhiteSpace(detailValue))
                    continue;

                var normalized = StripHtml(detailValue).Trim();
                if (normalized.Length == 0)
                    continue;

                parts.Add(normalized);
            }

            return string.Join(" • ", parts);
        }

        private List<object> GetTableRows(ReportElement element, object entityData)
        {
            if (element.Columns == null || !element.Columns.Any())
                return new List<object>();

            var firstPath = element.Columns[0].Path;
            if (string.IsNullOrEmpty(firstPath))
                return new List<object>();

            var collectionName = firstPath.Contains('.') ? firstPath.Split('.')[0] : firstPath;
            var collection = ResolvePropertyPath(entityData, collectionName) as IEnumerable<object>;
            return collection?.ToList() ?? new List<object>();
        }

        private void RenderNote(IContainer container, ReportElement element, object entityData)
        {
            var title = !string.IsNullOrWhiteSpace(element.Text) ? element.Text : "Note";
            var body = !string.IsNullOrWhiteSpace(element.Value)
                ? element.Value
                : (!string.IsNullOrWhiteSpace(element.Path) ? ResolvePropertyPath(entityData, element.Path)?.ToString() ?? string.Empty : string.Empty);

            container.Column(column =>
            {
                column.Item().Text(title).Bold().FontSize((float)(element.FontSize ?? 12));
                if (!string.IsNullOrWhiteSpace(body))
                    column.Item().PaddingTop(6).Text(body);
            });
        }

        private void RenderSummary(IContainer container, ReportElement element, object entityData)
        {
            var items = element.SummaryItems ?? new List<SummaryItem>();
            if (items.Count == 0)
                return;

            container.Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(element.Text))
                    column.Item().Text(element.Text).Bold().FontSize((float)(element.FontSize ?? 12));

                foreach (var item in items)
                {
                    var rawValue = ResolvePropertyPath(entityData, item.Path);
                    var displayValue = FormatTableCellValue(rawValue, item.Format);
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text(item.Label);
                        row.ConstantItem(140).AlignRight().Text(displayValue).Bold();
                    });
                }
            });
        }

        private void RenderQuotationTotals(IContainer container, ReportElement element, object entityData)
        {
            var options = element.QuotationTotalsOptions ?? new QuotationTotalsOptions();
            var grossAmount = ToDecimal(ResolvePropertyPath(entityData, "Total")) + ToDecimal(ResolvePropertyPath(entityData, "GeneralDiscountAmount"));
            var discountAmount = ToDecimal(ResolvePropertyPath(entityData, "GeneralDiscountAmount"));
            var netAmount = ToDecimal(ResolvePropertyPath(entityData, "Total"));
            var grandAmount = ToDecimal(ResolvePropertyPath(entityData, "GrandTotal"));
            var vatAmount = grandAmount - netAmount;
            var currencyCode = ResolveCurrencyCode(entityData, options);
            var noteValue = !string.IsNullOrWhiteSpace(options.NoteText)
                ? options.NoteText
                : (!string.IsNullOrWhiteSpace(options.NotePath) ? ResolvePropertyPath(entityData, options.NotePath)?.ToString() ?? string.Empty : string.Empty);

            var rows = new List<(string Label, decimal Value, bool Emphasize)>();

            if (options.ShowGross != false)
                rows.Add((string.IsNullOrWhiteSpace(options.GrossLabel) ? "Brut Toplam" : options.GrossLabel!, grossAmount, false));
            if (options.ShowDiscount != false)
                rows.Add((string.IsNullOrWhiteSpace(options.DiscountLabel) ? "Iskonto" : options.DiscountLabel!, discountAmount, false));

            rows.Add((string.IsNullOrWhiteSpace(options.NetLabel) ? "Net Toplam" : options.NetLabel!, netAmount, false));

            if (options.ShowVat != false)
                rows.Add((string.IsNullOrWhiteSpace(options.VatLabel) ? "KDV" : options.VatLabel!, vatAmount, false));

            rows.Add((
                string.IsNullOrWhiteSpace(options.GrandLabel) ? "Genel Toplam" : options.GrandLabel!,
                grandAmount,
                options.EmphasizeGrandTotal != false));

            var spec = QuotationTotalsSpec;

            container.PaddingLeft(spec.OuterPaddingX).PaddingRight(spec.OuterPaddingX).PaddingTop(spec.OuterPaddingTop).Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(element.Text))
                    column.Item().Text(element.Text).SemiBold().FontSize(spec.TitleFontSize).FontColor(spec.TitleColor);

                void RenderRowList(IContainer rowContainer, IReadOnlyList<(string Label, decimal Value, bool Emphasize)> rowItems)
                {
                    rowContainer.Column(listColumn =>
                    {
                        foreach (var row in rowItems)
                        {
                            listColumn.Item().PaddingTop(spec.RowGap).Element(item =>
                            {
                                var target = item
                                    .MinHeight(spec.RowHeight)
                                    .Border(1)
                                    .BorderColor(row.Emphasize ? spec.RowEmphasisFill : spec.RowBorderColor)
                                    .PaddingLeft(spec.RowPaddingX)
                                    .PaddingRight(spec.RowPaddingX)
                                    .PaddingTop(spec.RowPaddingTop)
                                    .PaddingBottom(spec.RowPaddingBottom);

                                if (row.Emphasize)
                                    target = target.Background(spec.RowEmphasisFill);

                                target.Row(inner =>
                                {
                                    var labelText = inner.RelativeItem().Text(row.Label).FontSize(spec.RowLabelFontSize);
                                    var valueText = inner.AutoItem().AlignRight().Text(FormatCurrencyValue(row.Value, currencyCode)).FontSize(spec.RowValueFontSize);

                                    if (row.Emphasize)
                                    {
                                        labelText.FontColor(spec.RowLabelEmphasisColor);
                                        valueText.FontColor(spec.RowValueEmphasisColor).Bold();
                                    }
                                    else
                                    {
                                        labelText.FontColor(spec.RowLabelColor);
                                        valueText.FontColor(spec.RowValueColor).Bold();
                                    }
                                });
                            });
                        }
                    });
                }

                if (string.Equals(options.Layout, "two-column", StringComparison.OrdinalIgnoreCase) && rows.Count > 1)
                {
                    var leftRows = rows.Where((_, index) => index % 2 == 0).ToList();
                    var rightRows = rows.Where((_, index) => index % 2 == 1).ToList();
                    column.Item().PaddingTop(spec.TitleBottomGap).Row(row =>
                    {
                        row.RelativeItem().Element(c => RenderRowList(c, leftRows));
                        row.RelativeItem().PaddingLeft(spec.ColumnGap).Element(c => RenderRowList(c, rightRows));
                    });
                }
                else
                {
                    column.Item().PaddingTop(spec.TitleBottomGap).Element(c => RenderRowList(c, rows));
                }

                var shouldShowNote = options.ShowNote == true &&
                    !(options.HideEmptyNote != false && string.IsNullOrWhiteSpace(noteValue));

                if (shouldShowNote)
                {
                    column.Item().PaddingTop(spec.NoteTopGap).Border(1).BorderColor(spec.RowBorderColor).Background("#f8fafc").PaddingLeft(spec.NotePaddingX).PaddingRight(spec.NotePaddingX).PaddingTop(spec.NotePaddingTop).PaddingBottom(spec.NotePaddingBottom).Column(noteColumn =>
                    {
                        noteColumn.Item().Text(string.IsNullOrWhiteSpace(options.NoteTitle) ? "Not" : options.NoteTitle!).FontSize(spec.NoteTitleFontSize).SemiBold().FontColor(spec.TitleColor);
                        noteColumn.Item().PaddingTop(8).DefaultTextStyle(s => s.FontSize(spec.NoteTextFontSize).FontColor("#475569").LineHeight(spec.NoteTextLineHeight)).Text(noteValue);
                    });
                }
            });
        }

        private sealed class QuotationTotalsLayoutSpec
        {
            public float OuterPaddingX { get; init; } = 14;
            public float OuterPaddingTop { get; init; } = 12;
            public float TitleFontSize { get; init; } = 13;
            public string TitleColor { get; init; } = "#64748b";
            public float TitleBottomGap { get; init; } = 10;
            public float RowHeight { get; init; } = 26;
            public float RowGap { get; init; } = 6;
            public float ColumnGap { get; init; } = 8;
            public float RowPaddingX { get; init; } = 10;
            public float RowPaddingTop { get; init; } = 7;
            public float RowPaddingBottom { get; init; } = 7;
            public float RowLabelFontSize { get; init; } = 11;
            public float RowValueFontSize { get; init; } = 11;
            public string RowLabelColor { get; init; } = "#64748b";
            public string RowLabelEmphasisColor { get; init; } = "#cbd5e1";
            public string RowValueColor { get; init; } = "#0f172a";
            public string RowValueEmphasisColor { get; init; } = "#ffffff";
            public string RowBorderColor { get; init; } = "#e2e8f0";
            public string RowEmphasisFill { get; init; } = "#0f172a";
            public float NoteTopGap { get; init; } = 10;
            public float NotePaddingX { get; init; } = 10;
            public float NotePaddingTop { get; init; } = 10;
            public float NotePaddingBottom { get; init; } = 10;
            public float NoteTitleFontSize { get; init; } = 10;
            public float NoteTextFontSize { get; init; } = 10;
            public float NoteTextLineHeight { get; init; } = 1.35f;
        }

        private sealed class ReportRegionPaginationSpec
        {
            public float FirstPageBudget { get; init; } = 360;
            public float ContinuationBudget { get; init; } = 460;
            public float LastPageBudget { get; init; } = 110;
            public float RowBaseHeight { get; init; } = 18;
            public float RowLineHeight { get; init; } = 6;
            public int DescriptionMaxCharacters { get; init; } = 52;
        }

        private static QuotationTotalsLayoutSpec LoadQuotationTotalsLayoutSpec()
        {
            try
            {
                var current = new DirectoryInfo(AppContext.BaseDirectory);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "pdf-samples", "quotation-totals-layout-spec.json");
                    if (File.Exists(candidate))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                        if (!doc.RootElement.TryGetProperty("quotationTotals", out var node))
                            break;

                        return new QuotationTotalsLayoutSpec
                        {
                            OuterPaddingX = ReadFloat(node, "outerPaddingX", 14),
                            OuterPaddingTop = ReadFloat(node, "outerPaddingTop", 12),
                            TitleFontSize = ReadFloat(node, "titleFontSize", 13),
                            TitleColor = ReadString(node, "titleColor", "#64748b"),
                            TitleBottomGap = ReadFloat(node, "titleBottomGap", 10),
                            RowHeight = ReadFloat(node, "rowHeight", 26),
                            RowGap = ReadFloat(node, "rowGap", 6),
                            ColumnGap = ReadFloat(node, "columnGap", 8),
                            RowPaddingX = ReadFloat(node, "rowPaddingX", 10),
                            RowPaddingTop = ReadFloat(node, "rowPaddingTop", 7),
                            RowPaddingBottom = ReadFloat(node, "rowPaddingBottom", 7),
                            RowLabelFontSize = ReadFloat(node, "rowLabelFontSize", 11),
                            RowValueFontSize = ReadFloat(node, "rowValueFontSize", 11),
                            RowLabelColor = ReadString(node, "rowLabelColor", "#64748b"),
                            RowLabelEmphasisColor = ReadString(node, "rowLabelEmphasisColor", "#cbd5e1"),
                            RowValueColor = ReadString(node, "rowValueColor", "#0f172a"),
                            RowValueEmphasisColor = ReadString(node, "rowValueEmphasisColor", "#ffffff"),
                            RowBorderColor = ReadString(node, "rowBorderColor", "#e2e8f0"),
                            RowEmphasisFill = ReadString(node, "rowEmphasisFill", "#0f172a"),
                            NoteTopGap = ReadFloat(node, "noteTopGap", 10),
                            NotePaddingX = ReadFloat(node, "notePaddingX", 10),
                            NotePaddingTop = ReadFloat(node, "notePaddingTop", 10),
                            NotePaddingBottom = ReadFloat(node, "notePaddingBottom", 10),
                            NoteTitleFontSize = ReadFloat(node, "noteTitleFontSize", 10),
                            NoteTextFontSize = ReadFloat(node, "noteTextFontSize", 10),
                            NoteTextLineHeight = ReadFloat(node, "noteTextLineHeight", 1.35f),
                        };
                    }

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return new QuotationTotalsLayoutSpec();
        }

        private static float ReadFloat(JsonElement element, string name, float fallback)
            => element.TryGetProperty(name, out var property) && property.TryGetSingle(out var value) ? value : fallback;

        private static string ReadString(JsonElement element, string name, string fallback)
            => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? fallback : fallback;

        private static ReportRegionPaginationSpec LoadReportRegionPaginationSpec()
        {
            try
            {
                var current = new DirectoryInfo(AppContext.BaseDirectory);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "pdf-samples", "windo-quotation-layout-spec.json");
                    if (File.Exists(candidate))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                        if (!doc.RootElement.TryGetProperty("pagination", out var node))
                            break;

                        var firstPageBudget = ReadFloat(node, "firstPageBudget", 520);
                        var continuationBudget = ReadFloat(node, "continuationBudget", 900);
                        var rowBaseHeight = ReadFloat(node, "rowBaseHeight", 18);
                        var rowLineHeight = ReadFloat(node, "rowLineHeight", 6);
                        var descriptionMaxCharacters = node.TryGetProperty("descriptionMaxCharacters", out var charsNode) &&
                            charsNode.TryGetInt32(out var maxChars)
                            ? maxChars
                            : 52;

                        return new ReportRegionPaginationSpec
                        {
                            FirstPageBudget = Math.Max(200, firstPageBudget * 0.68f),
                            ContinuationBudget = Math.Max(240, continuationBudget * 0.50f),
                            LastPageBudget = Math.Max(80, continuationBudget * 0.12f),
                            RowBaseHeight = rowBaseHeight,
                            RowLineHeight = rowLineHeight,
                            DescriptionMaxCharacters = descriptionMaxCharacters,
                        };
                    }

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return new ReportRegionPaginationSpec();
        }

        private void ApplyTextStyle(IContainer container, ReportElement element, string content)
        {
            var style = element.Style;
            var fontSize = (float)(style?.FontSize ?? element.FontSize ?? 12);
            var color = style?.Color ?? element.Color;
            var fontFamily = style?.FontFamily ?? element.FontFamily;
            var lineHeight = (float)(style?.LineHeight ?? 1.2m);
            var letterSpacing = (float)(style?.LetterSpacing ?? 0);
            var textAlign = (style?.TextAlign ?? "left").ToLowerInvariant();
            var verticalAlign = (style?.VerticalAlign ?? "top").ToLowerInvariant();
            var block = container.DefaultTextStyle(s =>
            {
                var x = s.FontSize(fontSize);
                if (!string.IsNullOrEmpty(color)) x = x.FontColor(color);
                if (!string.IsNullOrEmpty(fontFamily)) x = x.FontFamily(fontFamily);
                if (lineHeight != 1.2f) x = x.LineHeight(lineHeight);
                if (letterSpacing != 0) x = x.LetterSpacing(letterSpacing);
                return x;
            });

            if (textAlign == "center") block = block.AlignCenter();
            else if (textAlign == "right") block = block.AlignRight();
            else block = block.AlignLeft();

            if (verticalAlign == "middle" || verticalAlign == "center") block = block.AlignMiddle();
            else if (verticalAlign == "bottom") block = block.AlignBottom();
            else block = block.AlignTop();

            block.Text(content);
        }

        private void RenderText(IContainer container, ReportElement element)
        {
            if (string.IsNullOrEmpty(element.Text)) return;
            ApplyTextStyle(container, element, element.Text);
        }

        private void RenderField(IContainer container, ReportElement element, object entityData)
        {
            if (string.IsNullOrEmpty(element.Path)) return;
            var value = ResolvePropertyPath(entityData, element.Path);
            var displayValue = value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(displayValue) &&
                (element.Path.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                 element.Path.Contains("Until", StringComparison.OrdinalIgnoreCase)) &&
                DateTime.TryParse(displayValue, out var parsedDate))
            {
                displayValue = parsedDate.ToString("dd.MM.yyyy");
            }
            if (displayValue.IndexOf('<') >= 0)
                displayValue = StripHtml(displayValue);
            ApplyTextStyle(container, element, displayValue);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var stripped = Regex.Replace(html, @"<[^>]+>", " ");
            return Regex.Replace(stripped, @"\s+", " ").Trim();
        }

        private static decimal ToDecimal(object? value)
        {
            if (value == null)
                return 0m;

            return value switch
            {
                decimal decimalValue => decimalValue,
                int intValue => intValue,
                long longValue => longValue,
                float floatValue => (decimal)floatValue,
                double doubleValue => (decimal)doubleValue,
                _ when decimal.TryParse(value.ToString(), out var parsed) => parsed,
                _ => 0m,
            };
        }

        private static string? ResolveCurrencyCode(object entityData, QuotationTotalsOptions options)
        {
            if (!string.Equals(options.CurrencyMode, "code", StringComparison.OrdinalIgnoreCase))
                return null;

            var path = string.IsNullOrWhiteSpace(options.CurrencyPath) ? "Currency" : options.CurrencyPath!;
            return ResolvePropertyPathStatic(entityData, path)?.ToString();
        }

        private static string FormatCurrencyValue(decimal value, string? currencyCode)
        {
            var formatted = FormatTableCellValue(value, "currency");
            return string.IsNullOrWhiteSpace(currencyCode) ? formatted : $"{formatted} {currencyCode}";
        }

        private async Task<object?> FetchFastQuotationEntityDataAsync(long entityId)
        {
            var data = await (from tq in _unitOfWork.TempQuotattions.Query(false, false)
                              where tq.Id == entityId && !tq.IsDeleted
                              select new
                              {
                                  tq.Id,
                                  OfferNo = !string.IsNullOrWhiteSpace(tq.QuotationNo) ? tq.QuotationNo : ("HT-" + tq.Id),
                                  tq.QuotationNo,
                                  tq.OfferDate,
                                  OfferType = "FastQuotation",
                                  tq.RevisionId,
                                  RevisionNo = tq.RevisionId.HasValue ? ("REV-" + tq.RevisionId.Value) : "",
                                  CustomerName = tq.Customer != null ? tq.Customer.CustomerName : "",
                                  PotentialCustomerName = tq.Customer != null ? tq.Customer.CustomerName : "",
                                  ErpCustomerCode = "",
                                  DeliveryDate = (DateTime?)null,
                                  ShippingAddressText =
                                      tq.Customer != null
                                          ? (tq.Customer.DefaultShippingAddress != null
                                              ? (tq.Customer.DefaultShippingAddress.Address ?? "")
                                              : (tq.Customer.Address ?? ""))
                                          : "",
                                  RepresentativeName = "",
                                  tq.Description,
                                  PaymentTypeName = "",
                                  DocumentSerialTypeName = "",
                                  CurrencyCode = tq.CurrencyCode,
                                  tq.ExchangeRate,
                                  tq.DiscountRate1,
                                  tq.DiscountRate2,
                                  tq.DiscountRate3,
                                  GeneralDiscountAmount = (from tl in _unitOfWork.TempQuotattionLines.Query(false, false)
                                                           where tl.TempQuotattionId == tq.Id && !tl.IsDeleted
                                                           select (decimal?)(tl.DiscountAmount1 + tl.DiscountAmount2 + tl.DiscountAmount3))
                                                          .Sum() ?? 0m,
                                  Total = (from tl in _unitOfWork.TempQuotattionLines.Query(false, false)
                                           where tl.TempQuotattionId == tq.Id && !tl.IsDeleted
                                           select (decimal?)tl.LineTotal)
                                          .Sum() ?? 0m,
                                  GrandTotal = (from tl in _unitOfWork.TempQuotattionLines.Query(false, false)
                                                where tl.TempQuotattionId == tq.Id && !tl.IsDeleted
                                                select (decimal?)tl.LineGrandTotal)
                                               .Sum() ?? 0m,
                                  tq.IsApproved,
                                  tq.ApprovedDate,
                                  tq.CreatedBy,
                                  tq.UpdatedBy,
                                  ExchangeRates = (from er in _unitOfWork.TempQuotattionExchangeLines.Query(false, false)
                                                   where er.TempQuotattionId == tq.Id && !er.IsDeleted
                                                   select new
                                                   {
                                                       er.Currency,
                                                       er.ExchangeRate,
                                                       er.ExchangeRateDate,
                                                       er.IsManual
                                                   }).ToList(),
                                  Lines = (from tl in _unitOfWork.TempQuotattionLines.Query(false, false)
                                           where tl.TempQuotattionId == tq.Id && !tl.IsDeleted
                                           select new
                                           {
                                               ImagePath = tl.ImagePath,
                                               tl.ProductCode,
                                               tl.ProductName,
                                               GroupCode = "",
                                               StockCode = tl.ProductCode,
                                               StockUnit = "",
                                               StockManufacturerCode = "",
                                               StockGroupName = "",
                                               StockCode1 = "",
                                               StockCode1Name = "",
                                               StockCode2 = "",
                                               StockCode2Name = "",
                                               StockCode3 = "",
                                               StockCode3Name = "",
                                               StockCode4 = "",
                                               StockCode4Name = "",
                                               StockCode5 = "",
                                               StockCode5Name = "",
                                               tl.Quantity,
                                               tl.UnitPrice,
                                               tl.DiscountRate1,
                                               tl.DiscountAmount1,
                                               tl.DiscountRate2,
                                               tl.DiscountAmount2,
                                               tl.DiscountRate3,
                                               tl.DiscountAmount3,
                                               tl.VatRate,
                                               tl.VatAmount,
                                               tl.LineTotal,
                                               tl.LineGrandTotal,
                                               tl.Description,
                                               HtmlDescription = "",
                                               DefaultImagePath = ""
                                           }).ToList()
                              }).FirstOrDefaultAsync().ConfigureAwait(false);

            if (data == null)
                return null;

            var currencyName = await ResolveErpCurrencyNameAsync(data.CurrencyCode).ConfigureAwait(false);

            return new
            {
                data.Id,
                data.OfferNo,
                data.QuotationNo,
                data.OfferDate,
                data.OfferType,
                data.RevisionId,
                data.RevisionNo,
                data.CustomerName,
                data.PotentialCustomerName,
                data.ErpCustomerCode,
                data.DeliveryDate,
                data.ShippingAddressText,
                data.RepresentativeName,
                data.Description,
                data.PaymentTypeName,
                data.DocumentSerialTypeName,
                Currency = currencyName,
                data.ExchangeRate,
                data.DiscountRate1,
                data.DiscountRate2,
                data.DiscountRate3,
                data.GeneralDiscountAmount,
                data.Total,
                data.GrandTotal,
                data.IsApproved,
                data.ApprovedDate,
                data.CreatedBy,
                data.UpdatedBy,
                data.ExchangeRates,
                data.Lines
            };
        }

        private async Task<string> ResolveErpCurrencyNameAsync(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (_erpService == null)
                return normalized;

            try
            {
                var rates = await _erpService.GetExchangeRateAsync(DateTime.Now, 1).ConfigureAwait(false);
                if (!rates.Success || rates.Data == null)
                    return normalized;

                foreach (var item in rates.Data)
                {
                    if (string.Equals(item.DovizTipi.ToString(), normalized, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(item.DovizIsmi))
                    {
                        return item.DovizIsmi.Trim().ToUpperInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve ERP currency name for fast quotation currency code {CurrencyCode}", normalized);
            }

            return normalized;
        }

        private async Task<object?> FetchActivityEntityDataAsync(long entityId)
        {
            return await (from a in _unitOfWork.Activities.Query(false, false)
                          where a.Id == entityId && !a.IsDeleted
                          select new
                          {
                              a.Id,
                              a.Subject,
                              a.Description,
                              ActivityTypeName = _unitOfWork.ActivityTypes.Query(false, false)
                                  .Where(x => x.Id == a.ActivityTypeId && !x.IsDeleted)
                                  .Select(x => x.Name)
                                  .FirstOrDefault() ?? "",
                              PaymentTypeName = a.PaymentTypeId.HasValue
                                  ? (_unitOfWork.Repository<PaymentType>().Query(false, false)
                                      .Where(x => x.Id == a.PaymentTypeId.Value && !x.IsDeleted)
                                      .Select(x => x.Name)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              ActivityMeetingTypeName = a.ActivityMeetingTypeId.HasValue
                                  ? (_unitOfWork.Repository<ActivityMeetingType>().Query(false, false)
                                      .Where(x => x.Id == a.ActivityMeetingTypeId.Value && !x.IsDeleted)
                                      .Select(x => x.Name)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              ActivityTopicPurposeName = a.ActivityTopicPurposeId.HasValue
                                  ? (_unitOfWork.Repository<ActivityTopicPurpose>().Query(false, false)
                                      .Where(x => x.Id == a.ActivityTopicPurposeId.Value && !x.IsDeleted)
                                      .Select(x => x.Name)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              ActivityShippingName = a.ActivityShippingId.HasValue
                                  ? (_unitOfWork.Repository<ActivityShipping>().Query(false, false)
                                      .Where(x => x.Id == a.ActivityShippingId.Value && !x.IsDeleted)
                                      .Select(x => x.Name)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              a.StartDateTime,
                              a.EndDateTime,
                              a.IsAllDay,
                              Status = a.Status.ToString(),
                              Priority = a.Priority.ToString(),
                              AssignedUserName = _unitOfWork.Users.Query(false, false)
                                  .Where(x => x.Id == a.AssignedUserId && !x.IsDeleted)
                                  .Select(x => x.FullName)
                                  .FirstOrDefault() ?? "",
                              AssignedUserEmail = _unitOfWork.Users.Query(false, false)
                                  .Where(x => x.Id == a.AssignedUserId && !x.IsDeleted)
                                  .Select(x => x.Email)
                                  .FirstOrDefault() ?? "",
                              ContactName = a.ContactId.HasValue
                                  ? (_unitOfWork.Repository<Contact>().Query(false, false)
                                      .Where(x => x.Id == a.ContactId.Value && !x.IsDeleted)
                                      .Select(x => x.FullName)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              ContactEmail = a.ContactId.HasValue
                                  ? ((_unitOfWork.Repository<Contact>().Query(false, false)
                                          .Where(x => x.Id == a.ContactId.Value && !x.IsDeleted)
                                          .Select(x => x.Email)
                                          .FirstOrDefault())
                                      ?? (_unitOfWork.Customers.Query(false, false)
                                          .Where(x => a.PotentialCustomerId.HasValue && x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                          .Select(x => x.Email)
                                          .FirstOrDefault() ?? ""))
                                  : (_unitOfWork.Customers.Query(false, false)
                                      .Where(x => a.PotentialCustomerId.HasValue && x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                      .Select(x => x.Email)
                                      .FirstOrDefault() ?? ""),
                              ContactPhone = a.ContactId.HasValue
                                  ? ((_unitOfWork.Repository<Contact>().Query(false, false)
                                          .Where(x => x.Id == a.ContactId.Value && !x.IsDeleted)
                                          .Select(x => !string.IsNullOrWhiteSpace(x.Mobile) ? x.Mobile : x.Phone)
                                          .FirstOrDefault())
                                      ?? (_unitOfWork.Customers.Query(false, false)
                                          .Where(x => a.PotentialCustomerId.HasValue && x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                          .Select(x => !string.IsNullOrWhiteSpace(x.Phone1) ? x.Phone1 : x.Phone2)
                                          .FirstOrDefault() ?? ""))
                                  : (_unitOfWork.Customers.Query(false, false)
                                      .Where(x => a.PotentialCustomerId.HasValue && x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                      .Select(x => !string.IsNullOrWhiteSpace(x.Phone1) ? x.Phone1 : x.Phone2)
                                      .FirstOrDefault() ?? ""),
                              CustomerName = a.PotentialCustomerId.HasValue
                                  ? (_unitOfWork.Customers.Query(false, false)
                                      .Where(x => x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                      .Select(x => x.CustomerName)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              CustomerAddress = a.PotentialCustomerId.HasValue
                                  ? ((_unitOfWork.Customers.Query(false, false)
                                          .Where(x => x.Id == a.PotentialCustomerId.Value && !x.IsDeleted)
                                          .Select(x => x.DefaultShippingAddress != null ? x.DefaultShippingAddress.Address : x.Address)
                                          .FirstOrDefault())
                                      ?? "")
                                  : "",
                              a.ErpCustomerCode,
                              CreatedBy = a.CreatedBy.HasValue
                                  ? _unitOfWork.Users.Query(false, false)
                                      .Where(u => u.Id == a.CreatedBy.Value && !u.IsDeleted)
                                      .Select(u => u.FullName)
                                      .FirstOrDefault() ?? ""
                                  : "",
                              UpdatedBy = a.UpdatedBy.HasValue
                                  ? _unitOfWork.Users.Query(false, false)
                                      .Where(u => u.Id == a.UpdatedBy.Value && !u.IsDeleted)
                                      .Select(u => u.FullName)
                                      .FirstOrDefault() ?? ""
                                  : "",
                              a.CreatedDate,
                              a.UpdatedDate,
                              CustomerLatestImageUrl = a.PotentialCustomerId.HasValue
                                  ? (_unitOfWork.CustomerImages.Query(false, false)
                                      .Where(img => img.CustomerId == a.PotentialCustomerId.Value && !img.IsDeleted)
                                      .OrderByDescending(img => img.CreatedDate)
                                      .ThenByDescending(img => img.Id)
                                      .Select(img => img.ImageUrl)
                                      .FirstOrDefault() ?? "")
                                  : "",
                              PrimaryImageUrl = a.PotentialCustomerId.HasValue
                                  ? ((_unitOfWork.CustomerImages.Query(false, false)
                                          .Where(img => img.CustomerId == a.PotentialCustomerId.Value && !img.IsDeleted)
                                          .OrderByDescending(img => img.CreatedDate)
                                          .ThenByDescending(img => img.Id)
                                          .Select(img => img.ImageUrl)
                                          .FirstOrDefault())
                                      ?? (_unitOfWork.ActivityImages.Query(false, false)
                                          .Where(img => img.ActivityId == a.Id && !img.IsDeleted)
                                          .OrderByDescending(img => img.CreatedDate)
                                          .ThenByDescending(img => img.Id)
                                          .Select(img => img.ResimUrl)
                                          .FirstOrDefault() ?? ""))
                                  : (_unitOfWork.ActivityImages.Query(false, false)
                                      .Where(img => img.ActivityId == a.Id && !img.IsDeleted)
                                      .OrderByDescending(img => img.CreatedDate)
                                      .ThenByDescending(img => img.Id)
                                      .Select(img => img.ResimUrl)
                                      .FirstOrDefault() ?? ""),
                              Images = _unitOfWork.ActivityImages.Query(false, false)
                                  .Where(img => img.ActivityId == a.Id && !img.IsDeleted)
                                  .OrderBy(img => img.Id)
                                  .Select(img => new
                                  {
                                      img.ResimAciklama,
                                      img.ResimUrl
                                  }).ToList(),
                              Reminders = _unitOfWork.Repository<ActivityReminder>().Query(false, false)
                                  .Where(reminder => reminder.ActivityId == a.Id && !reminder.IsDeleted)
                                  .OrderBy(reminder => reminder.OffsetMinutes)
                                  .Select(reminder => new
                                  {
                                      reminder.OffsetMinutes,
                                      Channel = reminder.Channel.ToString(),
                                      Status = reminder.Status.ToString(),
                                      reminder.SentAt
                                  }).ToList()
                          }).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        private static object? ResolvePropertyPathStatic(object obj, string path)
        {
            if (obj == null || string.IsNullOrWhiteSpace(path))
                return null;

            object? current = obj;
            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current == null)
                    return null;

                var type = current.GetType();
                var property = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                    return null;
                current = property.GetValue(current);
            }

            return current;
        }

        private static List<ReportElement> ResolveLayoutElements(List<ReportElement> elements)
        {
            var elementMap = elements.ToDictionary(element => element.Id, StringComparer.OrdinalIgnoreCase);
            var resolved = new Dictionary<string, ReportElement>(StringComparer.OrdinalIgnoreCase);
            var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ReportElement Resolve(ReportElement element)
            {
                if (resolved.TryGetValue(element.Id, out var cached))
                    return cached;

                if (!string.IsNullOrWhiteSpace(element.ParentId) &&
                    elementMap.TryGetValue(element.ParentId, out var parent))
                {
                    if (resolving.Contains(element.Id))
                        return element;

                    resolving.Add(element.Id);
                    var resolvedParent = Resolve(parent);
                    resolving.Remove(element.Id);
                    var parentPadding = resolvedParent.Style?.Padding ?? 0;

                    cached = CloneElement(element);
                    cached.X += resolvedParent.X + parentPadding;
                    cached.Y += resolvedParent.Y + parentPadding;
                    cached.Section = string.IsNullOrWhiteSpace(cached.Section) ? resolvedParent.Section : cached.Section;
                    cached.PageNumbers ??= resolvedParent.PageNumbers != null ? new List<int>(resolvedParent.PageNumbers) : null;
                    resolved[element.Id] = cached;
                    return cached;
                }

                cached = CloneElement(element);
                resolved[element.Id] = cached;
                return cached;
            }

            return elements.Select(Resolve).ToList();
        }

        private static ReportElement CloneElement(ReportElement source)
        {
            return new ReportElement
            {
                Id = source.Id,
                Type = source.Type,
                Section = source.Section,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                ZIndex = source.ZIndex,
                Rotation = source.Rotation,
                Style = source.Style,
                PageNumbers = source.PageNumbers != null ? new List<int>(source.PageNumbers) : null,
                ParentId = source.ParentId,
                Binding = source.Binding,
                Text = source.Text,
                Value = source.Value,
                Path = source.Path,
                FontSize = source.FontSize,
                FontFamily = source.FontFamily,
                Color = source.Color,
                TextOverflow = source.TextOverflow,
                Columns = source.Columns,
                HeaderStyle = source.HeaderStyle,
                RowStyle = source.RowStyle,
                AlternateRowStyle = source.AlternateRowStyle,
                ColumnWidths = source.ColumnWidths,
                TableOptions = source.TableOptions,
                SummaryItems = source.SummaryItems,
            };
        }

        private void RenderImage(IContainer container, ReportElement element, object entityData,
            Dictionary<string, byte[]> imageCache, Action onWarning)
        {
            var source = ResolveImageSource(element, entityData);
            var key = source?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                onWarning();
                return;
            }
            if (!imageCache.TryGetValue(key, out var imageBytes) || imageBytes == null || imageBytes.Length == 0)
            {
                onWarning();
                return;
            }
            var imageFit = element.Style?.ImageFit?.Trim().ToLowerInvariant();
            if (imageFit == "cover")
            {
                container.Image(imageBytes).FitUnproportionally();
                return;
            }

            container.Image(imageBytes).FitArea();
        }

        private static (float borderWidth, string? borderColor) ParseBorderSpec(string border, string unit)
        {
            if (string.IsNullOrWhiteSpace(border))
                return (0, null);

            var trimmed = border.Trim();
            var simpleColor = trimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("rgb", StringComparison.OrdinalIgnoreCase);
            if (simpleColor)
                return (1, trimmed);

            var match = Regex.Match(trimmed, @"(?<width>\d+(\.\d+)?)px\s+\w+\s+(?<color>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return (1, trimmed);

            var width = decimal.TryParse(match.Groups["width"].Value, out var parsedWidth)
                ? PdfUnitConversion.ToPointsFloat(parsedWidth, unit)
                : 1;
            var color = match.Groups["color"].Value.Trim();

            return (Math.Max(0, width), string.IsNullOrWhiteSpace(color) ? null : color);
        }

        private int GetTableRowCount(ReportElement element, object entityData)
            => GetTableRows(element, entityData).Count;

        private void RenderTable(IContainer container, ReportElement element, object entityData, string unit)
        {
            var rows = GetTableRows(element, entityData);
            RenderTable(container, element, entityData, unit, rows);
        }

        private void RenderTable(IContainer container, ReportElement element, object entityData, string unit, List<object> rows)
        {
            if (element.Columns == null || !element.Columns.Any() || rows.Count == 0)
                return;

            var headerStyle = element.HeaderStyle;
            var rowStyle = element.RowStyle;
            var altStyle = element.AlternateRowStyle;
            var repeatHeader = element.TableOptions?.RepeatHeader ?? true;
            var showBorders = element.TableOptions?.ShowBorders ?? true;
            var dense = element.TableOptions?.Dense ?? false;
            var cellPadding = dense ? 3 : 5;
            var detailColumnPath = element.TableOptions?.DetailColumnPath;
            var detailPaths = element.TableOptions?.DetailPaths ?? new List<string>();
            var detailLineFontSize = (float)(element.TableOptions?.DetailLineFontSize ?? Math.Max(8, (rowStyle?.FontSize ?? 9) - 1));
            var detailLineColor = element.TableOptions?.DetailLineColor ?? "#64748b";
            var groupByPath = element.TableOptions?.GroupByPath;
            var groupHeaderLabel = element.TableOptions?.GroupHeaderLabel ?? "Group";
            var showGroupFooter = element.TableOptions?.ShowGroupFooter;
            var groupFooterLabel = element.TableOptions?.GroupFooterLabel ?? "Toplam";
            var groupFooterValuePath = element.TableOptions?.GroupFooterValuePath;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (element.ColumnWidths != null && element.ColumnWidths.Count == element.Columns.Count)
                    {
                        foreach (var w in element.ColumnWidths)
                            columns.ConstantColumn(PdfUnitConversion.ToPointsFloat(w, unit));
                    }
                    else
                    {
                        foreach (var col in element.Columns)
                        {
                            if (col.Width.HasValue)
                                columns.ConstantColumn(PdfUnitConversion.ToPointsFloat(col.Width.Value, unit));
                            else
                                columns.RelativeColumn();
                        }
                    }
                });

                if (repeatHeader)
                {
                    table.Header(header =>
                    {
                        foreach (var col in element.Columns)
                        {
                            IContainer headerCell = header.Cell().Background(Colors.Grey.Lighten2);
                            if (showBorders)
                                headerCell = headerCell.Border(1);
                            IContainer headerContent = headerCell.Padding(cellPadding);
                            headerContent = (col.Align ?? "left").ToLowerInvariant() switch
                            {
                                "center" => headerContent.AlignCenter(),
                                "right" => headerContent.AlignRight(),
                                _ => headerContent.AlignLeft()
                            };

                            var cell = headerContent.Text(col.Label).Bold();
                            if (headerStyle?.FontSize.HasValue == true)
                                cell = cell.FontSize((float)headerStyle.FontSize.Value);
                            if (!string.IsNullOrEmpty(headerStyle?.Color))
                                cell = cell.FontColor(headerStyle.Color);
                        }
                    });
                }
                else
                {
                    foreach (var col in element.Columns)
                    {
                        IContainer headerCell = table.Cell().Background(Colors.Grey.Lighten2);
                        if (showBorders)
                            headerCell = headerCell.Border(1);
                        IContainer headerContent = headerCell.Padding(cellPadding);
                        headerContent = (col.Align ?? "left").ToLowerInvariant() switch
                        {
                            "center" => headerContent.AlignCenter(),
                            "right" => headerContent.AlignRight(),
                            _ => headerContent.AlignLeft()
                        };

                        var cell = headerContent.Text(col.Label).Bold();
                        if (headerStyle?.FontSize.HasValue == true)
                            cell = cell.FontSize((float)headerStyle.FontSize.Value);
                        if (!string.IsNullOrEmpty(headerStyle?.Color))
                            cell = cell.FontColor(headerStyle.Color);
                    }
                }

                var rowIndex = 0;
                var groupedRows = BuildTableGroups(rows, groupByPath);
                foreach (var group in groupedRows)
                {
                    if (!string.IsNullOrWhiteSpace(group.Key))
                    {
                        IContainer groupCell = table.Cell().ColumnSpan((uint)element.Columns.Count);
                        if (showBorders)
                            groupCell = groupCell.Border(1);
                        groupCell.Background("#eff6ff").Padding(cellPadding)
                            .Text($"{groupHeaderLabel}: {group.Key}")
                            .SemiBold();
                    }

                    foreach (var row in group.Rows)
                    {
                        foreach (var col in element.Columns)
                        {
                            if (string.IsNullOrEmpty(col.Path))
                            {
                                IContainer emptyCell = table.Cell();
                                if (showBorders)
                                    emptyCell = emptyCell.Border(1);
                                emptyCell.Padding(cellPadding).Text(string.Empty);
                                continue;
                            }
                            var propertyPath = col.Path.Contains('.') ? col.Path.Split('.', 2)[1] : col.Path;
                            var rawValue = ResolvePropertyPath(row, propertyPath);
                            var cellValue = FormatTableCellValue(rawValue, col.Format);
                            if (cellValue.IndexOf('<') >= 0) cellValue = StripHtml(cellValue);
                            var isDetailColumn = !string.IsNullOrWhiteSpace(detailColumnPath) &&
                                string.Equals(col.Path, detailColumnPath, StringComparison.OrdinalIgnoreCase);
                            var isLineImageColumn = string.Equals(col.Path, "Lines.ImagePath", StringComparison.OrdinalIgnoreCase);
                            var thumbImagePath = ResolvePropertyPath(row, "ImagePath")?.ToString();

                            var style = (rowIndex % 2 == 1 && altStyle != null) ? altStyle : rowStyle;
                            IContainer tableCell = table.Cell();
                            if (!string.IsNullOrEmpty(style?.BackgroundColor))
                                tableCell = tableCell.Background(style.BackgroundColor);
                            if (showBorders)
                                tableCell = tableCell.Border(1);

                            IContainer contentContainer = tableCell.Padding(cellPadding);
                            contentContainer = (col.Align ?? "left").ToLowerInvariant() switch
                            {
                                "center" => contentContainer.AlignCenter(),
                                "right" => contentContainer.AlignRight(),
                                _ => contentContainer.AlignLeft()
                            };

                            if (isLineImageColumn)
                            {
                                RenderTableImageCell(contentContainer, thumbImagePath);
                            }
                            else if (isDetailColumn && detailPaths.Count > 0)
                            {
                                var combinedDetailText = BuildCombinedDetailText(row, detailPaths);
                                contentContainer.Column(detailColumn =>
                                {
                                    RenderTablePrimaryCell(detailColumn.Item(), cellValue, style);

                                    if (!string.IsNullOrWhiteSpace(combinedDetailText))
                                    {
                                        detailColumn.Item()
                                            .PaddingTop(1)
                                            .DefaultTextStyle(textStyle => textStyle.FontSize(detailLineFontSize).FontColor(detailLineColor).LineHeight(1.2f))
                                            .Text(combinedDetailText);
                                    }
                                });
                            }
                            else
                            {
                                RenderTablePrimaryCell(contentContainer, cellValue, style);
                            }
                        }
                        rowIndex++;
                    }

                    if (showGroupFooter == true && !string.IsNullOrWhiteSpace(groupFooterValuePath))
                    {
                        IContainer footerLabelCell = table.Cell().ColumnSpan((uint)Math.Max(1, element.Columns.Count - 1));
                        if (showBorders)
                            footerLabelCell = footerLabelCell.Border(1);
                        footerLabelCell.Background("#f8fafc").Padding(cellPadding).AlignRight().Text(groupFooterLabel).SemiBold();

                        IContainer footerValueCell = table.Cell();
                        if (showBorders)
                            footerValueCell = footerValueCell.Border(1);
                        footerValueCell.Background("#f8fafc").Padding(cellPadding).AlignRight().Text(
                            FormatTableCellValue(SumGroupValues(group.Rows, groupFooterValuePath!), "currency")).SemiBold();
                    }
                }
            });
        }

        private static List<TableGroup> BuildTableGroups(List<object> rows, string? groupByPath)
        {
            if (string.IsNullOrWhiteSpace(groupByPath))
                return new List<TableGroup> { new() { Key = null, Rows = rows } };

            var normalizedPath = groupByPath.Contains('.') ? groupByPath.Split('.', 2)[1] : groupByPath;
            return rows
                .GroupBy(row => ResolvePropertyPathStatic(row, normalizedPath)?.ToString() ?? string.Empty)
                .Select(group => new TableGroup
                {
                    Key = string.IsNullOrWhiteSpace(group.Key) ? null : group.Key,
                    Rows = group.Cast<object>().ToList(),
                })
                .ToList();
        }

        private void RenderTablePrimaryCell(
            IContainer container,
            string cellValue,
            TableStyle? style)
        {
            var textBlock = container.Text(cellValue);
            if (style?.FontSize.HasValue == true) textBlock = textBlock.FontSize((float)style.FontSize.Value);
            if (!string.IsNullOrEmpty(style?.Color)) textBlock = textBlock.FontColor(style.Color);
        }

        private void RenderTableImageCell(IContainer container, string? imagePath)
        {
            var imageBytes = TryLoadInlineTableImage(imagePath);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                container.Text(string.Empty);
                return;
            }

            container.AlignCenter().Height(28).Image(imageBytes).FitArea();
        }

        private byte[]? TryLoadInlineTableImage(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            try
            {
                var key = source.Trim();
                if (key.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = key.IndexOf(',');
                    if (comma > 0)
                        return Convert.FromBase64String(key[(comma + 1)..]);
                }

                if (key.StartsWith("/"))
                {
                    if (string.IsNullOrEmpty(_options.LocalImageBasePath))
                        return null;

                    var sanitized = key.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var baseFull = Path.GetFullPath(_options.LocalImageBasePath);
                    var fullPath = Path.GetFullPath(Path.Combine(_options.LocalImageBasePath, sanitized));

                    if (!fullPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                        return null;

                    return File.ReadAllBytes(fullPath);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static decimal SumGroupValues(IEnumerable<object> rows, string groupFooterValuePath)
        {
            var normalizedPath = groupFooterValuePath.Contains('.') ? groupFooterValuePath.Split('.', 2)[1] : groupFooterValuePath;
            return rows.Sum(row => ToDecimal(ResolvePropertyPathStatic(row, normalizedPath)));
        }

        private sealed class TableGroup
        {
            public string? Key { get; init; }
            public List<object> Rows { get; init; } = new();
        }

        private static string FormatTableCellValue(object? rawValue, string? format)
        {
            if (rawValue == null)
                return string.Empty;

            var normalizedFormat = format?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedFormat) || normalizedFormat == "text" || normalizedFormat == "image")
                return rawValue.ToString() ?? string.Empty;

            if (normalizedFormat == "date")
            {
                if (rawValue is DateTime dateTime)
                    return dateTime.ToString("dd.MM.yyyy");
                if (DateTime.TryParse(rawValue.ToString(), out var parsedDate))
                    return parsedDate.ToString("dd.MM.yyyy");
            }

            if (normalizedFormat == "number" || normalizedFormat == "currency")
            {
                if (decimal.TryParse(rawValue.ToString(), out var parsedDecimal))
                    return normalizedFormat == "currency"
                        ? parsedDecimal.ToString("N2")
                        : parsedDecimal.ToString("N0");
            }

            return rawValue.ToString() ?? string.Empty;
        }

        private object? ResolvePropertyPath(object obj, string path)
        {
            if (obj == null || string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('.');
            var current = obj;
            foreach (var part in parts)
            {
                if (current == null) return null;
                if (current is System.Collections.IEnumerable enumerable && current is not string)
                {
                    current = enumerable.Cast<object?>().FirstOrDefault();
                    if (current == null) return null;
                }
                var type = current.GetType();
                var property = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null) return null;
                current = property.GetValue(current);
            }
            return current;
        }

        private async Task<object?> FetchEntityDataAsync(DocumentRuleType ruleType, long entityId)
        {
            return ruleType switch
            {
                DocumentRuleType.Demand => await (from d in _unitOfWork.Demands.Query(false, false)
                    where d.Id == entityId && !d.IsDeleted
                    select new
                    {
                        d.Id,
                        d.OfferNo,
                        d.OfferDate,
                        d.OfferType,
                        d.RevisionNo,
                        CustomerName = d.PotentialCustomer != null ? d.PotentialCustomer.CustomerName : (d.ErpCustomerCode ?? ""),
                        PotentialCustomerName = d.PotentialCustomer != null ? d.PotentialCustomer.CustomerName : "",
                        d.ErpCustomerCode,
                        d.DeliveryDate,
                        ShippingAddressText = d.ShippingAddress != null ? d.ShippingAddress.Address : "",
                        RepresentativeName = d.Representative != null ? d.Representative.FullName : "",
                        d.Description,
                        PaymentTypeName = d.PaymentType != null ? d.PaymentType.Name : "",
                        DocumentSerialTypeName = d.DocumentSerialType != null ? d.DocumentSerialType.SerialPrefix : "",
                        d.Currency,
                        d.CreatedBy,
                        d.UpdatedBy,
                        ExchangeRates = (from er in _unitOfWork.DemandExchangeRates.Query(false, false)
                                where er.DemandId == d.Id && !er.IsDeleted
                                select new { er.Currency, er.ExchangeRate, er.ExchangeRateDate, er.IsOfficial }).ToList(),
                        Lines = (from dl in _unitOfWork.DemandLines.Query(false, false)
                                where dl.DemandId == d.Id && !dl.IsDeleted
                                let stockData = _unitOfWork.Stocks.Query(false, false)
                                    .Where(s => !s.IsDeleted &&
                                        ((dl.RelatedStockId.HasValue && s.Id == dl.RelatedStockId.Value) ||
                                         (!dl.RelatedStockId.HasValue && s.ErpStockCode == dl.ProductCode)))
                                    .Select(s => new
                                    {
                                        Id = (long?)s.Id,
                                        s.ErpStockCode,
                                        s.StockName,
                                        s.Unit,
                                        s.UreticiKodu,
                                        s.GrupKodu,
                                        s.GrupAdi,
                                        s.Kod1,
                                        s.Kod1Adi,
                                        s.Kod2,
                                        s.Kod2Adi,
                                        s.Kod3,
                                        s.Kod3Adi,
                                        s.Kod4,
                                        s.Kod4Adi,
                                        s.Kod5,
                                        s.Kod5Adi
                                    })
                                    .FirstOrDefault()
                                select new
                                {
                                    dl.ProductCode,
                                    ProductName = stockData != null ? stockData.StockName : dl.ProductCode,
                                    GroupCode = stockData != null ? (stockData.GrupKodu ?? "") : "",
                                    StockCode = stockData != null ? (stockData.ErpStockCode ?? "") : "",
                                    StockUnit = stockData != null ? (stockData.Unit ?? "") : "",
                                    StockManufacturerCode = stockData != null ? (stockData.UreticiKodu ?? "") : "",
                                    StockGroupName = stockData != null ? (stockData.GrupAdi ?? "") : "",
                                    StockCode1 = stockData != null ? (stockData.Kod1 ?? "") : "",
                                    StockCode1Name = stockData != null ? (stockData.Kod1Adi ?? "") : "",
                                    StockCode2 = stockData != null ? (stockData.Kod2 ?? "") : "",
                                    StockCode2Name = stockData != null ? (stockData.Kod2Adi ?? "") : "",
                                    StockCode3 = stockData != null ? (stockData.Kod3 ?? "") : "",
                                    StockCode3Name = stockData != null ? (stockData.Kod3Adi ?? "") : "",
                                    StockCode4 = stockData != null ? (stockData.Kod4 ?? "") : "",
                                    StockCode4Name = stockData != null ? (stockData.Kod4Adi ?? "") : "",
                                    StockCode5 = stockData != null ? (stockData.Kod5 ?? "") : "",
                                    StockCode5Name = stockData != null ? (stockData.Kod5Adi ?? "") : "",
                                    dl.Quantity,
                                    dl.UnitPrice,
                                    dl.DiscountRate1,
                                    dl.DiscountAmount1,
                                    dl.DiscountRate2,
                                    dl.DiscountAmount2,
                                    dl.DiscountRate3,
                                    dl.DiscountAmount3,
                                    dl.VatRate,
                                    dl.VatAmount,
                                    dl.LineTotal,
                                    dl.LineGrandTotal,
                                    dl.Description,
                                    HtmlDescription = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.StockDetails.Query(false, false).Where(sd => sd.StockId == stockData.Id.Value && !sd.IsDeleted).Select(sd => sd.HtmlDescription).FirstOrDefault() ?? "")
                                        : "",
                                    DefaultImagePath = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.Repository<StockImage>().Query(false, false).Where(si => si.StockId == stockData.Id.Value && !si.IsDeleted).OrderByDescending(si => si.IsPrimary).ThenBy(si => si.SortOrder).Select(si => si.FilePath).FirstOrDefault() ?? "")
                                        : ""
                                }).ToList()
                    }).FirstOrDefaultAsync().ConfigureAwait(false),

                DocumentRuleType.Quotation => await (from q in _unitOfWork.Quotations.Query(false, false)
                    where q.Id == entityId && !q.IsDeleted
                    select new
                    {
                        q.Id,
                        q.OfferNo,
                        q.OfferDate,
                        q.OfferType,
                        q.RevisionNo,
                        q.ValidUntil,
                        CustomerName = q.PotentialCustomer != null ? q.PotentialCustomer.CustomerName : (q.ErpCustomerCode ?? ""),
                        PotentialCustomerName = q.PotentialCustomer != null ? q.PotentialCustomer.CustomerName : "",
                        q.ErpCustomerCode,
                        q.DeliveryDate,
                        ShippingAddressText = q.ShippingAddress != null ? q.ShippingAddress.Address : "",
                        RepresentativeName = q.Representative != null ? q.Representative.FullName : "",
                        SalesTypeDefinitionName = q.SalesTypeDefinition != null ? q.SalesTypeDefinition.Name : "",
                        q.Description,
                        PaymentTypeName = q.PaymentType != null ? q.PaymentType.Name : "",
                        DocumentSerialTypeName = q.DocumentSerialType != null ? q.DocumentSerialType.SerialPrefix : "",
                        q.Currency,
                        q.GeneralDiscountRate,
                        q.GeneralDiscountAmount,
                        q.Total,
                        q.GrandTotal,
                        q.ErpProjectCode,
                        Note1 = q.QuotationNotes != null ? q.QuotationNotes.Note1 : null,
                        Note2 = q.QuotationNotes != null ? q.QuotationNotes.Note2 : null,
                        Note3 = q.QuotationNotes != null ? q.QuotationNotes.Note3 : null,
                        Note4 = q.QuotationNotes != null ? q.QuotationNotes.Note4 : null,
                        Note5 = q.QuotationNotes != null ? q.QuotationNotes.Note5 : null,
                        Note6 = q.QuotationNotes != null ? q.QuotationNotes.Note6 : null,
                        Note7 = q.QuotationNotes != null ? q.QuotationNotes.Note7 : null,
                        Note8 = q.QuotationNotes != null ? q.QuotationNotes.Note8 : null,
                        Note9 = q.QuotationNotes != null ? q.QuotationNotes.Note9 : null,
                        Note10 = q.QuotationNotes != null ? q.QuotationNotes.Note10 : null,
                        Note11 = q.QuotationNotes != null ? q.QuotationNotes.Note11 : null,
                        Note12 = q.QuotationNotes != null ? q.QuotationNotes.Note12 : null,
                        Note13 = q.QuotationNotes != null ? q.QuotationNotes.Note13 : null,
                        Note14 = q.QuotationNotes != null ? q.QuotationNotes.Note14 : null,
                        Note15 = q.QuotationNotes != null ? q.QuotationNotes.Note15 : null,
                        q.CreatedBy,
                        q.UpdatedBy,
                        ExchangeRates = (from er in _unitOfWork.QuotationExchangeRates.Query(false, false)
                                where er.QuotationId == q.Id && !er.IsDeleted
                                select new { er.Currency, er.ExchangeRate, er.ExchangeRateDate, er.IsOfficial }).ToList(),
                        Lines = (from ql in _unitOfWork.QuotationLines.Query(false, false)
                                where ql.QuotationId == q.Id && !ql.IsDeleted
                                let stockData = _unitOfWork.Stocks.Query(false, false)
                                    .Where(s => !s.IsDeleted &&
                                        ((ql.RelatedStockId.HasValue && s.Id == ql.RelatedStockId.Value) ||
                                         (!ql.RelatedStockId.HasValue && s.ErpStockCode == ql.ProductCode)))
                                    .Select(s => new
                                    {
                                        Id = (long?)s.Id,
                                        s.ErpStockCode,
                                        s.StockName,
                                        s.Unit,
                                        s.UreticiKodu,
                                        s.GrupKodu,
                                        s.GrupAdi,
                                        s.Kod1,
                                        s.Kod1Adi,
                                        s.Kod2,
                                        s.Kod2Adi,
                                        s.Kod3,
                                        s.Kod3Adi,
                                        s.Kod4,
                                        s.Kod4Adi,
                                        s.Kod5,
                                        s.Kod5Adi
                                    })
                                    .FirstOrDefault()
                                select new
                                {
                                    ql.ProductCode,
                                    ProductName = stockData != null ? stockData.StockName : ql.ProductCode,
                                    GroupCode = stockData != null ? (stockData.GrupKodu ?? "") : "",
                                    StockCode = stockData != null ? (stockData.ErpStockCode ?? "") : "",
                                    StockUnit = stockData != null ? (stockData.Unit ?? "") : "",
                                    StockManufacturerCode = stockData != null ? (stockData.UreticiKodu ?? "") : "",
                                    StockGroupName = stockData != null ? (stockData.GrupAdi ?? "") : "",
                                    StockCode1 = stockData != null ? (stockData.Kod1 ?? "") : "",
                                    StockCode1Name = stockData != null ? (stockData.Kod1Adi ?? "") : "",
                                    StockCode2 = stockData != null ? (stockData.Kod2 ?? "") : "",
                                    StockCode2Name = stockData != null ? (stockData.Kod2Adi ?? "") : "",
                                    StockCode3 = stockData != null ? (stockData.Kod3 ?? "") : "",
                                    StockCode3Name = stockData != null ? (stockData.Kod3Adi ?? "") : "",
                                    StockCode4 = stockData != null ? (stockData.Kod4 ?? "") : "",
                                    StockCode4Name = stockData != null ? (stockData.Kod4Adi ?? "") : "",
                                    StockCode5 = stockData != null ? (stockData.Kod5 ?? "") : "",
                                    StockCode5Name = stockData != null ? (stockData.Kod5Adi ?? "") : "",
                                    ql.Quantity,
                                    ql.UnitPrice,
                                    ql.DiscountRate1,
                                    ql.DiscountAmount1,
                                    ql.DiscountRate2,
                                    ql.DiscountAmount2,
                                    ql.DiscountRate3,
                                    ql.DiscountAmount3,
                                    ql.VatRate,
                                    ql.VatAmount,
                                    ql.LineTotal,
                                    ql.LineGrandTotal,
                                    ql.Description,
                                    ql.Description1,
                                    ql.Description2,
                                    ql.Description3,
                                    ql.ErpProjectCode,
                                    HtmlDescription = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.StockDetails.Query(false, false).Where(sd => sd.StockId == stockData.Id.Value && !sd.IsDeleted).Select(sd => sd.HtmlDescription).FirstOrDefault() ?? "")
                                        : "",
                                    DefaultImagePath = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.Repository<StockImage>().Query(false, false).Where(si => si.StockId == stockData.Id.Value && !si.IsDeleted).OrderByDescending(si => si.IsPrimary).ThenBy(si => si.SortOrder).Select(si => si.FilePath).FirstOrDefault() ?? "")
                                        : ""
                                }).ToList()
                    }).FirstOrDefaultAsync().ConfigureAwait(false),

                DocumentRuleType.FastQuotation => await FetchFastQuotationEntityDataAsync(entityId).ConfigureAwait(false),

                DocumentRuleType.Activity => await FetchActivityEntityDataAsync(entityId).ConfigureAwait(false),

                DocumentRuleType.Order => await (from o in _unitOfWork.Orders.Query(false, false)
                    where o.Id == entityId && !o.IsDeleted
                    select new
                    {
                        o.Id,
                        o.OfferNo,
                        o.OfferDate,
                        o.OfferType,
                        o.RevisionNo,
                        CustomerName = o.PotentialCustomer != null ? o.PotentialCustomer.CustomerName : (o.ErpCustomerCode ?? ""),
                        PotentialCustomerName = o.PotentialCustomer != null ? o.PotentialCustomer.CustomerName : "",
                        o.ErpCustomerCode,
                        o.DeliveryDate,
                        ShippingAddressText = o.ShippingAddress != null ? o.ShippingAddress.Address : "",
                        RepresentativeName = o.Representative != null ? o.Representative.FullName : "",
                        o.Description,
                        PaymentTypeName = o.PaymentType != null ? o.PaymentType.Name : "",
                        DocumentSerialTypeName = o.DocumentSerialType != null ? o.DocumentSerialType.SerialPrefix : "",
                        o.Currency,
                        o.CreatedBy,
                        o.UpdatedBy,
                        ExchangeRates = (from er in _unitOfWork.OrderExchangeRates.Query(false, false)
                                where er.OrderId == o.Id && !er.IsDeleted
                                select new { er.Currency, er.ExchangeRate, er.ExchangeRateDate, er.IsOfficial }).ToList(),
                        Lines = (from ol in _unitOfWork.OrderLines.Query(false, false)
                                where ol.OrderId == o.Id && !ol.IsDeleted
                                let stockData = _unitOfWork.Stocks.Query(false, false)
                                    .Where(s => !s.IsDeleted &&
                                        ((ol.RelatedStockId.HasValue && s.Id == ol.RelatedStockId.Value) ||
                                         (!ol.RelatedStockId.HasValue && s.ErpStockCode == ol.ProductCode)))
                                    .Select(s => new
                                    {
                                        Id = (long?)s.Id,
                                        s.ErpStockCode,
                                        s.StockName,
                                        s.Unit,
                                        s.UreticiKodu,
                                        s.GrupKodu,
                                        s.GrupAdi,
                                        s.Kod1,
                                        s.Kod1Adi,
                                        s.Kod2,
                                        s.Kod2Adi,
                                        s.Kod3,
                                        s.Kod3Adi,
                                        s.Kod4,
                                        s.Kod4Adi,
                                        s.Kod5,
                                        s.Kod5Adi
                                    })
                                    .FirstOrDefault()
                                select new
                                {
                                    ol.ProductCode,
                                    ProductName = stockData != null ? stockData.StockName : ol.ProductCode,
                                    GroupCode = stockData != null ? (stockData.GrupKodu ?? "") : "",
                                    StockCode = stockData != null ? (stockData.ErpStockCode ?? "") : "",
                                    StockUnit = stockData != null ? (stockData.Unit ?? "") : "",
                                    StockManufacturerCode = stockData != null ? (stockData.UreticiKodu ?? "") : "",
                                    StockGroupName = stockData != null ? (stockData.GrupAdi ?? "") : "",
                                    StockCode1 = stockData != null ? (stockData.Kod1 ?? "") : "",
                                    StockCode1Name = stockData != null ? (stockData.Kod1Adi ?? "") : "",
                                    StockCode2 = stockData != null ? (stockData.Kod2 ?? "") : "",
                                    StockCode2Name = stockData != null ? (stockData.Kod2Adi ?? "") : "",
                                    StockCode3 = stockData != null ? (stockData.Kod3 ?? "") : "",
                                    StockCode3Name = stockData != null ? (stockData.Kod3Adi ?? "") : "",
                                    StockCode4 = stockData != null ? (stockData.Kod4 ?? "") : "",
                                    StockCode4Name = stockData != null ? (stockData.Kod4Adi ?? "") : "",
                                    StockCode5 = stockData != null ? (stockData.Kod5 ?? "") : "",
                                    StockCode5Name = stockData != null ? (stockData.Kod5Adi ?? "") : "",
                                    ol.Quantity,
                                    ol.UnitPrice,
                                    ol.DiscountRate1,
                                    ol.DiscountAmount1,
                                    ol.DiscountRate2,
                                    ol.DiscountAmount2,
                                    ol.DiscountRate3,
                                    ol.DiscountAmount3,
                                    ol.VatRate,
                                    ol.VatAmount,
                                    ol.LineTotal,
                                    ol.LineGrandTotal,
                                    ol.Description,
                                    HtmlDescription = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.StockDetails.Query(false, false).Where(sd => sd.StockId == stockData.Id.Value && !sd.IsDeleted).Select(sd => sd.HtmlDescription).FirstOrDefault() ?? "")
                                        : "",
                                    DefaultImagePath = stockData != null && stockData.Id.HasValue
                                        ? (_unitOfWork.Repository<StockImage>().Query(false, false).Where(si => si.StockId == stockData.Id.Value && !si.IsDeleted).OrderByDescending(si => si.IsPrimary).ThenBy(si => si.SortOrder).Select(si => si.FilePath).FirstOrDefault() ?? "")
                                        : ""
                                }).ToList()
                    }).FirstOrDefaultAsync().ConfigureAwait(false),

                _ => throw new ArgumentException($"Unsupported rule type: {ruleType}")
            };
        }
    }
}
