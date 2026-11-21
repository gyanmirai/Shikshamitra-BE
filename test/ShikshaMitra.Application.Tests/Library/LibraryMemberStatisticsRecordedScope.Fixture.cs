using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	private Task MoveStatisticsBorrowerAndActorAsync(StaffScopeFixture fixture, bool staff, bool originalScope) => WithUnitOfWorkAsync(async () =>
	{
		var context = fixture.Context;
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
		Guid boardId = context.BoardId, mediumId = context.MediumId, stageId = context.StageId;
		Guid? classId = originalScope ? context.BorrowerClassId : context.OtherClassId;
		if (staff)
		{
			var board = new EducationBoard(Guid.NewGuid(), tenantId, EducationBoardType.GSEB, context.Catalog.YearId);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), tenantId, "Reassigned statistics medium", board.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Reassigned statistics stage", null, medium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var placements = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await placements.DeleteAsync(row => row.EmployeeId == fixture.EmployeeId, true);
			await placements.InsertAsync(new(Guid.NewGuid(), tenantId, fixture.EmployeeId, stage.Id), true);
			if (!originalScope) { boardId = board.Id; mediumId = medium.Id; stageId = stage.Id; }
			classId = null;
		}
		else
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(context.StudentId);
			student.UpdateAcademicPlacement(context.Catalog.YearId, context.BoardId, context.MediumId, context.StageId, context.OtherClassId, null);
			await students.UpdateAsync(student, true);
		}
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == actorId, true);
		await assignments.InsertAsync(new(Guid.NewGuid(), tenantId, userId: actorId, academicYearId: context.Catalog.YearId,
			educationBoardId: boardId, educationMediumId: mediumId, educationMediumStageId: stageId, standardId: classId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actorId);
	});

	private Task<Guid> SeedStatisticsLoanAsync(StaffScopeFixture fixture, DateOnly dueDate) => WithUnitOfWorkAsync<Guid>(async () =>
	{
		var context = fixture.Context;
		var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), copy.TenantId, copy.Id, copy.AccessionNumber, context.StudentId, 0,
			new(2026, 11, 10), dueDate, new(context.Catalog.YearId, context.BoardId, context.MediumId, context.StageId,
				context.BorrowerClassId, null, "{}", "{}", "{}", GetRequiredService<ICurrentUser>().Id!.Value,
				new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		return loan.Id;
	});
}
