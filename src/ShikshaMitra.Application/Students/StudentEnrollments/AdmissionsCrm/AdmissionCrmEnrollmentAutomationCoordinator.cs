using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEnrollmentAutomationCoordinator(
	AdmissionCrmStaleArchiveWorkflow staleArchive,
	AdmissionCrmSlaReminderWorkflow slaReminder,
	AdmissionCrmJourneyWorkflow journey) : ITransientDependency
{
	public Task<AdmissionCrmStaleArchiveRunResultDto> RunStaleArchiveRulesAsync(Guid academicYearId) =>
		staleArchive.RunStaleArchiveRulesFromAutomationAsync(academicYearId);
	public Task<AdmissionCrmSlaReminderRunResultDto> RunSlaReminderRulesAsync(Guid academicYearId) =>
		slaReminder.RunSlaReminderRulesFromAutomationAsync(academicYearId);
	public Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input) => journey.RunAsync(academicYearId, input);
	public Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesForStaffAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input) => journey.RunForStaffAsync(academicYearId, input);
	public Task TrySendAcknowledgementAsync(
		Guid leadId,
		Guid? templateId,
		System.Collections.Generic.Dictionary<string, string>? variables = null) =>
		journey.TrySendAcknowledgementAsync(leadId, templateId, variables);
}