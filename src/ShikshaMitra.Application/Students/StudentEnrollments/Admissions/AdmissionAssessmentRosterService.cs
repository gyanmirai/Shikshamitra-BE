using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Scoped workbench projection, with batched evidence and route evaluation and no applicant-by-applicant reads.</summary>
[RemoteService(false)]
public class AdmissionAssessmentRosterService(
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowDefinitions,
	IRepository<AdmissionCycle, Guid> cycles,
	AdmissionWorkflowRuntimeValueService runtimeValues,
	AdmissionAssessmentAccessService access,
	AdmissionAssessmentReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionAssessmentRosterDto> GetAsync(AdmissionAssessmentRosterInput input)
	{
		if (input.SkipCount < 0 ||
			input.MaxResultCount < 1 || input.MaxResultCount > 1000 || input.Search?.Length > 128 ||
			input.Status != null && input.Status is not ("pending" or "ongoing" or "complete") ||
			input.Queue != null && !QueueNames.Contains(input.Queue))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a valid assessment scope, queue and page size.");
		if (input.AdmissionOfferingId != Guid.Empty) await access.GetOfferingAsync(input.AdmissionOfferingId);
		if (input.AdmissionOfferingId != Guid.Empty && input.WorkflowStageDefinitionId != Guid.Empty)
			await access.GetManagedDefinitionAsync(input.AdmissionOfferingId, input.WorkflowStageDefinitionId);
		if (input.SessionId.HasValue) await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSessionService>().EnsureAccessibleAsync(input.SessionId.Value);

		var applicationQuery = await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationScopeAccessService>().GetScopedApplicationsAsync();
		var applicantQuery = await applicants.GetQueryableAsync();
		var workflowQuery = await workflows.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var attemptQuery = await attempts.GetQueryableAsync();
		var definitionQuery = await definitions.GetQueryableAsync();
		var offeringQuery = await access.GetScopedOfferingsAsync();
		var selectedStandards = AdmissionAssessmentFilterValues.Standards(input.StandardIds);
		var cycleQuery = await cycles.GetQueryableAsync();
		var standardQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.TenantSettings.Standards.Standard, Guid>>().GetQueryableAsync();
		var activeStatuses = new[] { AdmissionApplicationStatus.Draft, AdmissionApplicationStatus.Submitted, AdmissionApplicationStatus.InReview,
			AdmissionApplicationStatus.Approved, AdmissionApplicationStatus.Waitlisted, AdmissionApplicationStatus.Offered, AdmissionApplicationStatus.OfferAccepted };
		var query = from application in applicationQuery
			join offering in offeringQuery on application.AdmissionOfferingId equals offering.Id
			join standard in standardQuery on offering.StandardId equals standard.Id into linkedStandards
			from standard in linkedStandards.DefaultIfEmpty()
			join cycle in cycleQuery on offering.AdmissionCycleId equals cycle.Id
			join workflow in workflowQuery on application.Id equals workflow.ApplicationId
			join stage in stageQuery on workflow.Id equals stage.WorkflowInstanceId
			join definition in definitionQuery on stage.DefinitionStageId equals definition.Id
			join applicant in applicantQuery on application.ApplicantId equals applicant.Id into linkedApplicants
			from applicant in linkedApplicants.DefaultIfEmpty()
			where (input.AdmissionOfferingId == Guid.Empty || application.AdmissionOfferingId == input.AdmissionOfferingId) &&
				(input.WorkflowStageDefinitionId == Guid.Empty || definition.Id == input.WorkflowStageDefinitionId) &&
				(!input.BoardId.HasValue || offering.BoardId == input.BoardId) && (!input.MediumId.HasValue || offering.MediumId == input.MediumId) &&
				(!input.StandardId.HasValue || offering.StandardId == input.StandardId) && (!input.AcademicYearId.HasValue || cycle.AcademicYearId == input.AcademicYearId) &&
				(selectedStandards.Length == 0 || Enumerable.Contains(selectedStandards, offering.StandardId)) &&
				application.WorkflowDefinitionId == definition.WorkflowDefinitionId &&
				workflow.WorkflowDefinitionId == application.WorkflowDefinitionId && stage.DefinitionStageId == definition.Id &&
				stage.StageType == AdmissionWorkflowStageType.Assessment &&
				(stage.Id == application.CurrentStageInstanceId && stage.Status == AdmissionRuntimeStageStatus.Active ||
					stage.Status == AdmissionRuntimeStageStatus.Completed || attemptQuery.Any(attempt => attempt.ApplicationId == application.Id && attempt.StageInstanceId == stage.Id)) &&
				(!input.SessionId.HasValue || attemptQuery.Any(attempt => attempt.ApplicationId == application.Id && attempt.StageInstanceId == stage.Id &&
					attempt.SessionId == input.SessionId && attempt.Status != "cancelled" &&
					(stage.Status != AdmissionRuntimeStageStatus.Active || application.CurrentStageInstanceId != stage.Id ||
						workflow.Status != AdmissionWorkflowInstanceStatus.Active || !Enumerable.Contains(activeStatuses, application.Status) ||
						!attemptQuery.Any(newer => newer.ApplicationId == application.Id &&
						newer.StageInstanceId == stage.Id && newer.Status != "cancelled" && newer.AttemptNumber > attempt.AttemptNumber))))
			select new RosterContext { Application = application, Stage = stage, Definition = definition, OfferingName = offering.Name, ClassName = standard == null ? string.Empty : standard.Name, WorkflowActive = workflow.Status == AdmissionWorkflowInstanceStatus.Active,
				ApplicantName = applicant == null ? "Applicant unavailable" : applicant.FirstName + " " + (applicant.LastName ?? string.Empty) };
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(row => row.ApplicantName.ToLower().Contains(search) || row.Application.ApplicationNumber.ToLower().Contains(search));
		}
		// Exact queue membership needs configured route evaluation. Fetch scoped candidates once, never one query per application.
		var scopedContexts = await AsyncExecuter.ToListAsync(query);
		var policies = scopedContexts.GroupBy(row => row.Definition.Id).ToDictionary(group => group.Key,
			group => AdmissionAssessmentAccessService.ReadPolicy(group.First().Definition));
		var contexts = scopedContexts.Where(row => policies[row.Definition.Id] != null).ToList();
		if (contexts.Count == 0) return new() { Items = [], TotalCount = 0 };
		var applicationIds = contexts.Select(row => row.Application.Id).Distinct().ToArray();
		var stageIds = contexts.Select(row => row.Stage.Id).ToArray();
		var scopedAttempts = attemptQuery.Where(attempt => Enumerable.Contains(applicationIds, attempt.ApplicationId) &&
			Enumerable.Contains(stageIds, attempt.StageInstanceId) && attempt.Status != "cancelled");
		// Classification needs one current attempt and a count, not every candidate's entire history.
		var latestRows = await AsyncExecuter.ToListAsync(scopedAttempts.Where(attempt => !attemptQuery.Any(newer =>
			newer.ApplicationId == attempt.ApplicationId && newer.StageInstanceId == attempt.StageInstanceId &&
			newer.Status != "cancelled" && newer.AttemptNumber > attempt.AttemptNumber)));
		var latestByStage = latestRows.ToDictionary(attempt => attempt.StageInstanceId);
		var countRows = await AsyncExecuter.ToListAsync(scopedAttempts.GroupBy(attempt => attempt.StageInstanceId)
			.Select(group => new { StageId = group.Key, Count = group.Count() }));
		var countsByStage = countRows.ToDictionary(row => row.StageId, row => row.Count);
		var definitionIds = contexts.Select(row => row.Definition.Id).Distinct().ToArray();
		var routes = await transitions.GetListAsync(transition => Enumerable.Contains(definitionIds, transition.FromStageId));
		var workflowIds = contexts.Select(row => row.Definition.WorkflowDefinitionId).Distinct().ToArray();
		var versions = (await workflowDefinitions.GetListAsync(item => Enumerable.Contains(workflowIds, item.Id))).ToDictionary(item => item.Id, item => item.Version);
		var readinessContexts = contexts.Where(row => row.WorkflowActive).Select(row =>
			new AdmissionAssessmentContext(row.Application, row.Stage, row.Definition, policies[row.Definition.Id]) { WorkflowStatus = AdmissionWorkflowInstanceStatus.Active })
			.Where(context => context.IsActive && latestByStage.GetValueOrDefault(context.Stage.Id)?.Status == "completed").ToList();
		// Runtime facts choose by creation time, exactly as the individual workflow read does.
		var readinessIds = readinessContexts.Select(context => context.Stage.Id).ToArray();
		var factRows = readinessIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(scopedAttempts.Where(attempt =>
			Enumerable.Contains(readinessIds, attempt.StageInstanceId) && !attemptQuery.Any(newer =>
				newer.ApplicationId == attempt.ApplicationId && newer.StageInstanceId == attempt.StageInstanceId && newer.Status != "cancelled" &&
				(newer.CreationTime > attempt.CreationTime || newer.CreationTime == attempt.CreationTime && newer.AttemptNumber > attempt.AttemptNumber))));
		var factsByStage = await runtimeValues.ReadAssessmentBatchAsync(readinessContexts, factRows);
		var sortAttempts = latestByStage;
		if (input.SessionId.HasValue)
		{
			var selectedRows = await AsyncExecuter.ToListAsync(scopedAttempts.Where(attempt => attempt.SessionId == input.SessionId &&
				!attemptQuery.Any(newer => newer.ApplicationId == attempt.ApplicationId && newer.StageInstanceId == attempt.StageInstanceId && newer.Status != "cancelled" &&
					newer.SessionId == input.SessionId && newer.AttemptNumber > attempt.AttemptNumber)));
			sortAttempts = selectedRows.ToDictionary(attempt => attempt.StageInstanceId);
		}
		var sessionIds = sortAttempts.Values.Where(attempt => attempt.SessionId.HasValue).Select(attempt => attempt.SessionId!.Value).Distinct().ToArray();
		var sessionRows = (await sessions.GetListAsync(session => Enumerable.Contains(sessionIds, session.Id))).ToDictionary(session => session.Id);
		await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnrichReservationsAsync(sessionRows.Values);
		var classificationNow = DateTime.UtcNow;
		var rows = contexts.Select(row =>
		{
			var policy = policies[row.Definition.Id]!;
			var context = new AdmissionAssessmentContext(row.Application, row.Stage, row.Definition, policy)
				{ WorkflowStatus = row.WorkflowActive ? AdmissionWorkflowInstanceStatus.Active : AdmissionWorkflowInstanceStatus.Cancelled };
			var active = context.IsActive && row.WorkflowActive;
			var latest = latestByStage.GetValueOrDefault(row.Stage.Id);
			var ready = active && latest?.Status == "completed" && reader.GetBlockers(context, latest, routes.Where(route => route.FromStageId == row.Definition.Id).ToList(),
				factsByStage.GetValueOrDefault(row.Stage.Id) ?? []).Count == 0;
			var sortSessionId = sortAttempts.GetValueOrDefault(row.Stage.Id)?.SessionId;
			var appointmentStart = latest?.SlotStartsAt ?? (latest?.SessionId is Guid latestSessionId ? sessionRows.GetValueOrDefault(latestSessionId)?.StartsAt : null);
			return new RosterSummary
			{
				Context = row, Active = active, Status = ClassifyStatus(sortAttempts.GetValueOrDefault(row.Stage.Id),
					sortSessionId.HasValue ? sessionRows.GetValueOrDefault(sortSessionId.Value) : null, classificationNow, policy.ScoringEnabled), Queue = active && latest?.Status == "pending" && sortSessionId.HasValue && sessionRows.GetValueOrDefault(sortSessionId.Value)?.RoomReady == false
					? "attention" : active && latest?.Status == "pending" && appointmentStart > classificationNow ? "attendance" : Classify(active, row.Stage.Status, latest, countsByStage.GetValueOrDefault(row.Stage.Id), policy, ready),
				SessionStartsAt = sortAttempts.GetValueOrDefault(row.Stage.Id)?.SlotStartsAt ?? (sortSessionId.HasValue ? sessionRows.GetValueOrDefault(sortSessionId.Value)?.StartsAt : null)
			};
		}).ToList();
		var counts = CountQueues(rows);
		var statusCounts = new AdmissionAssessmentStatusCountsDto
		{
			All = rows.Count, Pending = rows.Count(row => row.Status == "pending"),
			Ongoing = rows.Count(row => row.Status == "ongoing"), Complete = rows.Count(row => row.Status == "complete")
		};
		var filtered = rows.Where(row => (input.Queue == null || row.Queue == input.Queue) &&
			(input.Status == null || row.Status == input.Status)).ToList();
		var page = Sort(filtered, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
		var pageIds = page.Select(row => row.Context.Stage.Id).ToArray();
		var pageApplicationIds = page.Select(row => row.Context.Application.Id).ToArray();
		var history = pageIds.Length == 0 ? [] : await attempts.GetListAsync(attempt =>
			Enumerable.Contains(pageApplicationIds, attempt.ApplicationId) && Enumerable.Contains(pageIds, attempt.StageInstanceId));
		var historyByStage = history.ToLookup(attempt => attempt.StageInstanceId);
		var missingSessions = history.Where(attempt => attempt.SessionId.HasValue && !sessionRows.ContainsKey(attempt.SessionId.Value))
			.Select(attempt => attempt.SessionId!.Value).Distinct().ToArray();
		if (missingSessions.Length > 0)
			foreach (var session in await sessions.GetListAsync(session => Enumerable.Contains(missingSessions, session.Id))) sessionRows[session.Id] = session;
		await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnrichReservationsAsync(sessionRows.Values);
		var interviewerIds = sessionRows.Values.Where(item => item.InterviewerId.HasValue).Select(item => item.InterviewerId!.Value).Distinct().ToArray();
		var interviewerNames = (await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.HR.Employees.Employee, Guid>>()
			.GetListAsync(item => Enumerable.Contains(interviewerIds, item.Id))).ToDictionary(item => item.Id, item => item.EmployeeName);
		var grants = page.Count == 0 ? [] : await reader.GetGrantsAsync();
		var now = DateTime.UtcNow;
		return new() { Counts = counts, StatusCounts = statusCounts, TotalCount = filtered.Count, Items = page.Select(summary =>
		{
			var row = summary.Context;
			var context = new AdmissionAssessmentContext(row.Application, row.Stage, row.Definition, policies[row.Definition.Id])
				{ WorkflowStatus = row.WorkflowActive ? AdmissionWorkflowInstanceStatus.Active : AdmissionWorkflowInstanceStatus.Cancelled };
			var evidence = reader.Build(context, historyByStage[row.Stage.Id].ToList(), sessionRows, routes.Where(route => route.FromStageId == row.Definition.Id).ToList(),
				factsByStage.GetValueOrDefault(row.Stage.Id) ?? [], grants, CurrentUser.Id, now);
			if (!row.WorkflowActive)
			{
				evidence.Blockers.Add("This application workflow is not active.");
				evidence.CanBook = evidence.CanCancel = evidence.CanComplete = evidence.CanRecordAttendance = evidence.CanRecordResult =
					evidence.CanRetake = evidence.CanReview = evidence.CanWaive = false;
			}
			return new AdmissionAssessmentRosterItemDto { AdmissionOfferingId = row.Application.AdmissionOfferingId, WorkflowStageDefinitionId = row.Definition.Id,
				InterviewerName = historyByStage[row.Stage.Id].Where(item => item.Status != "cancelled").OrderByDescending(item => item.AttemptNumber).Select(item => item.SessionId.HasValue ? sessionRows.GetValueOrDefault(item.SessionId.Value)?.InterviewerId : null).FirstOrDefault() is Guid interviewerId ? interviewerNames.GetValueOrDefault(interviewerId) : null,
				ClassName = row.ClassName, Status = summary.Status, OfferingName = row.OfferingName, WorkflowVersion = versions.GetValueOrDefault(row.Definition.WorkflowDefinitionId), ApplicationId = row.Application.Id, ApplicationNumber = row.Application.ApplicationNumber,
				ApplicantId = row.Application.ApplicantId, ApplicantName = row.ApplicantName.Trim(), StageInstanceId = row.Stage.Id, StageName = row.Stage.Name,
				ApplicationStatus = row.Application.Status, StageStatus = row.Stage.Status, IsActive = summary.Active, EnteredAt = row.Stage.EnteredAt,
				Queue = summary.Queue, Assessment = evidence };
		}).ToList() };
	}

	// Display progress is independent of routing readiness and never advances the workflow.
	internal static string ClassifyStatus(AdmissionAssessmentAttempt? attempt, AdmissionAssessmentSession? session, DateTime now, bool scoringEnabled)
	{
		if (attempt == null || attempt.Status == "cancelled" || attempt.Outcome == "retake") return "pending";
		if (attempt.Outcome == "waived") return "complete";
		var startsAt = attempt.SlotStartsAt ?? session?.StartsAt;
		var endsAt = attempt.SlotEndsAt ?? session?.EndsAt;
		if (startsAt > now) return "pending";
		// Qualitative assessments and absence have a final outcome without numeric marks.
		var recorded = (attempt.Status is "completed" or "pendingReview") &&
			(attempt.Outcome == "absent" || attempt.Outcome is "passed" or "failed" && (!scoringEnabled || attempt.Marks.HasValue));
		return recorded && (!endsAt.HasValue || endsAt <= now) ? "complete" : "ongoing";
	}

	private static string Classify(bool active, AdmissionRuntimeStageStatus stageStatus, AdmissionAssessmentAttempt? latest,
		int attemptCount, AdmissionAssessmentPolicy policy, bool ready)
	{
		if (stageStatus == AdmissionRuntimeStageStatus.Completed) return "completed";
		if (!active) return "attention";
		if (latest == null || latest.Outcome == "retake")
			return attemptCount < policy.MaxAttempts ? "toBook" : "attention";
		if (latest.Status == "pendingReview") return "review";
		if (latest.Status == "completed") return ready ? "ready" : "attention";
		if (latest.Status != "pending") return "attention";
		return policy.AttendanceRequired && latest.Attendance == "pending" ? "attendance" : "result";
	}

	private static readonly HashSet<string> QueueNames = ["toBook", "attendance", "result", "review", "ready", "attention", "completed"];
	private static AdmissionAssessmentQueueCountsDto CountQueues(List<RosterSummary> rows) => new()
	{
		All = rows.Count, ToBook = rows.Count(row => row.Queue == "toBook"), Attendance = rows.Count(row => row.Queue == "attendance"),
		Result = rows.Count(row => row.Queue == "result"), Review = rows.Count(row => row.Queue == "review"), Ready = rows.Count(row => row.Queue == "ready"),
		Attention = rows.Count(row => row.Queue == "attention"), Completed = rows.Count(row => row.Queue == "completed")
	};
	private static IOrderedEnumerable<RosterSummary> Sort(List<RosterSummary> rows, string? sorting)
	{
		var parts = (sorting ?? "enteredAt asc").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		Func<RosterSummary, object?> key = parts.FirstOrDefault()?.ToLowerInvariant() switch
		{
			"applicantname" => row => row.Context.ApplicantName.Trim(), "applicationnumber" => row => row.Context.Application.ApplicationNumber,
			"sessionstartsat" => row => row.SessionStartsAt,
			_ => row => row.Context.Stage.EnteredAt
		};
		return (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(row => row.Context.Application.Id).ThenBy(row => row.Context.Stage.Id);
	}
	private sealed class RosterContext
	{
		public AdmissionApplication Application { get; set; } = null!;
		public AdmissionApplicationStageInstance Stage { get; set; } = null!;
		public AdmissionWorkflowStageDefinition Definition { get; set; } = null!;
		public string ClassName { get; set; } = string.Empty;
		public string OfferingName { get; set; } = string.Empty;
		public bool WorkflowActive { get; set; }
		public string ApplicantName { get; set; } = string.Empty;
	}
	private sealed class RosterSummary
	{
		public RosterContext Context { get; set; } = null!;
		public bool Active { get; set; }
		public string Status { get; set; } = string.Empty;
		public string Queue { get; set; } = string.Empty;
		public DateTime? SessionStartsAt { get; set; }
	}
}
