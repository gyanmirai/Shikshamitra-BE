using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Application service interface for managing inquiry preferences
/// </summary>
public interface IInquiryPreferenceAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of inquiry preferences
	/// </summary>
    Task<PagedResultDto<InquiryPreferenceDto>> GetListAsync(PagedAndSortedResultRequestDto input, Guid academicYearId);

	/// <summary>
	/// Gets an inquiry preference by ID
	/// </summary>
	Task<InquiryPreferenceDto> GetAsync(Guid id);

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
    Task<InquiryPreferenceDto> CreateAsync(Guid academicYearId, CreateUpdateInquiryPreferenceDto input);

	/// <summary>
	/// Updates an existing inquiry preference
	/// </summary>
    Task<InquiryPreferenceDto> UpdateAsync(Guid academicYearId, Guid id, CreateUpdateInquiryPreferenceDto input);

	/// <summary>
	/// Deletes an inquiry preference
	/// </summary>
	Task DeleteAsync(Guid id);
}
