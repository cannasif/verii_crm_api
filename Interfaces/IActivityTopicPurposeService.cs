using crm_api.DTOs;

namespace crm_api.Interfaces
{
    public interface IActivityTopicPurposeService
    {
        Task<ApiResponse<PagedResponse<ActivityTopicPurposeGetDto>>> GetAllAsync(PagedRequest request);
        Task<ApiResponse<ActivityTopicPurposeGetDto>> GetByIdAsync(long id);
        Task<ApiResponse<ActivityTopicPurposeGetDto>> CreateAsync(ActivityTopicPurposeCreateDto dto);
        Task<ApiResponse<ActivityTopicPurposeGetDto>> UpdateAsync(long id, ActivityTopicPurposeUpdateDto dto);
        Task<ApiResponse<object>> DeleteAsync(long id);
    }
}
