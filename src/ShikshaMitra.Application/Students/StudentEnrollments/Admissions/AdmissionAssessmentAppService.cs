using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Uow;
using Volo.Abp.Authorization;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionAssessmentAppService(AdmissionAssessmentSessionService sessions,
	AdmissionAssessmentReadService reader, AdmissionAssessmentCommandService commands, AdmissionAssessmentRosterService roster)
	: ApplicationService, IAdmissionAssessmentAppService
{
	[Authorize(Permissions.ManageSessions)]
	public Task<AdmissionAssessmentSessionResourcesDto> GetSessionResourcesAsync() => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().GetResourcesAsync();
	[Authorize(Permissions.ManageSessions)]
	public Task<List<AdmissionAssessmentStaffDto>> GetInterviewersAsync(string? search) => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().GetInterviewersAsync(search);
	[Authorize(Permissions.View)]
	public Task<PagedResultDto<AdmissionAssessmentSessionDto>> GetSessionWorkbenchAsync(AdmissionAssessmentSessionListInput input) => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().GetListAsync(input);
	[Authorize(Permissions.View)]
	public Task<AdmissionAssessmentSessionDto> GetSessionAsync(Guid id) => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().GetAsync(id);
	[Authorize(Permissions.Book)]
	public Task<List<AdmissionAssessmentInterviewSlotDto>> GetSessionSlotsAsync(Guid id) => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().GetSlotsAsync(id);
	[Authorize(Permissions.ManageSessions)]
	public Task<AdmissionAssessmentSessionPreviewDto> PreviewSessionAsync(AdmissionAssessmentSessionPreviewInput input) => LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().PreviewAsync(input);
	[Authorize(Permissions.ManageSessions), UnitOfWork(true)]
	public async Task<AdmissionAssessmentSessionConfirmationDto> ConfirmSessionAsync(AdmissionAssessmentSessionConfirmInput input)
	{
		if (input.Bookings.Count > 0) await AuthorizationService.CheckAsync(Permissions.Book);
		return await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().ConfirmAsync(input);
	}
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
	[Authorize(Permissions.ManageSessions), UnitOfWork(true)]
	public async Task<AdmissionAssessmentSessionDto> CancelSessionAsync(Guid id, AdmissionAssessmentActionInput input)
	{
		if (input.CancelPendingBookings) await AuthorizationService.CheckAsync(Permissions.Book);
		return await sessions.CancelAsync(id, input);
	}
	[Authorize(Permissions.View)]
	public Task<AdmissionAssessmentDto> GetAsync(Guid applicationId, Guid stageInstanceId) => reader.GetAsync(applicationId, stageInstanceId);
	[Authorize(Permissions.Book), UnitOfWork(true)]
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
