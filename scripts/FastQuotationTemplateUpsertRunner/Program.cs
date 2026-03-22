using crm_api.Data;
using crm_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FastQuotationTemplateUpsertRunner;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var root = ResolveApiRoot();
        var configuration = BuildConfiguration(root);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection could not be resolved.");

        var sourceTitle = args.Length > 0 ? args[0] : "Windo teklif v4";
        var targetTitle = args.Length > 1 ? args[1] : "Windo hızlı teklif v4";

        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new CmsDbContext(options);

        var sourceTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.Quotation)
            .OrderByDescending(x => x.Default)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(x => x.Title == sourceTitle);

        if (sourceTemplate == null)
        {
            sourceTemplate = await db.ReportTemplates
                .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.Quotation)
                .OrderByDescending(x => x.Default)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();
        }

        if (sourceTemplate == null)
            throw new InvalidOperationException("No quotation template was found to clone.");

        var targetTemplate = await db.ReportTemplates
            .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation)
            .FirstOrDefaultAsync(x => x.Title == targetTitle);

        if (sourceTemplate.Default)
        {
            var otherDefaults = await db.ReportTemplates
                .Where(x => !x.IsDeleted && x.RuleType == DocumentRuleType.FastQuotation && x.Default)
                .ToListAsync();

            foreach (var item in otherDefaults)
            {
                if (targetTemplate != null && item.Id == targetTemplate.Id)
                    continue;

                item.Default = false;
                item.UpdatedDate = DateTime.UtcNow;
                item.UpdatedBy = sourceTemplate.UpdatedBy ?? sourceTemplate.CreatedBy;
            }
        }

        if (targetTemplate == null)
        {
            targetTemplate = new ReportTemplate
            {
                RuleType = DocumentRuleType.FastQuotation,
                Title = targetTitle,
                TemplateJson = sourceTemplate.TemplateJson,
                IsActive = sourceTemplate.IsActive,
                Default = sourceTemplate.Default,
                CreatedBy = sourceTemplate.CreatedBy,
                UpdatedBy = sourceTemplate.UpdatedBy,
                CreatedByUserId = sourceTemplate.CreatedByUserId,
                UpdatedByUserId = sourceTemplate.UpdatedByUserId,
            };

            db.ReportTemplates.Add(targetTemplate);
        }
        else
        {
            targetTemplate.TemplateJson = sourceTemplate.TemplateJson;
            targetTemplate.IsActive = sourceTemplate.IsActive;
            targetTemplate.Default = sourceTemplate.Default;
            targetTemplate.UpdatedDate = DateTime.UtcNow;
            targetTemplate.UpdatedBy = sourceTemplate.UpdatedBy ?? sourceTemplate.CreatedBy;
            targetTemplate.UpdatedByUserId = sourceTemplate.UpdatedByUserId ?? sourceTemplate.CreatedByUserId;
        }

        await db.SaveChangesAsync();

        Console.WriteLine($"{targetTemplate.Id}|{targetTemplate.Title}|source:{sourceTemplate.Id}|{sourceTemplate.Title}");
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
}
