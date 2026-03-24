using System.Text.Json;
using crm_api.Data;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.Services;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using depoWebAPI.Models;
using crm_api.DTOs.ErpDto;

namespace FastQuotationTemplateUpsertRunner;

public static class Program
{
    private const string SourceTemplateTitle = "Windo hızlı teklif v4";
    private const string TargetTemplateTitle = "Windo hızlı teklif v5";

    public static async Task Main(string[] args)
    {
        var apiRoot = ResolveApiRoot();
        var root = Directory.GetParent(apiRoot)!.FullName;
        var configuration = BuildConfiguration(apiRoot);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection could not be resolved.");

        var brochureImages = new[]
        {
            args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "WhatsApp Image 2026-03-18 at 11.56.07.jpeg"),
            args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "WhatsApp Image 2026-03-18 at 11.56.08.jpeg"),
            args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(root, "WhatsApp Image 2026-03-18 at 11.56.08 (1).jpeg"),
        };
        var outputPdf = args.Length > 3
            ? Path.GetFullPath(args[3])
            : Path.Combine(root, "pdf-samples", "fast-quotation-v5-proof.pdf");

        foreach (var image in brochureImages)
        {
            if (!File.Exists(image))
                throw new FileNotFoundException("Brochure image not found.", image);
        }

        var dbOptions = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var fakeLocalization = new FakeLocalizationService();
        var httpAccessor = new HttpContextAccessor();

        await using var db = new CmsDbContext(dbOptions);
        using var uow = new UnitOfWork(db, httpAccessor, fakeLocalization);

        var template = await UpsertTemplateAsync(db, apiRoot, brochureImages).ConfigureAwait(false);
        var lineImagePath = await EnsureTemplateAssetAsync(db, apiRoot, template.Id, brochureImages[0], "fast-quotation-v5-line").ConfigureAwait(false);
        var fastQuotation = await CreateProofFastQuotationAsync(db, lineImagePath).ConfigureAwait(false);

        var templateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson, SerializerOptions)
            ?? throw new InvalidOperationException("Template JSON could not be deserialized.");

        var generator = new PdfReportDocumentGeneratorService(
            uow,
            NullLogger<PdfReportDocumentGeneratorService>.Instance,
            null!,
            Options.Create(new PdfBuilderOptions
            {
                LocalImageBasePath = apiRoot,
            }),
            new FakeErpService());

        var bytes = await generator.GeneratePdfAsync(DocumentRuleType.FastQuotation, fastQuotation.Id, templateData).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);
        await File.WriteAllBytesAsync(outputPdf, bytes).ConfigureAwait(false);

        Console.WriteLine($"template:{template.Id}|{template.Title}");
        Console.WriteLine($"fastQuotation:{fastQuotation.Id}|{fastQuotation.QuotationNo}");
        Console.WriteLine($"pdf:{outputPdf}");
    }

    private static JsonSerializerOptions SerializerOptions => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task<ReportTemplate> UpsertTemplateAsync(CmsDbContext db, string apiRoot, IReadOnlyList<string> brochureImages)
    {
        var sourceTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation)
            .OrderByDescending(x => x.Default)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(x => x.Title == SourceTemplateTitle)
            .ConfigureAwait(false);

        sourceTemplate ??= await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation)
            .OrderByDescending(x => x.Default)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("No FastQuotation template found.");

        var templateData = JsonSerializer.Deserialize<ReportTemplateData>(sourceTemplate.TemplateJson, SerializerOptions)
            ?? throw new InvalidOperationException("Source template JSON could not be deserialized.");

        var targetTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation)
            .FirstOrDefaultAsync(x => x.Title == TargetTemplateTitle)
            .ConfigureAwait(false);

        if (targetTemplate == null)
        {
            targetTemplate = new ReportTemplate
            {
                RuleType = DocumentRuleType.FastQuotation,
                Title = TargetTemplateTitle,
                TemplateJson = sourceTemplate.TemplateJson,
                IsActive = true,
                Default = false,
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow,
            };
            db.ReportTemplates.Add(targetTemplate);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        var brochurePaths = new List<string>();
        for (var index = 0; index < brochureImages.Count; index++)
        {
            brochurePaths.Add(
                await EnsureTemplateAssetAsync(
                    db,
                    apiRoot,
                    targetTemplate.Id,
                    brochureImages[index],
                    $"fast-quotation-v5-brochure-{index + 1}").ConfigureAwait(false));
        }

        templateData.Page.PageCount = Math.Max(templateData.Page.PageCount, 4);
        templateData.Elements = templateData.Elements
            .Where(element => !IsAssignedToBrochurePages(element))
            .ToList();

        for (var index = 0; index < brochurePaths.Count; index++)
        {
            templateData.Elements.Add(new ReportElement
            {
                Id = $"fast-quotation-v5-brochure-{index + 1}",
                Type = "image",
                Section = "page",
                X = 0,
                Y = 0,
                Width = 210,
                Height = 297,
                ZIndex = -100 + index,
                PageNumbers = new List<int> { index + 1 },
                Value = brochurePaths[index],
                Style = new ElementStyle
                {
                    ImageFit = "cover",
                },
            });
        }

        foreach (var table in templateData.Elements.Where(x => string.Equals(x.Type, "table", StringComparison.OrdinalIgnoreCase) && x.Columns != null))
        {
            table.Columns = new List<TableColumn>
            {
                new() { Label = "Gorsel", Path = "Lines.ImagePath", Align = "center" },
                new() { Label = "Stok Kodu", Path = "Lines.ProductCode" },
                new() { Label = "Stok Bilgisi", Path = "Lines.ProductName" },
                new() { Label = "Miktar", Path = "Lines.Quantity", Align = "right" },
                new() { Label = "Birim Fiyat", Path = "Lines.UnitPrice", Align = "right", Format = "currency" },
                new() { Label = "Toplam", Path = "Lines.LineGrandTotal", Align = "right", Format = "currency" },
            };
            table.ColumnWidths = new List<decimal> { 16m, 28m, 72m, 18m, 24m, 28m };
            table.TableOptions ??= new TableOptions();
            table.TableOptions.DetailColumnPath = null;
            table.TableOptions.DetailPaths = new List<string>();
            table.TableOptions.RepeatHeader = true;
        }

        foreach (var element in templateData.Elements)
        {
            if (string.Equals(element.Id, "offer-delivery-label", StringComparison.OrdinalIgnoreCase))
            {
                element.Text = "Para Birimi:";
            }

            if (string.Equals(element.Id, "offer-delivery", StringComparison.OrdinalIgnoreCase))
            {
                element.Path = "Currency";
            }

            if (string.Equals(element.Id, "customer-rep", StringComparison.OrdinalIgnoreCase))
            {
                element.Path = "ShippingAddressText";
                element.Height = 6.35m;
            }
        }

        foreach (var template in db.ReportTemplates.Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation))
        {
            template.Default = false;
        }

        await db.SaveChangesAsync().ConfigureAwait(false);

        targetTemplate.Title = TargetTemplateTitle;
        targetTemplate.TemplateJson = JsonSerializer.Serialize(templateData);
        targetTemplate.IsActive = true;
        targetTemplate.Default = true;
        targetTemplate.UpdatedBy = 1;
        targetTemplate.UpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync().ConfigureAwait(false);
        return targetTemplate;
    }

    private static bool IsAssignedToBrochurePages(ReportElement element)
    {
        if (element.PageNumbers == null || element.PageNumbers.Count == 0)
            return false;

        return element.PageNumbers.Any(page => page is >= 1 and <= 3);
    }

    private static async Task<string> EnsureTemplateAssetAsync(
        CmsDbContext db,
        string apiRoot,
        long templateId,
        string sourceImage,
        string slug)
    {
        var extension = Path.GetExtension(sourceImage).ToLowerInvariant();
        var storedFileName = $"{slug}-{Guid.NewGuid():N}{extension}";
        var relativeUrl = $"/uploads/pdf-template-assets/templates/{templateId}/{storedFileName}";
        var fullPath = Path.Combine(apiRoot, "uploads", "pdf-template-assets", "templates", templateId.ToString(), storedFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var source = File.OpenRead(sourceImage))
        await using (var target = File.Create(fullPath))
        {
            await source.CopyToAsync(target).ConfigureAwait(false);
        }

        var asset = new PdfTemplateAsset
        {
            OriginalFileName = Path.GetFileName(sourceImage),
            StoredFileName = storedFileName,
            RelativeUrl = relativeUrl,
            ContentType = extension is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png",
            SizeBytes = new FileInfo(sourceImage).Length,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow,
        };

        db.Set<PdfTemplateAsset>().Add(asset);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return relativeUrl;
    }

    private static async Task<TempQuotattion> CreateProofFastQuotationAsync(CmsDbContext db, string lineImagePath)
    {
        var customer = await db.Customers.Where(x => !x.IsDeleted).OrderBy(x => x.Id).FirstOrDefaultAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("No customer found for proof fast quotation.");

        var header = new TempQuotattion
        {
            CustomerId = customer.Id,
            QuotationNo = $"HT-V5-{DateTime.UtcNow:yyyyMMddHHmmss}",
            OfferDate = DateTime.UtcNow,
            CurrencyCode = "3",
            ExchangeRate = 1m,
            Description = "V5 brochure + line image proof",
            IsApproved = false,
        };

        db.TempQuotattions.Add(header);
        await db.SaveChangesAsync().ConfigureAwait(false);

        db.TempQuotattionLines.Add(new TempQuotattionLine
        {
            TempQuotattionId = header.Id,
            ProductCode = "IMG-001",
            ProductName = "Gorselli Hizli Teklif Kalemi",
            ImagePath = lineImagePath,
            Quantity = 1m,
            UnitPrice = 1250m,
            DiscountRate1 = 0m,
            DiscountAmount1 = 0m,
            DiscountRate2 = 0m,
            DiscountAmount2 = 0m,
            DiscountRate3 = 0m,
            DiscountAmount3 = 0m,
            VatRate = 20m,
            VatAmount = 250m,
            LineTotal = 1250m,
            LineGrandTotal = 1500m,
            Description = "Bu satir gorsel test icin olusturuldu.",
        });

        await db.SaveChangesAsync().ConfigureAwait(false);
        return header;
    }

    private static IConfigurationRoot BuildConfiguration(string apiRoot)
    {
        return new ConfigurationBuilder()
            .SetBasePath(apiRoot)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveApiRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "crm_api.csproj")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("API root could not be resolved.");
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public string GetLocalizedString(string key) => key;
        public string GetLocalizedString(string key, params object[] arguments) => string.Format(key, arguments);
    }

    private sealed class FakeErpService : IErpService
    {
        public Task<ApiResponse<short>> GetBranchCodeFromContext() => throw new NotSupportedException();
        public Task<ApiResponse<List<CariDto>>> GetCarisAsync(string? cariKodu) => throw new NotSupportedException();
        public Task<ApiResponse<List<CariDto>>> GetCarisByCodesAsync(IEnumerable<string> cariKodlari) => throw new NotSupportedException();
        public Task<ApiResponse<List<StokFunctionDto>>> GetStoksAsync(string? stokKodu) => throw new NotSupportedException();
        public Task<ApiResponse<List<BranchDto>>> GetBranchesAsync(int? branchNo = null) => throw new NotSupportedException();
        public Task<ApiResponse<List<ErpCariMovementDto>>> GetCariMovementsAsync(string customerCode) => throw new NotSupportedException();
        public Task<ApiResponse<List<ErpCariBalanceDto>>> GetCariBalancesAsync(string customerCode) => throw new NotSupportedException();
        public Task<ApiResponse<List<ErpShippingAddressDto>>> GetErpShippingAddressAsync(string customerCode) => throw new NotSupportedException();
        public Task<ApiResponse<List<StokGroupDto>>> GetStokGroupAsync(string? grupKodu) => throw new NotSupportedException();
        public Task<ApiResponse<List<ProjeDto>>> GetProjectCodesAsync() => throw new NotSupportedException();
        public Task<ApiResponse<object>> HealthCheckAsync() => throw new NotSupportedException();

        public Task<ApiResponse<List<KurDto>>> GetExchangeRateAsync(DateTime tarih, int fiyatTipi)
        {
            return Task.FromResult(ApiResponse<List<KurDto>>.SuccessResult(new List<KurDto>
            {
                new() { DovizTipi = 1, DovizIsmi = "TL", KurDegeri = 1 },
                new() { DovizTipi = 2, DovizIsmi = "USD", KurDegeri = 36.1 },
                new() { DovizTipi = 3, DovizIsmi = "EURO", KurDegeri = 39.2 },
                new() { DovizTipi = 4, DovizIsmi = "GBP", KurDegeri = 45.7 },
            }, "ok"));
        }
    }
}
