using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmAnalyticsAppService : IApplicationService
{
	Task<AdmissionCrmAnalyticsSummaryDto> GetSummaryAsync(
		Guid academicYearId,
		GetAdmissionCrmAnalyticsSummaryInput input);
}
