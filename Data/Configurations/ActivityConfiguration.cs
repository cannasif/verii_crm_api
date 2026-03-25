using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using crm_api.Models;

namespace crm_api.Data.Configurations
{
    public class ActivityConfiguration : BaseEntityConfiguration<Activity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<Activity> builder)
        {
            builder.ToTable("RII_ACTIVITY");

            builder.Property(e => e.Subject)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(e => e.Description)
                .HasMaxLength(2000)
                .IsRequired(false);

            builder.Property(e => e.ActivityTypeId)
                .IsRequired();

            builder.HasOne(e => e.ActivityType)
                .WithMany(at => at.Activities)
                .HasForeignKey(e => e.ActivityTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.PaymentType)
                .WithMany()
                .HasForeignKey(e => e.PaymentTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.ActivityMeetingType)
                .WithMany()
                .HasForeignKey(e => e.ActivityMeetingTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.ActivityTopicPurpose)
                .WithMany()
                .HasForeignKey(e => e.ActivityTopicPurposeId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.ActivityShipping)
                .WithMany()
                .HasForeignKey(e => e.ActivityShippingId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Property(e => e.ErpCustomerCode)
                .HasMaxLength(50)
                .IsRequired(false);
            
            builder.Property(e => e.GoogleCalendarEventId)
                .HasMaxLength(512)
                .IsRequired(false);

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(ActivityStatus.Scheduled);

            builder.Property(e => e.Priority)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(ActivityPriority.Medium);

            builder.Property(e => e.StartDateTime).IsRequired();
            builder.Property(e => e.EndDateTime).IsRequired(false);
            builder.Property(e => e.IsAllDay).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.AssignedUserId).IsRequired();

            builder.HasOne(e => e.PotentialCustomer)
                .WithMany()
                .HasForeignKey(e => e.PotentialCustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.Contact)
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.AssignedUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasMany(e => e.Reminders)
                .WithOne(r => r.Activity)
                .HasForeignKey(r => r.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Images)
                .WithOne(i => i.Activity)
                .HasForeignKey(i => i.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.Subject)
                .HasDatabaseName("IX_Activity_Subject");

            builder.HasIndex(e => e.ActivityTypeId)
                .HasDatabaseName("IX_Activity_ActivityTypeId");

            builder.HasIndex(e => e.PaymentTypeId)
                .HasDatabaseName("IX_Activity_PaymentTypeId");

            builder.HasIndex(e => e.ActivityMeetingTypeId)
                .HasDatabaseName("IX_Activity_ActivityMeetingTypeId");

            builder.HasIndex(e => e.ActivityTopicPurposeId)
                .HasDatabaseName("IX_Activity_ActivityTopicPurposeId");

            builder.HasIndex(e => e.ActivityShippingId)
                .HasDatabaseName("IX_Activity_ActivityShippingId");

            builder.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Activity_Status");

            builder.HasIndex(e => e.PotentialCustomerId)
                .HasDatabaseName("IX_Activity_PotentialCustomerId");

            builder.HasIndex(e => e.ContactId)
                .HasDatabaseName("IX_Activity_ContactId");

            builder.HasIndex(e => e.AssignedUserId)
                .HasDatabaseName("IX_Activity_AssignedUserId");

            builder.HasIndex(e => e.StartDateTime)
                .HasDatabaseName("IX_Activity_StartDateTime");

            builder.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_Activity_IsDeleted");

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
