using crm_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace crm_api.Data.Configurations
{
    public class JobFailureLogConfiguration : BaseEntityConfiguration<JobFailureLog>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<JobFailureLog> builder)
        {
            builder.ToTable("RII_JOB_FAILURE_LOG");

            builder.Property(x => x.JobId)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.JobName)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.FailedAt)
                .IsRequired();

            builder.Property(x => x.Reason)
                .HasMaxLength(2000);

            builder.Property(x => x.ExceptionType)
                .HasMaxLength(500);

            builder.Property(x => x.ExceptionMessage)
                .HasMaxLength(4000);

            builder.Property(x => x.StackTrace)
                .HasMaxLength(8000);

            builder.Property(x => x.Queue)
                .HasMaxLength(100);

            builder.HasIndex(x => x.JobId)
                .HasDatabaseName("IX_JobFailureLog_JobId");

            builder.HasIndex(x => x.FailedAt)
                .HasDatabaseName("IX_JobFailureLog_FailedAt");

            builder.HasIndex(x => x.JobName)
                .HasDatabaseName("IX_JobFailureLog_JobName");
        }
    }
}
