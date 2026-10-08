using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionAssessmentAppService(AdmissionAssessmentSessionService sessions,
	AdmissionAssessmentReadService reader, AdmissionAssessmentCommandService commands, AdmissionAssessmentRosterService roster)
	: ApplicationService, IAdmissionAssessmentAppService
{
	[Authorize(Permissions.View)]
	public Task<List<AdmissionAssessmentScopeDto>> GetScopesAsync() => sessions.GetScopesAsync();
	[Authorize(Permissions.View)]
	public Task<AdmissionAssessmentRosterDto> GetApplicantsAsync(AdmissionAssessmentRosterInput input) => roster.GetAsync(input);
	[Authorize(Permissions.View)]
	public Task<List<AdmissionAssessmentSessionDto>> GetSessionsAsync(Guid admissionOfferingId, Guid workflowStageDefinitionId) =>
		sessions.GetListAsync(admissionOfferingId, workflowStageDefinitionId);
	[Authorize(Permissions.ManageSessions)]
	public Task<AdmissionAssessmentSessionDto> CreateSessionAsync(AdmissionAssessmentSessionInput input) => sessions.CreateAsync(input);
	[Authorize(Permissions.ManageSessions)]
	public Task<AdmissionAssessmentSessionDto> UpdateSessionAsync(Guid id, AdmissionAssessmentSessionInput input) => sessions.UpdateAsync(id, input);
	[Authorize(Permissions.ManageSessions)]
	public Task<AdmissionAssessmentSessionDto> CancelSessionAsync(Guid id, AdmissionAssessmentActionInput input) => sessions.CancelAsync(id, input);
	[Authorize(Permissions.View)]
	public Task<AdmissionAssessmentDto> GetAsync(Guid applicationId, Guid stageInstanceId) => reader.GetAsync(applicationId, stageInstanceId);
	[Authorize(Permissions.Book)]
	public Task<AdmissionAssessmentDto> BookAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentBookingInput input) => commands.BookAsync(applicationId, stageInstanceId, input);
	[Authorize(Permissions.RecordAttendance)]
	public Task<AdmissionAssessmentDto> RecordAttendanceAsync(Guid attemptId, AdmissionAssessmentAttendanceInput input) => commands.RecordAttendanceAsync(attemptId, input);
	[Authorize(Permissions.RecordResults)]
	public Task<AdmissionAssessmentDto> RecordResultAsync(Guid attemptId, AdmissionAssessmentResultInput input) => commands.RecordResultAsync(attemptId, input);
	[Authorize(Permissions.Review)]
	public Task<AdmissionAssessmentDto> ReviewAsync(Guid attemptId, AdmissionAssessmentReviewInput input) => commands.ReviewAsync(attemptId, input);
	[Authorize(Permissions.Book)]
	public Task<AdmissionAssessmentDto> RetakeAsync(Guid attemptId, AdmissionAssessmentActionInput input) => commands.RetakeAsync(attemptId, input);
	[Authorize(Permissions.Book)]
	public Task<AdmissionAssessmentDto> CancelAttemptAsync(Guid attemptId, AdmissionAssessmentActionInput input) => commands.CancelAsync(attemptId, input);
	[Authorize(Permissions.Waive)]
	public Task<AdmissionAssessmentDto> WaiveAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentWaiverInput input) => commands.WaiveAsync(applicationId, stageInstanceId, input);
}
