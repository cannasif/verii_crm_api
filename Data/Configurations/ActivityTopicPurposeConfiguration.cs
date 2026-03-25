using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using crm_api.Models;

namespace crm_api.Data.Configurations
{
    public class ActivityTopicPurposeConfiguration : BaseEntityConfiguration<ActivityTopicPurpose>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ActivityTopicPurpose> builder)
        {
            builder.ToTable("RII_ACTIVITY_TOPIC_PURPOSE");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            builder.HasIndex(e => e.Name).HasDatabaseName("IX_ActivityTopicPurpose_Name");
            builder.HasIndex(e => e.CreatedDate).HasDatabaseName("IX_ActivityTopicPurpose_CreatedDate");
            builder.HasIndex(e => e.IsDeleted).HasDatabaseName("IX_ActivityTopicPurpose_IsDeleted");

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
