using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class PolicyHolidayImpactAppService(PolicyHolidayImpactPreviewReader previewReader)
	: ApplicationService, IPolicyHolidayImpactAppService
{
	public async Task<PolicyHolidayImpactPreviewDto> GetPreviewAsync(
		Guid academicYearId,
		Guid? workCalendarPolicyId,
		Guid? payrollPolicyId,
		Guid? attendancePolicyId,
		DateTime? fromDate,
		DateTime? toDate,
		string? month)
	{
		return await previewReader.GetPreviewAsync(
			academicYearId,
			workCalendarPolicyId,
			payrollPolicyId,
			attendancePolicyId,
			fromDate,
			toDate,
			month);
	}
}
