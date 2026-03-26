using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using crm_api.Models;

namespace crm_api.Data.Configurations
{
    public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            // Primary key configuration
            builder.HasKey(e => e.Id);

            // Primary key property configuration
            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            // Base properties configuration
            builder.Property(e => e.CreatedDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(e => e.UpdatedDate)
                .IsRequired(false);

            builder.Property(e => e.DeletedDate)
                .IsRequired(false);

            builder.Property(e => e.IsDeleted)
                .IsRequired();

            // Audit fields are nullable long FKs; no MaxLength configuration

            // Foreign key relationships with NoAction to prevent cascade cycles
            builder.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.UpdatedByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.DeletedByUser)
                .WithMany()
                .HasForeignKey(e => e.DeletedBy)
                .OnDelete(DeleteBehavior.NoAction);

            // Global query filter for soft delete is applied on root entity types (e.g., BaseHeaderEntity)

            // Configure specific entity
            ConfigureEntity(builder);
        }

        protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
    }
}
