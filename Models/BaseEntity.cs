using System;

namespace crm_api.Models
{
    public abstract class BaseEntity
    {
        public long Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTimeProvider.Now;
        public DateTime? UpdatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    
        // User Informations
        public long? CreatedBy { get; set; }
        public User? CreatedByUser { get; set; }

        public long? UpdatedBy { get; set; }
        public User? UpdatedByUser { get; set; }

        public long? DeletedBy { get; set; }
        public User? DeletedByUser { get; set; }

    }
}