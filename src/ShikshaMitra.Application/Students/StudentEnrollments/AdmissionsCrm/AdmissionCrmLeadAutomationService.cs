using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
public class AdmissionCrmLeadAutomationService(
	AdmissionCrmStaleArchiveWorkflow staleArchive,
	AdmissionCrmSlaReminderWorkflow slaReminder,
	AdmissionCrmJourneyWorkflow journey) : ApplicationService, IAdmissionCrmLeadAutomationService
{
	public Task<AdmissionCrmStaleArchiveRunResultDto> RunStaleArchiveRulesAsync(Guid academicYearId) =>
		staleArchive.RunStaleArchiveRulesAsync(academicYearId);
	public Task<AdmissionCrmSlaReminderRunResultDto> RunSlaReminderRulesAsync(Guid academicYearId) =>
		slaReminder.RunSlaReminderRulesAsync(academicYearId);
	public Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input) => journey.RunForStaffAsync(academicYearId, input);
}