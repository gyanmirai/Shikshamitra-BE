using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentCommandService(
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionAssessmentAccessService access,
	AdmissionAssessmentReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionAssessmentDto> BookAsync(Guid applicationId, Guid stageId, AdmissionAssessmentBookingInput input)
	{
		await LazyServiceProvider.LazyGetRequiredService<IAdmissionAssessmentResourceLock>().AcquireAsync(CurrentTenant.Id, [applicationId, input.SessionId.GetValueOrDefault()]);
		var context = await access.GetContextAsync(applicationId, stageId, requireActive: false, requirePreparation: true);
		AdmissionAssessmentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var rows = await attempts.GetListAsync(item => item.StageInstanceId == stageId);
		var current = rows.Where(item => item.Status != "cancelled").OrderByDescending(item => item.AttemptNumber).FirstOrDefault();
		if (rows.Count(item => item.Status != "cancelled") >= context.Policy!.MaxAttempts || current != null && current.Outcome != "retake")
			throw Invalid("Complete or request a retake of the current attempt before creating another; respect the configured attempt limit.");
		if (context.Policy.SchedulingEnabled != input.SessionId.HasValue)
			throw Invalid(context.Policy.SchedulingEnabled ? "Choose an assessment session." : "This assessment records results without a session.");
		if (input.SessionId.HasValue)
		{
			var session = await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSessionService>().GetAsync(input.SessionId.Value, false);
			if (!session.Supports(context.Application.AdmissionOfferingId, context.Definition.Id))
				throw Invalid("This session belongs to a different offering or assessment stage.");
			await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnsureSessionReservationAsync(session);
			DateTime? slotStart = null;
			if (session.SessionType == "individualInterview")
			{
				var reserved = await attempts.GetListAsync(item => item.SessionId == session.Id && item.Status != "cancelled" && item.Outcome != "waived");
				var slots = session.GetInterviewSlots().Where(slot => slot.StartsAt > DateTime.UtcNow && reserved.All(item => item.SlotStartsAt != slot.StartsAt)).ToList();
				slotStart = input.SlotStartsAt?.UtcDateTime;
				if (!slotStart.HasValue || slots.All(slot => slot.StartsAt != slotStart)) throw Invalid("Choose an available interview slot.");
			}
			else if (input.SlotStartsAt.HasValue) throw Invalid("Only individual interviews accept a time slot.");
			await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnsureApplicantAvailableAsync(context, session, slotStart);
			session.Reserve(DateTime.UtcNow, slotStart);
			await sessions.UpdateAsync(session, false);
			input.SlotStartsAt = slotStart.HasValue ? AdmissionAssessmentSessionService.ToUtcOffset(slotStart.Value) : null;
		}
		var attempt = NewAttempt(context, input.SessionId, rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1);
		if (input.SlotStartsAt.HasValue)
		{
			var session = await sessions.GetAsync(input.SessionId!.Value);
			attempt.AssignInterviewSlot(input.SlotStartsAt.Value.UtcDateTime, input.SlotStartsAt.Value.UtcDateTime.AddMinutes(session.SlotDurationMinutes!.Value));
		}
		await attempts.InsertAsync(attempt, false);
		return await SaveAsync(context, attempt, "AssessmentAttemptCreated");
	}

	public Task<AdmissionAssessmentDto> RecordAttendanceAsync(Guid id, AdmissionAssessmentAttendanceInput input) =>
		MutateAsync(id, input, "AssessmentAttendanceRecorded", async (context, attempt) =>
		{
			await EnsureSessionStartedAsync(attempt);
			attempt.RecordAttendance(input.Attendance, input.Notes, Actor(), context.Policy!);
		});

	public Task<AdmissionAssessmentDto> RecordResultAsync(Guid id, AdmissionAssessmentResultInput input) =>
		MutateAsync(id, input, "AssessmentResultRecorded", async (context, attempt) =>
		{
			await EnsureSessionStartedAsync(attempt);
			if (context.Policy!.ScoringMode == "subjects") attempt.RecordSubjectResult(input.SubjectScores, input.Notes, Actor(), context.Policy);
			else
			{
				if (input.SubjectScores?.Count > 0) throw Invalid("This assessment uses one total result, without subject marks.");
				attempt.RecordResult(input.Marks, input.Outcome, input.Notes, Actor(), context.Policy);
			}
		});

	public Task<AdmissionAssessmentDto> SaveResultDraftAsync(Guid id, AdmissionAssessmentResultInput input) =>
		MutateAsync(id, input, "AssessmentResultDraftSaved", async (context, attempt) =>
		{
			await EnsureSessionStartedAsync(attempt);
			attempt.SaveResultDraft(input.SubjectScores, input.Notes, context.Policy!);
		});

	public Task<AdmissionAssessmentDto> ReviewAsync(Guid id, AdmissionAssessmentReviewInput input) =>
		MutateAsync(id, input, "AssessmentResultReviewed", (context, attempt) =>
		{
			if (context.Policy?.ReviewRequired != true) throw Invalid("Assessment result review is not enabled. Record the result directly.");
			attempt.Review(input.Approved, input.Notes, Actor(), DateTime.UtcNow);
			return Task.CompletedTask;
		});

	public Task<AdmissionAssessmentDto> RetakeAsync(Guid id, AdmissionAssessmentActionInput input) =>
		MutateAsync(id, input, "AssessmentRetakeRequested", async (context, attempt) =>
		{
			if (await attempts.CountAsync(item => item.StageInstanceId == attempt.StageInstanceId && item.Status != "cancelled") >= context.Policy!.MaxAttempts)
				throw Invalid("The configured assessment attempt limit has been reached.");
			attempt.RequestRetake(input.Notes);
		});

	public Task<AdmissionAssessmentDto> CancelAsync(Guid id, AdmissionAssessmentActionInput input) =>
		MutateAsync(id, input, "AssessmentAttemptCancelled", async (_, attempt) =>
		{
			attempt.Cancel(input.Notes);
			await ReleaseSessionAsync(attempt);
		});

	public async Task<AdmissionAssessmentDto> WaiveAsync(Guid applicationId, Guid stageId, AdmissionAssessmentWaiverInput input)
	{
		var context = await access.GetContextAsync(applicationId, stageId, requireActive: false, requirePreparation: true);
		AdmissionAssessmentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var rows = await attempts.GetListAsync(item => item.StageInstanceId == stageId);
		var attempt = rows.Where(item => item.Status != "cancelled").OrderByDescending(item => item.AttemptNumber).FirstOrDefault();
		if (attempt == null)
		{
			attempt = NewAttempt(context, null, rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1);
			attempt.Waive(input.Reason, Actor(), DateTime.UtcNow);
			await attempts.InsertAsync(attempt, false);
		}
		else
		{
			if (attempt.Outcome == "waived") throw Invalid("This assessment is already waived.");
			if (attempt.Status == "pending" && attempt.Attendance == "pending") await ReleaseSessionAsync(attempt);
			attempt.Waive(input.Reason, Actor(), DateTime.UtcNow);
			await attempts.UpdateAsync(attempt, false);
		}
		return await SaveAsync(context, attempt, "AssessmentWaived");
	}

	private async Task<AdmissionAssessmentDto> MutateAsync(Guid id, AdmissionAssessmentActionInput input, string action,
		Func<AdmissionAssessmentContext, AdmissionAssessmentAttempt, Task> change)
	{
		var attempt = await attempts.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionAssessmentAttempt), id);
		var context = await access.GetContextAsync(attempt.ApplicationId, attempt.StageInstanceId, requireActive: false, requirePreparation: true);
		context = context with { Policy = AdmissionAssessmentAccessService.ReadAttemptPolicy(attempt, context.Policy) };
		AdmissionAssessmentAccessService.EnsureConcurrency(attempt, input.ConcurrencyStamp);
		if (await attempts.AnyAsync(item => item.StageInstanceId == attempt.StageInstanceId && item.Status != "cancelled" && item.AttemptNumber > attempt.AttemptNumber))
			throw Invalid("Only the latest assessment attempt can be changed.");
		await change(context, attempt);
		await attempts.UpdateAsync(attempt, false);
		return await SaveAsync(context, attempt, action);
	}

	private async Task<AdmissionAssessmentDto> SaveAsync(AdmissionAssessmentContext context, AdmissionAssessmentAttempt attempt, string action)
	{
		await applications.UpdateAsync(context.Application, false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, action,
			$"{context.Stage.Name}: attempt {attempt.AttemptNumber}, {attempt.Outcome}.", context.Application.ApplicantId,
			context.Application.Id, metadataJson: JsonSerializer.Serialize(new
			{
				ActorUserId = Actor(), AttemptId = attempt.Id, attempt.StageInstanceId, attempt.Status, attempt.Outcome, attempt.Attendance,
				attempt.Marks, SubjectScores = attempt.GetSubjectScores(), attempt.RecordedBy, attempt.ReviewedBy, attempt.Notes, attempt.ReviewNotes
			})), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(context.Application.Id, context.Stage.Id);
	}
	private AdmissionAssessmentAttempt NewAttempt(AdmissionAssessmentContext context, Guid? sessionId, int number) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, context.Application.Id, context.Stage.Id, sessionId, number,
			JsonSerializer.Serialize(new { assessment = context.Policy?.EffectiveProfile is { } profile ? context.Policy with { ClassProfiles = [profile] } : context.Policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
	private async Task EnsureSessionStartedAsync(AdmissionAssessmentAttempt attempt)
	{
		if (!attempt.SessionId.HasValue) return;
		var session = await sessions.GetAsync(attempt.SessionId.Value);
		if (session.IsCancelled || (attempt.SlotStartsAt ?? session.StartsAt) > DateTime.UtcNow || !await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().IsReservationApprovedAsync(session))
			throw Invalid("The assessment appointment has not started or its room reservation is not approved.");
	}
	private async Task ReleaseSessionAsync(AdmissionAssessmentAttempt attempt)
	{
		if (!attempt.SessionId.HasValue) return;
		var session = await sessions.GetAsync(attempt.SessionId.Value);
		session.Release();
		await sessions.UpdateAsync(session, false);
	}
	private Guid Actor() => CurrentUser.Id ?? throw Invalid("An authenticated staff member is required.");
	private static BusinessException Invalid(string message) => new(AdmissionAssessmentErrorCodes.InvalidState, message);
}
