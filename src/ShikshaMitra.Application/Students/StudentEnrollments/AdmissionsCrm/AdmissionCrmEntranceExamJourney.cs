using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamJourney(AdmissionCrmEnrollmentAutomationCoordinator automation) : ITransientDependency
{
	public Task RunAsync(AdmissionInquiryLead lead, AdmissionCrmJourneyTrigger trigger) =>
		automation.RunJourneyRulesAsync(
			lead.AcademicYearId,
			new RunAdmissionCrmJourneyRulesDto { Trigger = trigger, LeadId = lead.Id, Stage = lead.Stage });

	public Task RunStageReachedIfChangedAsync(AdmissionInquiryLead lead, AdmissionInquiryStage previousStage) =>
		lead.Stage == previousStage
			? Task.CompletedTask
			: RunAsync(lead, AdmissionCrmJourneyTrigger.StageReached);
}