using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record HomeworkReminderPlacement(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, string ClassName);
internal sealed record HomeworkReminderSubject(Guid Id, string Name, Guid? ElectiveGroupId);
internal sealed record HomeworkReminderSchedule(Guid DivisionId, DateTimeOffset DueAt, DateTimeOffset? PublishedAt);
internal sealed record HomeworkReminderRecord(HomeworkAssignmentEntity Task, HomeworkReminderPlacement Placement,
	HomeworkReminderSubject Subject, HomeworkReminderSchedule[] Schedules);

[RemoteService(false)]
public sealed class HomeworkReminderRecordReader(IRepository<HomeworkAssignmentEntity, Guid> tasks,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<AssignedSubject, Guid> subjects, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<HomeworkReminderRecord?> FindAsync(Guid tenantId, Guid sourceId, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var task = await queries.SingleOrDefaultAsync((await tasks.WithDetailsAsync(x => x.DivisionSchedules))
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == sourceId), cancellationToken);
		if (task == null || task.Status == HomeworkStatus.Closed)
			return null;

		var placement = await queries.SingleOrDefaultAsync((await standards.GetQueryableAsync())
			.Where(x => x.Id == task.StandardId && x.TenantId == tenantId && x.EducationMediumStage.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.EducationBoard.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYear.TenantId == tenantId)
			.Select(x => new HomeworkReminderPlacement(x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
				x.EducationMediumStage.EducationMedium.EducationBoardId, x.EducationMediumStage.EducationMediumId,
				x.EducationMediumStageId, x.Name)), cancellationToken);
		if (placement == null || task.AcademicYearId.HasValue && task.AcademicYearId != placement.YearId ||
			task.EducationBoardId.HasValue && task.EducationBoardId != placement.BoardId ||
			task.EducationMediumId.HasValue && task.EducationMediumId != placement.MediumId ||
			task.EducationMediumStageId.HasValue && task.EducationMediumStageId != placement.StageId)
			return null;

		var schedules = ReadSchedules(task, tenantId, now);
		if (schedules.Length == 0 || schedules.Select(x => x.DivisionId).Distinct().Count() != schedules.Length) return null;
		var divisionIds = schedules.Select(x => x.DivisionId).ToArray();
		var validDivisions = await queries.CountAsync((await divisions.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && x.StandardId == task.StandardId && Enumerable.Contains(divisionIds, x.Id)), cancellationToken);
		if (validDivisions != divisionIds.Length) return null;

		var assigned = await queries.ToListAsync((await subjects.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.StandardId == task.StandardId && (x.Id == task.SubjectId || x.SubjectId == task.SubjectId) &&
			x.Subject.TenantId == tenantId && x.Status == SubjectStatus.Published)
			.Select(x => new HomeworkReminderSubject(x.SubjectId, x.Subject.Name, x.ElectiveGroupId)).Distinct()
			.Take(2), cancellationToken);
		return assigned.Count == 1 ? new(task, placement, assigned[0], schedules) : null;
	}

	private static HomeworkReminderSchedule[] ReadSchedules(HomeworkAssignmentEntity task, Guid tenantId, DateTimeOffset now)
	{
		if (task.DivisionSchedules.Count == 0)
			return task.IsPublished && task.DivisionId is Guid id && task.DueDate is DateTime due
				? [new(id, AsUtc(due), null)] : [];

		return task.DivisionSchedules.Where(x => x.TenantId == tenantId && x.IsPublished &&
			(!x.PublishedAt.HasValue || AsUtc(x.PublishedAt.Value) <= now))
			.OrderBy(x => x.DivisionId)
			.Select(x => new HomeworkReminderSchedule(x.DivisionId, AsUtc(x.DueDate),
				x.PublishedAt.HasValue ? AsUtc(x.PublishedAt.Value) : null)).ToArray();
	}

	internal static DateTimeOffset AsUtc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));
}
