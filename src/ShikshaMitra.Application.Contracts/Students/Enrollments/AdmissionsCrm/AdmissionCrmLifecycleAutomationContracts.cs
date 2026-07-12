using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLifecycleAutomationResultDto
{
	public Guid AcademicYearId { get; set; }
	public int EvaluatedLeadCount { get; set; }
	public int TriggeredLeadCount { get; set; }
	public int CreatedTaskCount { get; set; }
	public int SentMessageCount { get; set; }
	public int ExpiredOfferCount { get; set; }
	public int OfferExpiryFailureCount { get; set; }
	public IReadOnlyDictionary<AdmissionCrmJourneyTrigger, int> TriggerCounts { get; set; } =
		new Dictionary<AdmissionCrmJourneyTrigger, int>();
}

public interface IAdmissionCrmLifecycleAutomationAppService : IApplicationService
{
	Task<AdmissionCrmLifecycleAutomationResultDto> RunAsync(Guid academicYearId);
	Task<AdmissionCrmLifecycleAutomationResultDto> RunFromAutomationAsync(Guid academicYearId);
}
