using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using ProgressRecord = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class AcademicProgressDigestDueSource(ICurrentTenant tenant, IRepository<AssignedSubject, Guid> assignments,
	IRepository<AssignedSubjectChapter, Guid> chapters, IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries, NotificationSchoolTimeZone schoolZone,
	LearningTaskSubmissionDigestTiming timing, AcademicProgressDigestTargets targets) : INotificationDueSource, ITransientDependency
{
	internal const string Type = "Academic.Progress";
	internal const string EventKey = AcademicNotificationEventKeys.ProgressBehindSchedule;
	public string SourceType => Type;
	public string ScanKey => EventKey;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		var school = request.TenantId;
		if (school == Guid.Empty || school != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));

		var enabled = (await rules.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.EventKey == EventKey && x.IsEnabled);
		if (!await queries.AnyAsync(enabled, token)) return new([], null);

		var assigned = (await assignments.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == SubjectStatus.Published);
		var published = AcademicProgressDigestTargets.PublishedTargets(await chapters.GetQueryableAsync(), school);
		var scoped = from assignment in assigned
			join standard in await standards.GetQueryableAsync() on assignment.StandardId equals standard.Id
			join division in await divisions.GetQueryableAsync() on standard.Id equals division.StandardId
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where standard.TenantId == school && !standard.IsDeleted && division.TenantId == school && !division.IsDeleted &&
				stage.TenantId == school && !stage.IsDeleted && medium.TenantId == school && !medium.IsDeleted &&
				board.TenantId == school && !board.IsDeleted && board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted &&
				published.Any(chapter => chapter.AssignedSubjectId == assignment.Id) &&
				enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == board.AcademicYearId) &&
					(!rule.BoardId.HasValue || rule.BoardId == board.Id) && (!rule.MediumId.HasValue || rule.MediumId == medium.Id) &&
					(!rule.ClassId.HasValue || rule.ClassId == standard.Id) && (!rule.DivisionId.HasValue || rule.DivisionId == division.Id))
			select new { AssignmentId = assignment.Id, DivisionId = division.Id, ClassId = standard.Id,
				board.AcademicYearId, medium.EducationBoardId, stage.EducationMediumId };
		if (request.Continuation is { } continuation)
		{
			var (assignmentId, divisionId) = LearningTaskReminderDuePager.ReadCursor(continuation);
			scoped = scoped.Where(x => x.AssignmentId.CompareTo(assignmentId) > 0 ||
				x.AssignmentId == assignmentId && x.DivisionId.CompareTo(divisionId) > 0);
		}

		var page = await queries.ToListAsync(scoped.OrderBy(x => x.AssignmentId).ThenBy(x => x.DivisionId).Take(request.PageSize + 1), token);
		if (page.Count == 0) return new([], null);

		var zone = await schoolZone.GetAsync(token);
		var windows = new Dictionary<NotificationSourceScopeDto, LearningTaskDigestWindow?>();
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in page.Take(request.PageSize))
		{
			var scope = new NotificationSourceScopeDto(row.AcademicYearId, row.EducationBoardId, row.EducationMediumId, row.ClassId, row.DivisionId);
			if (!windows.TryGetValue(scope, out var window))
				windows[scope] = window = await timing.ResolveAsync(EventKey, scope, request.Now, zone, requireEnabled: true, token);
			if (window == null || await targets.CountIncompleteAsync(school, row.AssignmentId, row.DivisionId, window, token) == 0)
				continue;
			candidates.Add(new(EventKey, row.AssignmentId, LearningTaskSubmissionDigestTiming.Revision(scope, window),
				scope, window.End, window.ExpiresAt, NotificationDueTimeMode.FixedOccurrence));
		}

		var last = page[Math.Min(page.Count, request.PageSize) - 1];
		return new(candidates, page.Count > request.PageSize ? $"{last.AssignmentId:N}:{last.DivisionId:N}" : null);
	}
}

[RemoteService(false)]
public sealed class AcademicProgressDigestTargets(IRepository<AssignedSubjectChapter, Guid> chapters,
	IRepository<ProgressRecord, Guid> progress, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal static IQueryable<AssignedSubjectChapter> PublishedTargets(IQueryable<AssignedSubjectChapter> chapters, Guid school) =>
		chapters.Where(x => x.TenantId == school && !x.IsDeleted &&
			(x.PublishStatus == "Published" || x.PublishStatus == "published") &&
			x.ChapterStartTime != default && x.ChapterEndTime != default);

	internal async Task<int> CountIncompleteAsync(Guid school, Guid assignmentId, Guid divisionId, LearningTaskDigestWindow window,
		CancellationToken token)
	{
		var published = PublishedTargets(await chapters.GetQueryableAsync(), school).Where(x => x.AssignedSubjectId == assignmentId);
		var progressForDivision = (await progress.GetQueryableAsync()).Where(x => x.DivisionId == divisionId && !x.IsDeleted);
		var dates = await queries.ToListAsync(published.Select(chapter => new
		{
			chapter.ChapterStartTime,
			chapter.ChapterEndTime,
			Progress = progressForDivision.Where(x => x.ChapterId == chapter.Id).OrderByDescending(x => x.CreationTime)
				.ThenByDescending(x => x.Id).Select(x => (int?)x.Progress).FirstOrDefault() ?? 0
		}), token);
		return dates.Count(x => x.Progress < 100 && x.ChapterStartTime <= x.ChapterEndTime &&
			x.ChapterEndTime > window.Start && x.ChapterEndTime <= window.End);
	}
}
