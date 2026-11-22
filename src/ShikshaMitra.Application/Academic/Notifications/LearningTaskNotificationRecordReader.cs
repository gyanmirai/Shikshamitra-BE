using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record LearningTaskNotificationPlacement(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, string ClassName);
internal sealed record LearningTaskNotificationSubject(Guid Id, string Name, Guid? ElectiveGroupId);
internal sealed record LearningTaskNotificationSchedule(Guid DivisionId, DateTimeOffset DueAt, DateTimeOffset? PublishedAt);
internal sealed record LearningTaskNotificationTask(Guid Id, Guid StandardId, Guid SubjectId, string Title, SubmissionType SubmissionType, bool IsAssignment);
internal sealed record LearningTaskNotificationRecord(LearningTaskNotificationTask Task, LearningTaskNotificationPlacement Placement,
	LearningTaskNotificationSubject Subject, LearningTaskNotificationSchedule[] Schedules);

[RemoteService(false)]
public sealed class LearningTaskNotificationRecordReader(IRepository<HomeworkAssignmentEntity, Guid> homeworks, IRepository<AssignmentEntity, Guid> assignments,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<AssignedSubject, Guid> subjects, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<LearningTaskNotificationRecord?> FindHomeworkAsync(Guid tenantId, Guid sourceId, DateTimeOffset now, CancellationToken cancellationToken, bool includeClosed = false)
	{
		var task = await queries.SingleOrDefaultAsync((await homeworks.WithDetailsAsync(x => x.DivisionSchedules))
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == sourceId), cancellationToken);
		if (task == null || !includeClosed && task.Status == HomeworkStatus.Closed)
			return null;

		var input = new LearningTaskNotificationTask(task.Id, task.StandardId, task.SubjectId, task.Title, task.SubmissionType, IsAssignment: false);
		return await ValidateAsync(tenantId, task, input, ReadSchedules(task, tenantId, now), cancellationToken);
	}

	internal async Task<LearningTaskNotificationRecord?> FindAssignmentAsync(Guid tenantId, Guid sourceId, DateTimeOffset now,
		CancellationToken cancellationToken, bool includeClosed = false)
	{
		var task = await queries.SingleOrDefaultAsync((await assignments.WithDetailsAsync(x => x.DivisionSchedules))
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == sourceId), cancellationToken);
		if (task == null || !includeClosed && task.Status == AssignmentStatus.Closed) return null;

		var schedules = task.DivisionSchedules.Where(x => x.TenantId == tenantId && x.IsPublished &&
			(!x.PublishedAt.HasValue || AsUtc(x.PublishedAt.Value) <= now)).OrderBy(x => x.DivisionId)
			.Select(x => new LearningTaskNotificationSchedule(x.DivisionId, AsUtc(x.DueDate),
				x.PublishedAt.HasValue ? AsUtc(x.PublishedAt.Value) : null)).ToArray();
		var input = new LearningTaskNotificationTask(task.Id, task.StandardId, task.SubjectId, task.Title, task.SubmissionType, IsAssignment: true);
		return await ValidateAsync(tenantId, task, input, schedules, cancellationToken);
	}

	private async Task<LearningTaskNotificationRecord?> ValidateAsync(Guid tenantId, IUserScopedEntity source, LearningTaskNotificationTask task,
		LearningTaskNotificationSchedule[] schedules, CancellationToken cancellationToken)
	{
		var placement = await queries.SingleOrDefaultAsync((await standards.GetQueryableAsync())
			.Where(x => x.Id == task.StandardId && x.TenantId == tenantId && x.EducationMediumStage.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.EducationBoard.TenantId == tenantId &&
				x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYear.TenantId == tenantId)
			.Select(x => new LearningTaskNotificationPlacement(x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
				x.EducationMediumStage.EducationMedium.EducationBoardId, x.EducationMediumStage.EducationMediumId,
				x.EducationMediumStageId, x.Name)), cancellationToken);
		if (placement == null || source.AcademicYearId.HasValue && source.AcademicYearId != placement.YearId ||
			source.EducationBoardId.HasValue && source.EducationBoardId != placement.BoardId ||
			source.EducationMediumId.HasValue && source.EducationMediumId != placement.MediumId ||
			source.EducationMediumStageId.HasValue && source.EducationMediumStageId != placement.StageId)
			return null;

		if (schedules.Length == 0 || schedules.Select(x => x.DivisionId).Distinct().Count() != schedules.Length) return null;
		var divisionIds = schedules.Select(x => x.DivisionId).ToArray();
		var validDivisions = await queries.CountAsync((await divisions.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && x.StandardId == task.StandardId && Enumerable.Contains(divisionIds, x.Id)), cancellationToken);
		if (validDivisions != divisionIds.Length) return null;

		var assigned = await queries.ToListAsync((await subjects.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.StandardId == task.StandardId && (x.Id == task.SubjectId || x.SubjectId == task.SubjectId) &&
			x.Subject.TenantId == tenantId && x.Status == SubjectStatus.Published)
			.Select(x => new LearningTaskNotificationSubject(x.SubjectId, x.Subject.Name, x.ElectiveGroupId)).Distinct()
			.Take(2), cancellationToken);
		return assigned.Count == 1 ? new(task, placement, assigned[0], schedules) : null;
	}

	private static LearningTaskNotificationSchedule[] ReadSchedules(HomeworkAssignmentEntity task, Guid tenantId, DateTimeOffset now)
	{
		if (task.DivisionSchedules.Count == 0)
			return task.IsPublished && task.DivisionId is Guid id && task.DueDate is DateTime due
				? [new(id, AsUtc(due), null)] : [];

		return task.DivisionSchedules.Where(x => x.TenantId == tenantId && x.IsPublished &&
			(!x.PublishedAt.HasValue || AsUtc(x.PublishedAt.Value) <= now))
			.OrderBy(x => x.DivisionId)
			.Select(x => new LearningTaskNotificationSchedule(x.DivisionId, AsUtc(x.DueDate),
				x.PublishedAt.HasValue ? AsUtc(x.PublishedAt.Value) : null)).ToArray();
	}

	internal static DateTimeOffset AsUtc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));
}
