using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private sealed record NotificationClassContext(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid OriginalClassId, Guid NextClassId);
	private Task<NotificationClassContext> SeedNotificationClassesAsync(Context context) => WithUnitOfWorkAsync<NotificationClassContext>(async () => {
		var students = GetRequiredService<IRepository<Student, Guid>>(); var student = await students.GetAsync(context.StudentId);
		var standards = GetRequiredService<IRepository<Standard, Guid>>(); var manager = GetRequiredService<StandardManager>();
		var original = await manager.CreateAsync(student.AcademicMediumStageId!.Value, "Original Class", 6, null);
		await standards.InsertAsync(original, true);
		var next = await manager.CreateAsync(student.AcademicMediumStageId.Value, "Next Class", 7, null);
		await standards.InsertAsync(next, true);
		student.UpdateAcademicPlacement(student.AcademicYearId!.Value, student.AcademicBoardId!.Value, student.AcademicMediumId,
			student.AcademicMediumStageId.Value, original.Id, null);
		await students.UpdateAsync(student, true);
		await GetRequiredService<IRepository<BorrowingRule, Guid>>().InsertAsync(new BorrowingRule(Guid.NewGuid(), context.TenantId,
			student.AcademicYearId.Value, 0, 4, 2), true);
		return new(student.AcademicYearId.Value, student.AcademicBoardId.Value, student.AcademicMediumId,
			student.AcademicMediumStageId.Value, original.Id, next.Id);
	});
	private Task AssignNotificationClassAsync(Context context, NotificationClassContext placement, Guid classId) => WithUnitOfWorkAsync(async () => {
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == context.ActorId, true);
		await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), context.TenantId, userId: context.ActorId,
			academicYearId: placement.YearId, educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
			educationMediumStageId: placement.StageId, standardId: classId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(context.ActorId);
	});
	private Task MoveNotificationStudentAsync(Context context, NotificationClassContext placement, Guid classId) => WithUnitOfWorkAsync(async () => {
		var students = GetRequiredService<IRepository<Student, Guid>>(); var student = await students.GetAsync(context.StudentId);
		student.UpdateAcademicPlacement(placement.YearId, placement.BoardId, placement.MediumId, placement.StageId, classId, null);
		await students.UpdateAsync(student, true);
	});
}
