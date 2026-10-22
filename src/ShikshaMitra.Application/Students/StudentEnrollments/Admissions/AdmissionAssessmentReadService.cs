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
		await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnrichReservationsAsync(sessionRows.Values);
		var routes = latest?.Status == "completed" && context.IsActive ? await transitions.GetListAsync(item => item.FromStageId == context.Definition.Id) : [];
		var facts = latest?.Status == "completed" && context.IsActive ? await runtimeValues.ReadAsync(context.Application, stageId) : new Dictionary<string, string>();
		return Build(context, rows, sessionRows, routes, facts, await GetGrantsAsync(), CurrentUser.Id, DateTime.UtcNow);
	}

	public async Task<HashSet<string>> GetGrantsAsync()
	{
		var result = new HashSet<string>();
		foreach (var permission in new[] { Permissions.Book, Permissions.RecordAttendance, Permissions.RecordResults,
			Permissions.Review, Permissions.Waive, ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance }.Distinct())
			if (await permissions.IsGrantedAsync(permission)) result.Add(permission);
		return result;
	}

	/// <summary>Shared single-application and batch projection. Callers must authorize the context before projecting it.</summary>
	public AdmissionAssessmentDto Build(AdmissionAssessmentContext context, IReadOnlyCollection<AdmissionAssessmentAttempt> rows,
		IReadOnlyDictionary<Guid, AdmissionAssessmentSession> sessionRows, IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> routes,
		IReadOnlyDictionary<string, string> facts, IReadOnlySet<string> grants, Guid? userId, DateTime now)
	{
		var latest = rows.Where(item => item.Status != "cancelled").OrderBy(item => item.AttemptNumber).LastOrDefault();
		context = context with { Policy = AdmissionAssessmentAccessService.ReadAttemptPolicy(latest, context.Policy) };
		var blockers = GetBlockers(context, latest, routes, facts);
		var active = context.IsActive && context.Policy != null;
		var editable = context.CanPrepare && context.Policy != null;
		var pending = editable && latest?.Status == "pending";
		var currentSession = latest?.SessionId is Guid sessionId ? sessionRows.GetValueOrDefault(sessionId) : null;
		var sessionStarted = currentSession == null || !currentSession.IsCancelled && currentSession.RoomReady && (latest!.SlotStartsAt ?? currentSession.StartsAt) <= now;
		if (currentSession != null && !currentSession.RoomReady) blockers.Add("The room reservation must be approved before conducting this assessment.");
		if (pending && !sessionStarted) blockers.Add("Attendance and results become available when the session starts.");
		if (latest?.Status == "pendingReview" && latest.RecordedBy == userId)
			blockers.Add("A different authorized staff member must review your recorded result.");
		var remaining = context.Policy != null && rows.Count(item => item.Status != "cancelled") < context.Policy.MaxAttempts;
		return new AdmissionAssessmentDto
		{
			ApplicationId = context.Application.Id, StageInstanceId = context.Stage.Id, AssessmentEnabled = context.Policy != null, Policy = context.Policy, EffectiveProfile = context.Policy?.EffectiveProfile,
			ConcurrencyStamp = context.Application.ConcurrencyStamp, Outcome = latest?.Outcome, Blockers = blockers,
			CanComplete = active && blockers.Count == 0 && grants.Contains(
				ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance),
			CanBook = editable && remaining && (latest == null || latest.Outcome == "retake") && grants.Contains(Permissions.Book),
			CanRecordAttendance = pending && sessionStarted && grants.Contains(Permissions.RecordAttendance),
			CanRecordResult = pending && sessionStarted && (!context.Policy!.AttendanceRequired || latest!.Attendance == "present") && grants.Contains(Permissions.RecordResults),
			CanSaveResultDraft = pending && sessionStarted && context.Policy!.ScoringMode == "subjects" && (!context.Policy.AttendanceRequired || latest!.Attendance == "present") && grants.Contains(Permissions.RecordResults),
			CanSubmitResult = pending && sessionStarted && (!context.Policy!.AttendanceRequired || latest!.Attendance == "present") && grants.Contains(Permissions.RecordResults) &&
				(context.Policy.ScoringMode != "subjects" || latest!.HasCompleteSubjectScores(context.Policy)),
			CanReview = editable && latest?.Status == "pendingReview" && latest.RecordedBy != userId && grants.Contains(Permissions.Review),
			CanRetake = editable && remaining && latest?.Status == "completed" && latest.Outcome is "failed" or "absent" && grants.Contains(Permissions.Book),
			CanWaive = editable && latest?.Outcome != "waived" && grants.Contains(Permissions.Waive),
			CanCancel = pending && latest!.Attendance == "pending" && !latest.HasEnteredSubjectMarks() && grants.Contains(Permissions.Book),
			Attempts = rows.OrderBy(item => item.AttemptNumber).Select(item => MapAttempt(item, item.SessionId.HasValue ? sessionRows.GetValueOrDefault(item.SessionId.Value) : null)).ToList()
		};
	}

	/// <summary>Evidence and configured route readiness, without allocating history/action DTOs for off-page candidates.</summary>
	public List<string> GetBlockers(AdmissionAssessmentContext context, AdmissionAssessmentAttempt? latest,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> routes, IReadOnlyDictionary<string, string> facts)
	{
		var blockers = context.Policy == null ? new List<string> { "This stage uses the existing manual assessment process." } :
			AdmissionAssessmentEvidence.GetBlockers(latest);
		if (!context.CanPrepare && !context.IsActive) blockers.Add("Assessment history is read-only for this stage.");
		if (latest?.Status == "completed" && context.IsActive)
		{
			try { AdmissionAssessmentEvidence.EnsureRoute(latest, context.Definition.IsTerminalStage ? null : selector.Select(routes, facts), facts); }
			catch (BusinessException exception)
			{
				blockers.Add(exception.Code == AdmissionAssessmentErrorCodes.RouteRequired ? exception.Message :
					"This assessment result needs one matching route before the application can continue.");
			}
		}
		return blockers;
	}

	private static AdmissionAssessmentAttemptDto MapAttempt(AdmissionAssessmentAttempt item, AdmissionAssessmentSession? session) => new()
	{
		Id = item.Id, AttemptNumber = item.AttemptNumber, SessionId = item.SessionId, SessionName = session?.Name,
		EffectiveProfile = AdmissionAssessmentAccessService.ReadAttemptPolicy(item, null)?.EffectiveProfile, SubjectScores = item.GetSubjectScores().ToList(),
		SlotStartsAt = item.SlotStartsAt.HasValue ? AdmissionAssessmentSessionService.ToUtcOffset(item.SlotStartsAt.Value) : null,
		SlotEndsAt = item.SlotEndsAt.HasValue ? AdmissionAssessmentSessionService.ToUtcOffset(item.SlotEndsAt.Value) : null,
		SessionStartsAt = session == null ? null : AdmissionAssessmentSessionService.ToUtcOffset(session.StartsAt),
		SessionEndsAt = session == null ? null : AdmissionAssessmentSessionService.ToUtcOffset(session.EndsAt), SessionLocation = session?.Location,
		Attendance = item.Attendance, Status = item.Status, Outcome = item.Outcome, Marks = item.Marks, Notes = item.Notes,
		ReviewNotes = item.ReviewNotes, RecordedBy = item.RecordedBy, ReviewedBy = item.ReviewedBy, ReviewedAt = item.ReviewedAt,
		CreatedAt = item.CreationTime, ConcurrencyStamp = item.ConcurrencyStamp
	};
}
