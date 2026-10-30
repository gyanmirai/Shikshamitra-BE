using System;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionJourneyResumeService(
	AdmissionApplicationReadService reader,
	AdmissionJourneyResumePlanner planner,
	AdmissionJourneyResumeResources resources,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	IRepository<AdmissionActivity, Guid> activities) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<AdmissionJourneyResumePreviewDto> PreviewAsync(Guid id) =>
		(await planner.BuildAsync(await reader.GetEntityAsync(id))).Preview;

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<AdmissionApplicationDto> ResumeAsync(Guid id, ResumeAdmissionJourneyDto input)
	{
		var reason = input.Reason?.Trim();
		var key = input.IdempotencyKey?.Trim();
		if (string.IsNullOrWhiteSpace(reason) || reason.Length > 512 || string.IsNullOrWhiteSpace(key) || key.Length > 80 || input.ClosureHistoryId == Guid.Empty)
			throw new BusinessException(AdmissionJourneyResumeErrorCodes.Blocked, "Enter a reason of up to 512 characters and refresh the resume preview.")
				.WithData("Reason", "Enter a reason of up to 512 characters and refresh the resume preview.");
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions
		{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		try
		{
			var application = await reader.GetEntityAsync(id);
			var actor = CurrentUser.Id ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
			var decisions = await history.GetListAsync(x => x.ApplicationId == id);
			var closure = decisions.Where(x => x.Action == "journey-ended").OrderByDescending(x => x.OccurredAt).FirstOrDefault();
			if (closure?.Id != input.ClosureHistoryId) throw Conflict();
			var previous = decisions.SingleOrDefault(x => x.Action == "journey-resumed" && x.IdempotencyKey == key);
			var fingerprint = JsonSerializer.Serialize(new { ClosureHistoryId = input.ClosureHistoryId, Reason = reason });
			if (previous != null)
			{
				if (previous.MetadataJson != fingerprint || application.Status == AdmissionApplicationStatus.Withdrawn) throw Conflict();
				await work.CompleteAsync();
				return await reader.GetAsync(id);
			}
			if (application.ConcurrencyStamp != input.ConcurrencyStamp || string.IsNullOrWhiteSpace(input.ConcurrencyStamp)) throw Conflict();
			var plan = await planner.BuildAsync(application);
			if (!plan.Preview.CanResume) throw new BusinessException(AdmissionJourneyResumeErrorCodes.Blocked, plan.Preview.Blockers[0]);
			application.ResumeJourney(plan.Snapshot!, Clock.Now);
			plan.Workflow!.Resume(plan.Snapshot!);
			foreach (var stage in plan.Stages.Where(x => x.Id == plan.Snapshot!.CurrentStageId || plan.Snapshot!.PendingStageIds.Contains(x.Id)))
			{
				stage.ResumeCancelled(stage.Id == plan.Snapshot!.CurrentStageId);
				await stages.UpdateAsync(stage, false);
			}
			await workflows.UpdateAsync(plan.Workflow, false);
			await applications.UpdateAsync(application, false);
			// Owner operations participate in this transaction; any seat failure rolls back all restoration.
			await resources.RestoreAsync(application, plan, closure.Id, reason);
			// Deterministic ID provides database-enforced one-resume-per-closure, including null terminal stage.
			var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"admission-resume:{closure.Id:N}"))[..16]);
			await history.InsertAsync(new AdmissionApplicationDecisionHistory(eventId, CurrentTenant.Id, id,
				application.Status, reason, actor, "journey-resumed", plan.Snapshot!.CurrentStageId, key, fingerprint), false);
			await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(
				"AdmissionJourneyResumed", "Admission journey resumed", application.ApplicantId, id, application.InquiryId,
				JsonSerializer.Serialize(new { Reason = reason, ClosureHistoryId = closure.Id, StageInstanceId = plan.Snapshot.CurrentStageId })), false);
			await work.CompleteAsync();
			return await reader.GetAsync(id);
		}
		catch (AbpDbConcurrencyException) { throw Conflict(); }
		catch (Exception error) when (IsDatabaseConflict(error)) { throw Conflict(); }
		catch (BusinessException error) { error.WithData("Reason", error.Message); throw; }
	}

	private static bool IsDatabaseConflict(Exception error)
	{
		for (Exception? current = error; current != null; current = current.InnerException)
			if (current is DbException database && database.SqlState is "40001" or "40P01" or "23505") return true;
		return false;
	}

	private static BusinessException Conflict() => new BusinessException(AdmissionJourneyResumeErrorCodes.Conflict,
		"This admission changed. Refresh the case and review the latest resume stage before continuing.")
		.WithData("Reason", "This admission changed. Refresh the case and review the latest resume stage before continuing.");
}
