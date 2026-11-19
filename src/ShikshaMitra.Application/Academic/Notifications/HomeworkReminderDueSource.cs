using System;
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
public sealed class HomeworkReminderDueSource(IRepository<HomeworkAssignmentEntity, Guid> homeworks, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, IAsyncQueryableExecuter queries, ICurrentTenant tenant) : INotificationDueSource, ITransientDependency
{
	public string SourceType => HomeworkReminderSourceAdapter.SourceType;

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
		var now = request.Now.UtcDateTime;
		var startedAt = request.CycleStartedAt.UtcDateTime;
		var tasks = (await homeworks.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.Status != HomeworkStatus.Closed && x.SubmissionType != SubmissionType.NoSubmissionRequired);
		var scheduled = from task in tasks
			from schedule in task.DivisionSchedules
			where schedule.TenantId == request.TenantId && schedule.IsPublished && schedule.DueDate > now &&
				(!schedule.PublishedAt.HasValue || schedule.PublishedAt <= startedAt)
			select new { SourceId = task.Id, ClassId = task.StandardId, schedule.DivisionId, schedule.DueDate };
		var legacy = from task in tasks
			where task.IsPublished && task.DivisionId.HasValue && task.DueDate > now && !task.DivisionSchedules.Any()
			select new { SourceId = task.Id, ClassId = task.StandardId, DivisionId = task.DivisionId!.Value, DueDate = task.DueDate!.Value };
		var occurrences = scheduled.Concat(legacy);
		if (request.Continuation is { } continuation)
		{
			var (sourceId, divisionId) = ReadCursor(continuation);
			occurrences = occurrences.Where(x => x.SourceId.CompareTo(sourceId) > 0 ||
				x.SourceId == sourceId && x.DivisionId.CompareTo(divisionId) > 0);
		}

		var scoped = from occurrence in occurrences
			join division in await divisions.GetQueryableAsync() on occurrence.DivisionId equals division.Id
			join standard in await standards.GetQueryableAsync() on occurrence.ClassId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.TenantId == request.TenantId && division.StandardId == standard.Id &&
				standard.TenantId == request.TenantId && stage.TenantId == request.TenantId &&
				medium.TenantId == request.TenantId && board.TenantId == request.TenantId && board.AcademicYear.TenantId == request.TenantId
			orderby occurrence.SourceId, occurrence.DivisionId
			select new { occurrence.SourceId, occurrence.DivisionId, occurrence.ClassId, occurrence.DueDate,
				board.AcademicYearId, medium.EducationBoardId, stage.EducationMediumId };

		var page = await queries.ToListAsync(scoped.Take(request.PageSize + 1), cancellationToken);
		var candidates = page.Take(request.PageSize).Select(x =>
		{
			var scope = new NotificationSourceScopeDto(x.AcademicYearId, x.EducationBoardId, x.EducationMediumId, x.ClassId, x.DivisionId);
			var dueAt = HomeworkReminderRecordReader.AsUtc(x.DueDate);
			return new NotificationDueSourceCandidate(HomeworkReminderSourceAdapter.EventKey, x.SourceId,
				HomeworkReminderSourceAdapter.DueRevision(scope, dueAt), scope, dueAt, dueAt, NotificationDueTimeMode.PrecedingSchoolDays);
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
