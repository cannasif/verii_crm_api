using System.ComponentModel.DataAnnotations;

namespace crm_api.DTOs
{
    public class ActivityMeetingTypeGetDto : BaseEntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityMeetingTypeCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityMeetingTypeUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityTopicPurposeGetDto : BaseEntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityTopicPurposeCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityTopicPurposeUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityShippingGetDto : BaseEntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityShippingCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityShippingUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
