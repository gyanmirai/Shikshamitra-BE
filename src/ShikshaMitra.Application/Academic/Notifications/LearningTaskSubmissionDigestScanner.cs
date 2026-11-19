using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class HomeworkSubmissionDigestDueSource(LearningTaskSubmissionDigestScanner scanner) : INotificationDueSource, ITransientDependency
{
	public string SourceType => LearningTaskReminderSourceAdapter.HomeworkSourceType;
	public string ScanKey => AcademicNotificationEventKeys.HomeworkSubmitted;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcademicNotificationEventKeys.HomeworkSubmitted });

	public Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken) =>
		scanner.ReadPageAsync(ScanKey, isAssignment: false, request, cancellationToken);
}

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class AssignmentSubmissionDigestDueSource(LearningTaskSubmissionDigestScanner scanner) : INotificationDueSource, ITransientDependency
{
	public string SourceType => LearningTaskReminderSourceAdapter.AssignmentSourceType;
	public string ScanKey => AcademicNotificationEventKeys.AssignmentSubmitted;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcademicNotificationEventKeys.AssignmentSubmitted });

	public Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken) =>
		scanner.ReadPageAsync(ScanKey, isAssignment: true, request, cancellationToken);
}

[RemoteService(false)]
public sealed class LearningTaskSubmissionDigestScanner(ICurrentTenant tenant, IRepository<StudentSubmissionEntity, Guid> submissions,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, IRepository<NotificationRule, Guid> rules,
	IAsyncQueryableExecuter queries, NotificationSchoolTimeZone schoolZone, LearningTaskSubmissionDigestTiming timing) : ITransientDependency
{
	private sealed class SubmittedDivision
	{
		public Guid SourceId { get; init; }
		public Guid ClassId { get; init; }
		public Guid DivisionId { get; init; }
	}

	internal async Task<NotificationDueSourcePage> ReadPageAsync(string eventKey, bool isAssignment,
		NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));

		var enabled = (await rules.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.EventKey == eventKey && x.IsEnabled);
		if (!await queries.AnyAsync(enabled, cancellationToken)) return new([], null);

		// A completed school-local window and the partial day after it fit inside this bounded search.
		var recorded = LearningTaskSubmissionFacts.InWindow(await submissions.GetQueryableAsync(), request.TenantId,
			request.CycleStartedAt.AddDays(-3), request.CycleStartedAt);
		var affected = isAssignment
			? recorded.Where(x => x.AssignmentId.HasValue && !x.HomeworkAssignmentId.HasValue &&
				x.Assignment!.TenantId == request.TenantId && !x.Assignment.IsDeleted &&
				x.Assignment.SubmissionType != SubmissionType.NoSubmissionRequired)
				.Select(x => new SubmittedDivision { SourceId = x.AssignmentId!.Value, ClassId = x.Assignment!.StandardId, DivisionId = x.DivisionId })
			: recorded.Where(x => x.HomeworkAssignmentId.HasValue && !x.AssignmentId.HasValue &&
				x.HomeworkAssignment!.TenantId == request.TenantId && !x.HomeworkAssignment.IsDeleted &&
				x.HomeworkAssignment.SubmissionType != SubmissionType.NoSubmissionRequired)
				.Select(x => new SubmittedDivision { SourceId = x.HomeworkAssignmentId!.Value, ClassId = x.HomeworkAssignment!.StandardId, DivisionId = x.DivisionId });
		if (request.Continuation is { } continuation)
		{
			var (sourceId, divisionId) = LearningTaskReminderDuePager.ReadCursor(continuation);
			affected = affected.Where(x => x.SourceId.CompareTo(sourceId) > 0 ||
				x.SourceId == sourceId && x.DivisionId.CompareTo(divisionId) > 0);
		}

		var scoped = from occurrence in affected.Distinct()
			join division in await divisions.GetQueryableAsync() on occurrence.DivisionId equals division.Id
			join standard in await standards.GetQueryableAsync() on occurrence.ClassId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.TenantId == request.TenantId && division.StandardId == standard.Id &&
				standard.TenantId == request.TenantId && stage.TenantId == request.TenantId &&
				medium.TenantId == request.TenantId && board.TenantId == request.TenantId && board.AcademicYear.TenantId == request.TenantId &&
				enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == board.AcademicYearId) &&
					(!rule.BoardId.HasValue || rule.BoardId == board.Id) && (!rule.MediumId.HasValue || rule.MediumId == medium.Id) &&
					(!rule.ClassId.HasValue || rule.ClassId == standard.Id) && (!rule.DivisionId.HasValue || rule.DivisionId == division.Id))
			orderby occurrence.SourceId, occurrence.DivisionId
			select new { occurrence.SourceId, occurrence.DivisionId, occurrence.ClassId, board.AcademicYearId,
				medium.EducationBoardId, stage.EducationMediumId };

		var page = await queries.ToListAsync(scoped.Take(request.PageSize + 1), cancellationToken);
		if (page.Count == 0) return new([], null);
		var zone = await schoolZone.GetAsync(cancellationToken);
		var windows = new Dictionary<NotificationSourceScopeDto, LearningTaskDigestWindow?>();
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in page.Take(request.PageSize))
		{
			var scope = new NotificationSourceScopeDto(row.AcademicYearId, row.EducationBoardId, row.EducationMediumId, row.ClassId, row.DivisionId);
			if (!windows.TryGetValue(scope, out var window))
				windows[scope] = window = await timing.ResolveAsync(eventKey, scope, request.Now, zone, requireEnabled: true, cancellationToken);
			if (window != null)
				candidates.Add(new(eventKey, row.SourceId, LearningTaskSubmissionDigestTiming.Revision(scope, window),
					scope, window.End, window.ExpiresAt, NotificationDueTimeMode.FixedOccurrence));
		}
		var next = page.Count <= request.PageSize ? null : $"{page[request.PageSize - 1].SourceId:N}:{page[request.PageSize - 1].DivisionId:N}";
		return new(candidates, next);
	}
}
