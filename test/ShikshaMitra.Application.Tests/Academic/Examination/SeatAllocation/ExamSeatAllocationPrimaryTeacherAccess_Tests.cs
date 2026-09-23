using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Fact]
	public async Task Seating_Plan_Should_Match_Schedule_Exam_Primary_Teacher_Access()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, _) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var teacherUserId = Guid.NewGuid();
				var teacher = await CreateSeatingPlanTeacherAsync(teacherUserId);
				var examSubjects = exam.ExamSubjects.OrderBy(subject => subject.SubjectId).ToList();

				await AssignPrimaryTeacherAsync(exam.StandardId, examSubjects[0].SubjectId, teacher.Id);
				using (GetRequiredService<ICurrentPrincipalAccessor>().Change(
					BuildSeatingPlanTeacherPrincipal(teacherUserId)))
				{
					var deniedList = await _seatAllocationAppService.GetSeatAllocationTableAsync(
						new Dtos.GetSeatAllocationTableInput { StandardId = exam.StandardId });
					deniedList.Items.Select(item => item.ExamId).ShouldNotContain(exam.Id);
					await Should.ThrowAsync<AbpAuthorizationException>(() =>
						_seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id));
				}

				await AssignPrimaryTeacherAsync(exam.StandardId, examSubjects[1].SubjectId, teacher.Id);
				using (GetRequiredService<ICurrentPrincipalAccessor>().Change(
					BuildSeatingPlanTeacherPrincipal(teacherUserId)))
				{
					var visibleList = await _seatAllocationAppService.GetSeatAllocationTableAsync(
						new Dtos.GetSeatAllocationTableInput { StandardId = exam.StandardId });
					visibleList.Items.Select(item => item.ExamId).ShouldContain(exam.Id);

					var workspace = await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id);
					workspace.ExamId.ShouldBe(exam.Id);
				}
			}
		});
	}

	private async Task<Employee> CreateSeatingPlanTeacherAsync(Guid userId)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var role = new IdentityRole(Guid.NewGuid(), $"seat-teacher-{suffix[..8]}", TestTenantId);
		await _identityRoleRepository.InsertAsync(role, autoSave: true);
		var user = new IdentityUser(userId, $"seat-teacher-{suffix[..8]}", $"{suffix[..12]}@test.local", TestTenantId);
		user.AddRole(role.Id);
		await _identityUserRepository.InsertAsync(user, autoSave: true);

		var employee = new Employee(
			Guid.NewGuid(),
			TestTenantId,
			userId,
			$"SEAT{suffix[..8]}",
			new EmployeeBasicInfo(
				null, "Teacher", "Seating", null, "Seating Teacher", EmployeeStatusType.Active,
				new DateOnly(1990, 1, 1), null, null, null, null, null, null, null, null,
				$"{suffix[..12]}@test.local", $"ABCDE{suffix[..4].ToUpperInvariant()}F",
				$"1{Math.Abs((long)suffix.GetHashCode()):D11}"[..12], null, null, null,
				"Academics", "Teacher", new DateOnly(2025, 1, 1), EmployeeGroupType.Teaching,
				null, role.Id, null, null, null, null, null, null),
			new BankDetails(null, null, null, null, null),
			null);
		return await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, autoSave: true);
	}

	private async Task AssignPrimaryTeacherAsync(Guid standardId, Guid subjectId, Guid teacherId)
	{
		var assignment = new AssignedSubject(
			Guid.NewGuid(), TestTenantId, standardId, subjectId,
			$"SUB-{Guid.NewGuid():N}"[..12], SubjectStatus.Published);
		assignment.SetPrimaryTeacher(teacherId);
		await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assignment, autoSave: true);
	}

	private static ClaimsPrincipal BuildSeatingPlanTeacherPrincipal(Guid userId) =>
		new(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.UserName, "seating.teacher"),
			new Claim(AbpClaimTypes.Role, "teacher"),
			new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())
		], "Test"));
}
