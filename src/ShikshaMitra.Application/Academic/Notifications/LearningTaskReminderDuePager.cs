using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed class LearningTaskDueRow
{
	public Guid SourceId { get; init; }
	public Guid ClassId { get; init; }
	public Guid DivisionId { get; init; }
	public DateTime DueDate { get; init; }
	public DateTime? PublishedAt { get; init; }
}

[RemoteService(false)]
public sealed class LearningTaskReminderDuePager(IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<NotificationDueSourcePage> ReadPageAsync(string dueEventKey, string overdueEventKey, IQueryable<LearningTaskDueRow> occurrences,
		NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
		if (request.Continuation is { } continuation)
		{
			var (sourceId, divisionId) = ReadCursor(continuation);
			occurrences = occurrences.Where(x => x.SourceId.CompareTo(sourceId) > 0 ||
				x.SourceId == sourceId && x.DivisionId.CompareTo(divisionId) > 0);
		}

		var now = request.Now.UtcDateTime;
		var startedAt = request.CycleStartedAt.UtcDateTime;
		var oldestDeadline = now - LearningTaskReminderSourceAdapter.OverdueLifetime;
		var overdueRules = (await rules.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.EventKey == overdueEventKey && x.IsEnabled);
		var scoped = from occurrence in occurrences
			join division in await divisions.GetQueryableAsync() on occurrence.DivisionId equals division.Id
			join standard in await standards.GetQueryableAsync() on occurrence.ClassId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.TenantId == request.TenantId && division.StandardId == standard.Id &&
				standard.TenantId == request.TenantId && stage.TenantId == request.TenantId &&
				medium.TenantId == request.TenantId && board.TenantId == request.TenantId && board.AcademicYear.TenantId == request.TenantId &&
				(occurrence.DueDate > now ||
				 occurrence.DueDate > oldestDeadline && occurrence.DueDate <= startedAt &&
				 (!occurrence.PublishedAt.HasValue || occurrence.PublishedAt.Value <= occurrence.DueDate) &&
				 overdueRules.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == board.AcademicYearId) &&
				     (!rule.BoardId.HasValue || rule.BoardId == board.Id) && (!rule.MediumId.HasValue || rule.MediumId == medium.Id) &&
				     (!rule.ClassId.HasValue || rule.ClassId == standard.Id) && (!rule.DivisionId.HasValue || rule.DivisionId == division.Id)))
			orderby occurrence.SourceId, occurrence.DivisionId
			select new { occurrence.SourceId, occurrence.DivisionId, occurrence.ClassId, occurrence.DueDate,
				board.AcademicYearId, medium.EducationBoardId, stage.EducationMediumId };

		var page = await queries.ToListAsync(scoped.Take(request.PageSize + 1), cancellationToken);
		var candidates = page.Take(request.PageSize).Select(x =>
		{
			var scope = new NotificationSourceScopeDto(x.AcademicYearId, x.EducationBoardId, x.EducationMediumId, x.ClassId, x.DivisionId);
			var dueAt = LearningTaskNotificationRecordReader.AsUtc(x.DueDate);
			var isOverdue = dueAt <= request.Now;
			var key = isOverdue ? overdueEventKey : dueEventKey;
			var expiry = isOverdue ? dueAt + LearningTaskReminderSourceAdapter.OverdueLifetime : dueAt;
			var mode = isOverdue ? NotificationDueTimeMode.FixedOccurrence : NotificationDueTimeMode.PrecedingSchoolDays;
			return new NotificationDueSourceCandidate(key, x.SourceId,
				LearningTaskReminderSourceAdapter.DueRevision(scope, dueAt), scope, dueAt, expiry, mode);
		}).ToArray();
		var next = page.Count <= request.PageSize ? null
			: $"{page[request.PageSize - 1].SourceId:N}:{page[request.PageSize - 1].DivisionId:N}";
		return new(candidates, next);
	}

	private static (Guid SourceId, Guid DivisionId) ReadCursor(string cursor)
	{
		if (cursor.Length != 65 || cursor[32] != ':' ||
			!Guid.TryParseExact(cursor.AsSpan(0, 32), "N", out var task) || task == Guid.Empty ||
			!Guid.TryParseExact(cursor.AsSpan(33, 32), "N", out var division) || division == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.StateConflict);
		return (task, division);
	}
}
