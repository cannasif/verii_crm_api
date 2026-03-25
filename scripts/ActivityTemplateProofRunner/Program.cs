using System.Text.Json;
using crm_api.Data;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.Services;
using crm_api.UnitOfWork;
using depoWebAPI.Models;
using crm_api.DTOs.ErpDto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ActivityTemplateProofRunner;

public static class Program
{
    private const long ActivityId = 70;
    private const string TemplateTitle = "Aktivite Fuar Formu v2";

    public static async Task Main(string[] args)
    {
        var apiRoot = ResolveApiRoot();
        var root = Directory.GetParent(apiRoot)!.FullName;
        var configuration = BuildConfiguration(apiRoot);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection could not be resolved.");
        var outputPdf = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(root, "pdf-samples", "activity-69-proof-v2.pdf");

        var dbOptions = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var fakeLocalization = new FakeLocalizationService();
        var httpAccessor = new HttpContextAccessor();

        await using var db = new CmsDbContext(dbOptions);
        using var uow = new UnitOfWork(db, httpAccessor, fakeLocalization);

        var activity = await db.Activities
            .Include(x => x.ActivityType)
            .Include(x => x.PaymentType)
            .Include(x => x.ActivityMeetingType)
            .Include(x => x.ActivityTopicPurpose)
            .Include(x => x.ActivityShipping)
            .Include(x => x.AssignedUser)
            .Include(x => x.Contact)
            .Include(x => x.PotentialCustomer)
                .ThenInclude(x => x!.DefaultShippingAddress)
            .FirstOrDefaultAsync(x => x.Id == ActivityId && !x.IsDeleted)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Activity {ActivityId} not found.");

        var template = await UpsertTemplateAsync(db).ConfigureAwait(false);

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

        var bytes = await generator.GeneratePdfAsync(DocumentRuleType.Activity, activity.Id, templateData).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);
        await File.WriteAllBytesAsync(outputPdf, bytes).ConfigureAwait(false);

        Console.WriteLine($"template:{template.Id}|{template.Title}");
        Console.WriteLine($"activity:{activity.Id}|{activity.Subject}");
        Console.WriteLine($"paymentTypeId:{activity.PaymentTypeId}");
        Console.WriteLine($"activityMeetingTypeId:{activity.ActivityMeetingTypeId}");
        Console.WriteLine($"activityTopicPurposeId:{activity.ActivityTopicPurposeId}");
        Console.WriteLine($"activityShippingId:{activity.ActivityShippingId}");
        Console.WriteLine($"assigned:{activity.AssignedUser?.FullName}|{activity.AssignedUser?.Email}");
        Console.WriteLine($"contact:{activity.Contact?.FullName}|{activity.Contact?.Email}|{activity.Contact?.Phone}|{activity.Contact?.Mobile}");
        Console.WriteLine($"customer:{activity.PotentialCustomer?.CustomerName}|{activity.PotentialCustomer?.Email}|{activity.PotentialCustomer?.Phone1}|{activity.PotentialCustomer?.Phone2}");
        Console.WriteLine($"pdf:{outputPdf}");
    }

    private static JsonSerializerOptions SerializerOptions => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task<ReportTemplate> UpsertTemplateAsync(CmsDbContext db)
    {
        var template = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.Activity)
            .FirstOrDefaultAsync(x => x.Title == TemplateTitle)
            .ConfigureAwait(false);

        var templateData = BuildTemplateData();
        var serialized = JsonSerializer.Serialize(templateData);

        if (template == null)
        {
            template = new ReportTemplate
            {
                RuleType = DocumentRuleType.Activity,
                Title = TemplateTitle,
                TemplateJson = serialized,
                IsActive = true,
                Default = false,
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow,
            };
            db.ReportTemplates.Add(template);
        }
        else
        {
            template.TemplateJson = serialized;
            template.IsActive = true;
            template.UpdatedBy = 1;
            template.UpdatedDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        return template;
    }

    private static ReportTemplateData BuildTemplateData()
    {
        var logoPath = Path.Combine(Directory.GetParent(ResolveApiRoot())!.FullName, "pdf-samples", "assets", "bilginoglu-endustri-logo.png");
        var elements = new List<ReportElement>
        {
            new()
            {
                Id = "logo",
                Type = "image",
                Section = "page",
                X = 16,
                Y = 10,
                Width = 58,
                Height = 18,
                Value = ToDataUri(logoPath),
                Style = new ElementStyle
                {
                    ImageFit = "contain",
                },
            },
            CreateText("title", 88, 13, 92, 9, "MUSTERI GORUSME FORMU", 16, true),
            CreateShape("header-line", 14, 30, 182, 0.6m, "#1d4d8f"),

            CreateHeaderLabel("fair-title", 123, 34, 22, "Fuar Adi"),
            CreateFieldBox("fair-box", 142, 32, 48, 11),
            CreateText("fair-name", 145, 35, 42, 3.5m, "Kazakistan Makine Fuari'26", 7, true, "Helvetica-Bold"),
            CreateText("fair-date", 150, 39, 30, 3.5m, "01-03 NISAN 2026", 6.5m, false),

            CreateHeaderLabel("company-label", 18, 38, 48, "Firma Adi, Adresi"),
            CreateUnderline("company-line", 18, 48, 104),
            CreateField("company-field", 18, 43.5m, 104, 4.5m, "CustomerName", false),
            CreateUnderline("address-line", 18, 58, 104),
            CreateField("address-field", 18, 52.5m, 104, 5.5m, "CustomerAddress", true, 7.5m),

            CreateHeaderLabel("contact-label", 18, 66, 90, "Gorusulen Kisi / Gorevi / E-Mail"),
            CreateUnderline("contact-line", 18, 76, 104),
            CreateField("contact-field", 18, 71.5m, 104, 4.5m, "ContactName", false),

            CreateHeaderLabel("email-label", 18, 82, 26, "E-Posta"),
            CreateUnderline("email-line", 18, 92, 50),
            CreateField("email-field", 18, 87.5m, 50, 4.5m, "ContactEmail", false),
            CreateHeaderLabel("phone-label", 72, 82, 22, "Telefon"),
            CreateUnderline("phone-line", 72, 92, 50),
            CreateField("phone-field", 72, 87.5m, 50, 4.5m, "ContactPhone", false),

            CreateSectionTitle("visit-title", 129, 48, "ZIYARET TARIHI"),
            CreateFieldBox("visit-current-box", 129, 54, 61, 14),
            CreateField("visit-current", 132, 58, 55, 7, "StartDateTime", false, 9, "Helvetica-Bold"),

            CreateSectionTitle("shipping-title", 129, 76, "TESLIMAT"),
            CreateFieldBox("shipping-current-box", 129, 82, 28, 14),
            CreateField("shipping-current", 132, 86, 22, 7, "ActivityShippingName", false, 9, "Helvetica-Bold"),

            CreateSectionTitle("payment-title", 162, 76, "ODEME"),
            CreateFieldBox("payment-current-box", 162, 82, 28, 14),
            CreateField("payment-current", 165, 86, 22, 7, "PaymentTypeName", false, 9, "Helvetica-Bold"),

            CreateSectionTitle("topic-title", 18, 104, "ILGILENILEN KONULAR"),
            CreateFieldBox("topic-current-box", 18, 110, 104, 18),
            CreateField("topic-current", 21, 115, 98, 10, "ActivityTopicPurposeName", true, 9, "Helvetica-Bold"),

            CreateSectionTitle("meeting-title", 129, 104, "GORUSME"),
            CreateFieldBox("meeting-current-box", 129, 110, 61, 18),
            CreateField("meeting-current", 132, 115, 55, 10, "ActivityMeetingTypeName", true, 9, "Helvetica-Bold"),

            CreateSectionTitle("summary-title", 94, 140, "GORUSME OZETI"),
            CreateShape("summary-frame", 18, 146, 172, 128, "#d5dbe6", "#ffffff", "1px solid #d5dbe6"),
        };

        elements.AddRange(CreateGridLines(18, 146, 172, 128, 4, 4));
        elements.Add(CreateField("summary-field", 21, 149, 166, 120, "Description", true, 9));

        elements.Add(CreateHeaderLabel("footer-left", 18, 279, 34, "Gorusen Kisi"));
        elements.Add(CreateUnderline("footer-left-line", 18, 287, 58));
        elements.Add(CreateField("footer-user", 18, 282.5m, 58, 4, "AssignedUserName", false, 8));
        elements.Add(CreateText("footer-rev", 168, 284, 22, 4, "FR 07.01.01 REV: 00", 6, false));

        return new ReportTemplateData
        {
            SchemaVersion = 1,
            Page = new PageConfig
            {
                Width = 210,
                Height = 297,
                Unit = "mm",
                PageCount = 1,
            },
            Elements = elements
        };
    }

    private static ReportElement CreateText(string id, decimal x, decimal y, decimal width, decimal height, string text, decimal fontSize, bool bold, string? fontFamily = null)
    {
        return new ReportElement
        {
            Id = id,
            Type = "text",
            Section = "page",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = text,
            FontSize = fontSize,
            FontFamily = fontFamily ?? (bold ? "Helvetica-Bold" : "Helvetica"),
            Color = "#1e293b",
        };
    }

    private static ReportElement CreateSectionTitle(string id, decimal x, decimal y, string text)
        => CreateText(id, x, y, 52, 5, text.ToUpperInvariant(), 10.5m, true, "Helvetica-Bold");

    private static ReportElement CreateHeaderLabel(string id, decimal x, decimal y, decimal width, string text)
        => CreateText(id, x, y, width, 4.5m, text.ToUpperInvariant(), 9.5m, true, "Helvetica-Bold");

    private static ReportElement CreateUnderline(string id, decimal x, decimal y, decimal width)
        => CreateShape(id, x, y, width, 0.35m, "#7b8794");

    private static ReportElement CreateFieldBox(string id, decimal x, decimal y, decimal width, decimal height)
        => CreateShape(id, x, y, width, height, "#b7c0cc", "#ffffff", "1px solid #b7c0cc");

    private static ReportElement CreateCheckboxRow(string id, decimal x, decimal y, string label)
    {
        return new ReportElement
        {
            Id = id,
            Type = "text",
            Section = "page",
            X = x,
            Y = y,
            Width = 28,
            Height = 4,
            Text = $"{label}   □",
            FontSize = 7,
            FontFamily = "Helvetica",
            Color = "#1f2937",
        };
    }

    private static IEnumerable<ReportElement> CreateGridLines(decimal x, decimal y, decimal width, decimal height, decimal stepX, decimal stepY)
    {
        var elements = new List<ReportElement>();
        var rowIndex = 0;
        for (decimal currentY = y + stepY; currentY < y + height; currentY += stepY)
        {
            elements.Add(CreateShape($"grid-h-{rowIndex++}", x, currentY, width, 0.12m, "#e5e7eb"));
        }

        var colIndex = 0;
        for (decimal currentX = x + stepX; currentX < x + width; currentX += stepX)
        {
            elements.Add(CreateShape($"grid-v-{colIndex++}", currentX, y, 0.12m, height, "#e5e7eb"));
        }

        return elements;
    }

    private static ReportElement CreateShape(string id, decimal x, decimal y, decimal width, decimal height, string color, string? background = null, string? border = null)
    {
        return new ReportElement
        {
            Id = id,
            Type = "shape",
            Section = "page",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Style = new ElementStyle
            {
                Background = background ?? color,
                Border = border,
            },
        };
    }

    private static ReportElement CreateField(string id, decimal x, decimal y, decimal width, decimal height, string path, bool multiline = false, decimal fontSize = 8.5m, string fontFamily = "Helvetica")
    {
        return new ReportElement
        {
            Id = id,
            Type = "field",
            Section = "page",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Path = path,
            FontSize = fontSize,
            FontFamily = fontFamily,
            Color = "#0f172a",
            TextOverflow = multiline ? "autoHeight" : null,
            Style = new ElementStyle { Padding = 0 }
        };
    }

    private static string ToDataUri(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };

        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
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
            => Task.FromResult(ApiResponse<List<KurDto>>.SuccessResult(new List<KurDto>(), "ok"));
    }
}
