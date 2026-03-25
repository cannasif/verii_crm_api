using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using crm_api.Models;

namespace crm_api.Data.Configurations
{
    public class ActivityShippingConfiguration : BaseEntityConfiguration<ActivityShipping>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ActivityShipping> builder)
        {
            builder.ToTable("RII_ACTIVITY_SHIPPING");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            builder.HasIndex(e => e.Name).HasDatabaseName("IX_ActivityShipping_Name");
            builder.HasIndex(e => e.CreatedDate).HasDatabaseName("IX_ActivityShipping_CreatedDate");
            builder.HasIndex(e => e.IsDeleted).HasDatabaseName("IX_ActivityShipping_IsDeleted");

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
