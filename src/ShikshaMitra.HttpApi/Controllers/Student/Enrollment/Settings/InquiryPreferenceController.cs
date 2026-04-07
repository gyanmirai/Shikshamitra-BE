using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.Settings;

/// <summary>
/// API controller for managing inquiry preferences
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/settings/inquiry-preferences")]
public class InquiryPreferenceController(IInquiryPreferenceAppService inquiryPreferenceAppService) : AbpControllerBase
{
	/// <summary>
	/// Gets a paginated list of inquiry preferences
	/// </summary>
	/// <param name="input">Pagination parameters</param>
	/// <returns>Paginated list of inquiry preferences</returns>
	[HttpGet("/api/student-enrollment/{academicYearId}/settings/inquiry-preferences")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsInquiryPreference.View)]
	public async Task<PagedResultDto<InquiryPreferenceDto>> GetListAsync([FromRoute] Guid academicYearId, [FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await inquiryPreferenceAppService.GetListAsync(input, academicYearId);
	}

	/// <summary>
	/// Gets a specific inquiry preference by ID
	/// </summary>
	/// <param name="id">The inquiry preference identifier</param>
	/// <returns>Inquiry preference details</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsInquiryPreference.View)]
	public async Task<InquiryPreferenceDto> GetAsync(Guid id)
	{
		return await inquiryPreferenceAppService.GetAsync(id);
	}

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
	/// <param name="input">Inquiry preference creation data</param>
	/// <returns>Created inquiry preference</returns>
	[HttpPost("/api/student-enrollment/{academicYearId}/settings/inquiry-preferences")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsInquiryPreference.Add)]
    public async Task<InquiryPreferenceDto> CreateAsync([FromRoute] Guid academicYearId, [FromBody] CreateUpdateInquiryPreferenceDto input)
	{
        return await inquiryPreferenceAppService.CreateAsync(academicYearId, input);
	}

	/// <summary>
	/// Updates an existing inquiry preference
	/// </summary>
	/// <param name="id">The inquiry preference identifier</param>
	/// <param name="input">Updated inquiry preference data</param>
	/// <returns>Updated inquiry preference</returns>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsInquiryPreference.Add)]
    public async Task<InquiryPreferenceDto> UpdateAsync(Guid id, [FromBody] CreateUpdateInquiryPreferenceDto input)
	{
        return await inquiryPreferenceAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Deletes an inquiry preference
	/// </summary>
	/// <param name="id">The inquiry preference identifier</param>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsInquiryPreference.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		await inquiryPreferenceAppService.DeleteAsync(id);
	}
}
