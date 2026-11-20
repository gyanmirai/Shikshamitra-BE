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
public sealed class AcademicWeeklySummaryDueSource(ICurrentTenant tenant, IRepository<NotificationRule, Guid> rules,
	IRepository<StudentSubmissionEntity, Guid> submissions, IRepository<Division, Guid> divisions,
	IRepository<Standard, Guid> standards, IAsyncQueryableExecuter queries, AcademicWeeklySummaryTiming timing)
	: INotificationDueSource, ITransientDependency
{
	public string SourceType => AcademicWeeklySummaryTiming.SourceType;
	public string ScanKey => AcademicWeeklySummaryTiming.EventKey;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcademicWeeklySummaryTiming.EventKey });

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken token)
	{
		var school = request.TenantId;
		if (school == Guid.Empty || school != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));

		var enabled = (await rules.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == AcademicWeeklySummaryTiming.EventKey);
		if (!await queries.AnyAsync(enabled, token)) return new([], null);
		// One closed local week and the following delivery day fit inside this discovery range even across an offset change.
		var activity = LearningTaskSubmissionFacts.InWindow(await submissions.GetQueryableAsync(), school,
			request.CycleStartedAt.AddDays(-9), request.CycleStartedAt);
		var activeDivisions = activity.Where(x => x.AssignmentId.HasValue != x.HomeworkAssignmentId.HasValue)
			.Select(x => x.DivisionId).Distinct();
		var scoped = from id in activeDivisions
			join division in await divisions.GetQueryableAsync() on id equals division.Id
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.TenantId == school && !division.IsDeleted && standard.TenantId == school && !standard.IsDeleted &&
				stage.TenantId == school && !stage.IsDeleted && medium.TenantId == school && !medium.IsDeleted &&
				board.TenantId == school && !board.IsDeleted && board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted &&
				enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == board.AcademicYearId) &&
					(!rule.BoardId.HasValue || rule.BoardId == board.Id) && (!rule.MediumId.HasValue || rule.MediumId == medium.Id) &&
					(!rule.ClassId.HasValue || rule.ClassId == standard.Id) && (!rule.DivisionId.HasValue || rule.DivisionId == division.Id))
			select new { DivisionId = division.Id, ClassId = standard.Id, board.AcademicYearId,
				medium.EducationBoardId, stage.EducationMediumId };
		if (request.Continuation is { } continuation)
		{
			if (!Guid.TryParseExact(continuation, "N", out var last) || last == Guid.Empty)
				throw new BusinessException(NotificationErrorCodes.StateConflict);
			scoped = scoped.Where(x => x.DivisionId.CompareTo(last) > 0);
		}

		var page = await queries.ToListAsync(scoped.OrderBy(x => x.DivisionId).Take(request.PageSize + 1), token);
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in page.Take(request.PageSize))
		{
			var scope = new NotificationSourceScopeDto(row.AcademicYearId, row.EducationBoardId, row.EducationMediumId,
				row.ClassId, row.DivisionId);
			if (await timing.ResolveAsync(scope, request.Now, token) is { } window)
				candidates.Add(new(ScanKey, row.DivisionId, window.Revision,
					scope, window.Period.End, window.Period.ExpiresAt, NotificationDueTimeMode.FixedOccurrence));
		}

		var next = page.Count > request.PageSize ? page[request.PageSize - 1].DivisionId.ToString("N") : null;
		return new(candidates, next);
	}
}
