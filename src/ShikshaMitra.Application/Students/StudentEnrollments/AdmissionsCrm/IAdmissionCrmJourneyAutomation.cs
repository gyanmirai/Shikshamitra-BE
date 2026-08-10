using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmJourneyAutomation
{
	Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesFromAutomationAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input);
}
