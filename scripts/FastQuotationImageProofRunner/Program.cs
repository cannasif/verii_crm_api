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

namespace FastQuotationImageProofRunner;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var apiRoot = ResolveApiRoot();
        var root = Directory.GetParent(apiRoot)!.FullName;
        var configuration = BuildConfiguration(apiRoot);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection could not be resolved.");

        var sourceImage = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(root, "WhatsApp Image 2026-03-18 at 11.56.07.jpeg");
        var outputPdf = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.Combine(root, "pdf-samples", "fast-quotation-image-proof.pdf");

        if (!File.Exists(sourceImage))
            throw new FileNotFoundException("Source image not found.", sourceImage);

        var dbOptions = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var fakeLocalization = new FakeLocalizationService();
        var httpAccessor = new HttpContextAccessor();

        await using var db = new CmsDbContext(dbOptions);
        using var uow = new UnitOfWork(db, httpAccessor, fakeLocalization);

        var template = await UpsertProofTemplateAsync(db).ConfigureAwait(false);
        var relativeImagePath = await CopyProofImageAsync(apiRoot, sourceImage).ConfigureAwait(false);
        var tempQuotation = await CreateProofFastQuotationAsync(db, relativeImagePath).ConfigureAwait(false);

        var templateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Template JSON could not be deserialized.");

        var generator = new PdfReportDocumentGeneratorService(
            uow,
            NullLogger<PdfReportDocumentGeneratorService>.Instance,
            null!,
            Options.Create(new PdfBuilderOptions
            {
                LocalImageBasePath = apiRoot,
            }));

        var bytes = await generator.GeneratePdfAsync(DocumentRuleType.FastQuotation, tempQuotation.Id, templateData).ConfigureAwait(false);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);
        await File.WriteAllBytesAsync(outputPdf, bytes).ConfigureAwait(false);

        Console.WriteLine($"template:{template.Id}|{template.Title}");
        Console.WriteLine($"fastQuotation:{tempQuotation.Id}|{tempQuotation.QuotationNo}");
        Console.WriteLine($"pdf:{outputPdf}");
    }

    private static async Task<ReportTemplate> UpsertProofTemplateAsync(CmsDbContext db)
    {
        const string sourceTitle = "Windo teklif v4";
        const string targetTitle = "Windo hızlı teklif v4";

        var sourceTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.Quotation)
            .OrderByDescending(x => x.Default)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(x => x.Title == sourceTitle)
            .ConfigureAwait(false);

        if (sourceTemplate == null)
        {
            sourceTemplate = await db.ReportTemplates
                .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.Quotation)
                .OrderByDescending(x => x.Default)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("No quotation template found for FastQuotation proof.");
        }

        var templateData = JsonSerializer.Deserialize<ReportTemplateData>(sourceTemplate.TemplateJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Source template JSON could not be deserialized.");

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

        var serialized = JsonSerializer.Serialize(templateData);

        var targetTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation)
            .FirstOrDefaultAsync(x => x.Title == targetTitle)
            .ConfigureAwait(false);

        if (targetTemplate == null)
        {
            targetTemplate = new ReportTemplate
            {
                RuleType = DocumentRuleType.FastQuotation,
                Title = targetTitle,
                TemplateJson = serialized,
                IsActive = true,
                Default = false,
            };
            db.ReportTemplates.Add(targetTemplate);
        }
        else
        {
            targetTemplate.TemplateJson = serialized;
            targetTemplate.IsActive = true;
            targetTemplate.UpdatedDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        return targetTemplate;
    }

    private static async Task<string> CopyProofImageAsync(string apiRoot, string sourceImage)
    {
        var extension = Path.GetExtension(sourceImage);
        var relativeUrl = $"/uploads/pdf-template-assets/proofs/fast-quotation-proof{extension}";
        var targetPath = Path.Combine(apiRoot, "uploads", "pdf-template-assets", "proofs", $"fast-quotation-proof{extension}");

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = File.OpenRead(sourceImage);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target).ConfigureAwait(false);

        return relativeUrl;
    }

    private static async Task<TempQuotattion> CreateProofFastQuotationAsync(CmsDbContext db, string relativeImagePath)
    {
        var customer = await db.Customers.Where(x => !x.IsDeleted).OrderBy(x => x.Id).FirstOrDefaultAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("No customer found for proof fast quotation.");

        var header = new TempQuotattion
        {
            CustomerId = customer.Id,
            QuotationNo = $"HT-IMG-{DateTime.UtcNow:yyyyMMddHHmmss}",
            OfferDate = DateTime.UtcNow,
            CurrencyCode = "TRY",
            ExchangeRate = 1m,
            Description = "Gorsel test hizli teklif",
            IsApproved = false,
        };

        db.TempQuotattions.Add(header);
        await db.SaveChangesAsync().ConfigureAwait(false);

        var line = new TempQuotattionLine
        {
            TempQuotattionId = header.Id,
            ProductCode = "IMG-001",
            ProductName = "Gorselli Hizli Teklif Kalemi",
            ImagePath = relativeImagePath,
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
        };

        db.TempQuotattionLines.Add(line);
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
}
