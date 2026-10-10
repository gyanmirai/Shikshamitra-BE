using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Closes admission-owned work atomically while retaining its evidence and financial history.</summary>
[RemoteService(false)]
public class AdmissionJourneyClosureService(
	AdmissionApplicationReadService applicationReader,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionInquiryFollowUp, Guid> followUps,
	IRepository<AdmissionSeatReservation, Guid> reservations,
	IRepository<AdmissionSeatPool, Guid> pools,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	IRepository<AdmissionActivity, Guid> activities,
	StudentRecordAdmissionReturnClosureService returnClosure)
	: ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<AdmissionApplicationDto> EndAsync(Guid id, EndAdmissionJourneyDto input)
	{
		var application = await applicationReader.GetEntityAsync(id);
		var reason = input.Reason?.Trim();
		if (string.IsNullOrWhiteSpace(reason) || reason.Length > 512)
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionReasonRequired,
				"Enter a reason of up to 512 characters for ending the admission journey.");
		// A successful retry may carry the pre-save stamp. It must not duplicate closure history.
		if (application.Status == AdmissionApplicationStatus.Withdrawn && application.DecisionReason == reason)
			return AdmissionApplicationMapper.ToDto(application);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || application.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(AdmissionApplicationErrorCodes.ConcurrencyConflict);
		var actor = CurrentUser.Id ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var previousStageId = application.CurrentStageInstanceId;
		application.EndJourney(actor, reason);
		var workflow = await workflows.FindAsync(item => item.ApplicationId == id);
		if (workflow != null)
		{
			workflow.Cancel();
			await workflows.UpdateAsync(workflow, false);
			foreach (var stage in await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id &&
				(item.Status == AdmissionRuntimeStageStatus.Active || item.Status == AdmissionRuntimeStageStatus.Pending)))
			{
				stage.Cancel(actor, reason);
				await stages.UpdateAsync(stage, false);
			}
		}
		await ReleaseBookingsAsync(application, reason);
		await ReleaseSeatsAsync(application, actor, reason);
		if (application.InquiryId.HasValue)
			foreach (var followUp in await followUps.GetListAsync(item => item.InquiryId == application.InquiryId &&
				item.Status == AdmissionInquiryFollowUpStatus.Scheduled))
			{
				followUp.Cancel(actor, Clock.Now, reason);
				await followUps.UpdateAsync(followUp, false);
			}
		await returnClosure.CloseWithdrawnApplicationAsync(application, reason);
		await applications.UpdateAsync(application, false);
		await history.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
			id, AdmissionApplicationStatus.Withdrawn, reason, actor, "journey-ended", previousStageId), false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"AdmissionJourneyEnded", "Admission journey ended", application.ApplicantId, id, application.InquiryId,
			JsonSerializer.Serialize(new { Reason = reason, ActorUserId = actor, PreviousStageInstanceId = previousStageId })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return AdmissionApplicationMapper.ToDto(application);
	}

	private async Task ReleaseBookingsAsync(AdmissionApplication application, string reason)
	{
		// Completed or started assessment evidence remains intact. Only unused reservations are released.
		var pending = await attempts.GetListAsync(item => item.ApplicationId == application.Id &&
			item.Status == "pending" && item.Attendance == "pending");
		var sessionIds = pending.Where(item => item.SessionId.HasValue).Select(item => item.SessionId!.Value).Distinct().ToArray();
		var bookedSessions = (await sessions.GetListAsync(item => Enumerable.Contains(sessionIds, item.Id))).ToDictionary(item => item.Id);
		foreach (var attempt in pending)
		{
			attempt.Cancel(reason);
			await attempts.UpdateAsync(attempt, false);
			if (attempt.SessionId is Guid sessionId && bookedSessions.TryGetValue(sessionId, out var session)) session.Release();
		}
		foreach (var session in bookedSessions.Values) await sessions.UpdateAsync(session, false);
	}

	private async Task ReleaseSeatsAsync(AdmissionApplication application, Guid actor, string reason)
	{
		var held = await reservations.GetListAsync(item => item.ApplicationId == application.Id &&
			(item.Status == AdmissionSeatReservationStatus.Reserved || item.Status == AdmissionSeatReservationStatus.Confirmed ||
			 item.Status == AdmissionSeatReservationStatus.Waitlisted));
		var poolIds = held.Select(item => item.PoolId).Distinct().ToArray();
		var seatPools = (await pools.GetListAsync(item => Enumerable.Contains(poolIds, item.Id))).ToDictionary(item => item.Id);
		foreach (var reservation in held)
		{
			seatPools[reservation.PoolId].Release(reservation.Status);
			reservation.Release(Clock.Now, actor, reason);
			await reservations.UpdateAsync(reservation, false);
		}
		foreach (var pool in seatPools.Values) await pools.UpdateAsync(pool, false);
	}
}
