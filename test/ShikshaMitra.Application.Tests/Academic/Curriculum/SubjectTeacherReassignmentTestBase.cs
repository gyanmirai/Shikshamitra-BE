using System;
using Shouldly;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Identity;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Uow;
using SchoolTimetable = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public abstract class SubjectTeacherReassignmentTestBase : LessonPlanProgressTestBase
{
	protected readonly Guid OldTeacher = Guid.NewGuid();
	protected readonly Guid NewTeacher = Guid.NewGuid();
	protected readonly Guid OtherTeacher = Guid.NewGuid();
	protected readonly Guid ActorId = Guid.NewGuid();
	protected Guid AssignmentId;
	protected ITimetableRepository Timetables => GetRequiredService<ITimetableRepository>();
	protected ISubjectAssignmentAppService Allocations => GetRequiredService<ISubjectAssignmentAppService>();
	protected IDisposable AsAdmin() => GetRequiredService<ICurrentPrincipalAccessor>().Change(
		new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, ActorId.ToString()),
			new Claim(AbpClaimTypes.Role, "admin")], "Test")));
	protected static DateTimeOffset Today => new(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(330)).Date, TimeSpan.FromMinutes(330));

	protected async Task WithTransactionalUnitOfWorkAsync(Func<Task> action)
	{
		// The shared application-test module replaces the manager with an always-nontransactional
		// one. Use ABP's real manager here to test the allocation HTTP request's rollback semantics.
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var unit = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		await action();
		await unit.CompleteAsync();
	}

	protected async Task SeedAllocationAsync()
	{
		await SeedContextAsync();
		await GetRequiredService<IIdentityUserRepository>().InsertAsync(
			new IdentityUser(ActorId, $"allocation-admin-{ActorId:N}", $"{ActorId:N}@example.test", TenantId), autoSave: true);
		await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), TenantId, ActorId), autoSave: true);
		var teacherRoleId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(
			new IdentityRole(teacherRoleId, $"allocation-teacher-{teacherRoleId:N}", TenantId), autoSave: true);
		Employee[] teachers =
			[
				CreateTeacher(OldTeacher, teacherRoleId, "Old", 1),
				CreateTeacher(NewTeacher, teacherRoleId, "New", 2),
				CreateTeacher(OtherTeacher, teacherRoleId, "Other", 3)
			];
		foreach (var teacher in teachers)
		{
			var userId = teacher.UserId!.Value;
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(
				new IdentityUser(userId, $"allocation-teacher-{userId:N}", $"{userId:N}@example.test", TenantId), autoSave: true);
			await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), TenantId, userId, standardId: StandardId), autoSave: true);
		}
		await GetRequiredService<IRepository<Employee, Guid>>().InsertManyAsync(teachers, autoSave: true);
		var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
		var assignment = (await repository.GetListAsync(a => a.StandardId == StandardId)).Single();
		assignment.SetPrimaryTeacher(OldTeacher);
		assignment.SetSecondaryTeachers([OtherTeacher]);
		AssignmentId = assignment.Id;
		await repository.UpdateAsync(assignment, autoSave: true);
	}

	private static Employee CreateTeacher(Guid id, Guid roleId, string firstName, int sequence) =>
		new(
			id,
			TenantId,
			Guid.NewGuid(),
			$"ALLOC{sequence:D3}",
			new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Teacher",
				FirstName: firstName,
				MiddleName: null,
				ShortName: firstName,
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: new DateOnly(1990, 1, sequence),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"allocation-{id:N}@example.test",
				PanNo: $"ABCDE{sequence:D4}F",
				AdharNo: $"100000000{sequence:D3}",
				Address1: null,
				Address2: null,
				Address3: null,
				Department: "Academics",
				Designation: "Teacher",
				DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: EmployeeGroupType.Teaching,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			new BankDetails(null, null, null, null, null),
			null);

	protected async Task<SubjectAssignmentDto> ChangeTeacherAsync(SubjectStatus status = SubjectStatus.Published) =>
		(await Allocations.UpdateAsync(AssignmentId, Guid.Empty, new CreateUpdateSubjectAssignmentDto
		{
			ClassId = StandardId, Status = status,
			Subjects = [new SubjectAssignmentDetailDto
			{
				SubjectId = SubjectId, SubjectCode = "MATH", PrimaryTeacherId = NewTeacher,
				SecondaryTeacherIds = [OtherTeacher]
			}]
		})).ShouldNotBeNull();

	protected async Task<SchoolTimetable> AddTimetableAsync(Guid? divisionId = null, bool published = true,
		TimetableDayOfWeek day = TimetableDayOfWeek.Tuesday, Guid? teacherId = null, Guid? standardId = null)
	{
		var timetable = new SchoolTimetable(Guid.NewGuid(), TenantId, standardId ?? StandardId, divisionId ?? DivisionId);
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, 5,
			new TimeOnly(9, 0), new TimeOnly(9, 30), SlotType.Period, subjectType: SubjectType.Elective);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, SubjectId, teacherId ?? OldTeacher, Guid.NewGuid()));
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid(),
			teacherId == OtherTeacher ? Guid.NewGuid() : OtherTeacher, Guid.NewGuid()));
		timetable.AddSlot(slot);
		if (published) timetable.Publish();
		return await Timetables.InsertAsync(timetable, autoSave: true);
	}

	protected Task<LessonPlan> AddPlanAsync(DateTimeOffset date, Guid? slotId = null, Guid? teacherId = null) =>
		Plans.InsertAsync(new LessonPlan(Guid.NewGuid(), TenantId, YearId, teacherId ?? OldTeacher,
			StandardId, DivisionId, SubjectId, ChapterId, date.UtcDateTime, Guid.NewGuid().ToString(), 30, slotId), autoSave: true);
}
