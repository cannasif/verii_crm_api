using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using crm_api.Models;

namespace crm_api.Data.Configurations
{
    public class ActivityMeetingTypeConfiguration : BaseEntityConfiguration<ActivityMeetingType>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ActivityMeetingType> builder)
        {
            builder.ToTable("RII_ACTIVITY_MEETING_TYPE");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            builder.HasIndex(e => e.Name).HasDatabaseName("IX_ActivityMeetingType_Name");
            builder.HasIndex(e => e.CreatedDate).HasDatabaseName("IX_ActivityMeetingType_CreatedDate");
            builder.HasIndex(e => e.IsDeleted).HasDatabaseName("IX_ActivityMeetingType_IsDeleted");

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
