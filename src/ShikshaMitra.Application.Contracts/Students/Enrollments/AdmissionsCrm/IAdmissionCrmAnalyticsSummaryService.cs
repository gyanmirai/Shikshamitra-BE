using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmAnalyticsSummaryService : IApplicationService
{
	Task<AdmissionCrmAnalyticsSummaryDto> GetAsync(Guid academicYearId, GetAdmissionCrmAnalyticsSummaryInput input);
}
