using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentReadService(
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions,
	IPermissionChecker permissions,
	AdmissionWorkflowRuntimeValueService runtimeValues,
	AdmissionWorkflowTransitionSelector selector,
	AdmissionAssessmentAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionAssessmentDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId, false);
		var rows = (await attempts.GetListAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId))
			.OrderBy(item => item.AttemptNumber).ToList();
		var latest = rows.LastOrDefault(item => item.Status != "cancelled");
		var sessionIds = rows.Where(item => item.SessionId.HasValue).Select(item => item.SessionId!.Value).ToList();
		var sessionRows = (await sessions.GetListAsync(item => sessionIds.Contains(item.Id))).ToDictionary(item => item.Id);
		var blockers = context.Policy == null ? new List<string> { "This stage uses the existing manual assessment process." } :
			AdmissionAssessmentEvidence.GetBlockers(latest);
		if (!context.IsActive) blockers.Add("This assessment stage is not active.");
		if (latest?.Status == "completed" && context.IsActive)
		{
			var routes = await transitions.GetListAsync(item => item.FromStageId == context.Definition.Id);
			var facts = await runtimeValues.ReadAsync(context.Application, stageId);
			try { AdmissionAssessmentEvidence.EnsureRoute(latest, context.Definition.IsTerminalStage ? null : selector.Select(routes, facts), facts); }
			catch (BusinessException exception)
			{
				blockers.Add(exception.Code == AdmissionAssessmentErrorCodes.RouteRequired ? exception.Message :
					"This assessment result needs one matching route before the application can continue.");
			}
		}
		var active = context.IsActive && context.Policy != null;
		var pending = active && latest?.Status == "pending";
		var currentSession = latest?.SessionId is Guid sessionId ? sessionRows.GetValueOrDefault(sessionId) : null;
		var sessionStarted = currentSession == null || !currentSession.IsCancelled && currentSession.StartsAt <= DateTime.UtcNow;
		if (pending && !sessionStarted) blockers.Add("Attendance and results become available when the session starts.");
		if (latest?.Status == "pendingReview" && latest.RecordedBy == CurrentUser.Id)
			blockers.Add("A different authorized staff member must review your recorded result.");
		var remaining = context.Policy != null && rows.Count(item => item.Status != "cancelled") < context.Policy.MaxAttempts;
		return new AdmissionAssessmentDto
		{
			ApplicationId = applicationId, StageInstanceId = stageId, AssessmentEnabled = context.Policy != null, Policy = context.Policy,
			ConcurrencyStamp = context.Application.ConcurrencyStamp, Outcome = latest?.Outcome, Blockers = blockers,
			CanComplete = active && blockers.Count == 0 && await permissions.IsGrantedAsync(
				ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance),
			CanBook = active && remaining && (latest == null || latest.Outcome == "retake") && await permissions.IsGrantedAsync(Permissions.Book),
			CanRecordAttendance = pending && sessionStarted && await permissions.IsGrantedAsync(Permissions.RecordAttendance),
			CanRecordResult = pending && sessionStarted && (!context.Policy!.AttendanceRequired || latest!.Attendance == "present") && await permissions.IsGrantedAsync(Permissions.RecordResults),
			CanReview = active && latest?.Status == "pendingReview" && latest.RecordedBy != CurrentUser.Id && await permissions.IsGrantedAsync(Permissions.Review),
			CanRetake = active && remaining && latest?.Status == "completed" && latest.Outcome is "failed" or "absent" && await permissions.IsGrantedAsync(Permissions.Book),
			CanWaive = active && latest?.Outcome != "waived" && await permissions.IsGrantedAsync(Permissions.Waive),
			CanCancel = pending && latest!.Attendance == "pending" && await permissions.IsGrantedAsync(Permissions.Book),
			Attempts = rows.Select(item => MapAttempt(item, item.SessionId.HasValue ? sessionRows.GetValueOrDefault(item.SessionId.Value) : null)).ToList()
		};
	}

	private static AdmissionAssessmentAttemptDto MapAttempt(AdmissionAssessmentAttempt item, AdmissionAssessmentSession? session) => new()
	{
		Id = item.Id, AttemptNumber = item.AttemptNumber, SessionId = item.SessionId, SessionName = session?.Name,
		SessionStartsAt = session?.StartsAt, SessionEndsAt = session?.EndsAt, SessionLocation = session?.Location,
		Attendance = item.Attendance, Status = item.Status, Outcome = item.Outcome, Marks = item.Marks, Notes = item.Notes,
		ReviewNotes = item.ReviewNotes, RecordedBy = item.RecordedBy, ReviewedBy = item.ReviewedBy, ReviewedAt = item.ReviewedAt,
		CreatedAt = item.CreationTime, ConcurrencyStamp = item.ConcurrencyStamp
	};
}
