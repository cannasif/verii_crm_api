using crm_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace crm_api.Data.Configurations
{
    public class GoogleIntegrationLogConfiguration : BaseEntityConfiguration<GoogleIntegrationLog>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<GoogleIntegrationLog> builder)
        {
            builder.ToTable("RII_GOOGLE_INTEGRATION_LOGS");

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.Operation)
                .HasMaxLength(120)
                .IsRequired();

            builder.Property(x => x.IsSuccess)
                .IsRequired();

            builder.Property(x => x.Severity)
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Provider)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(x => x.Message)
                .HasMaxLength(2000);

            builder.Property(x => x.ErrorCode)
                .HasMaxLength(256);

            builder.Property(x => x.GoogleCalendarEventId)
                .HasMaxLength(512);

            builder.Property(x => x.MetadataJson)
                .HasMaxLength(4000);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_GoogleIntegrationLogs_TenantId");

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_GoogleIntegrationLogs_UserId");

            builder.HasIndex(x => x.CreatedDate)
                .HasDatabaseName("IX_GoogleIntegrationLogs_CreatedDate");
        }
    }
}
