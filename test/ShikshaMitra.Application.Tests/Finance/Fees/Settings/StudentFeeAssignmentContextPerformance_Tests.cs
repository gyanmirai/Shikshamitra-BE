using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Assignment_context_reads_are_bounded_for_ten_students(bool assigned)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 1000m);
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var original = await students.GetAsync(input.StudentId);
		var ids = new System.Collections.Generic.List<Guid> { original.Id };
		for (var i = 1; i < 10; i++)
		{
			var student = new Student(Guid.NewGuid(), TestTenantId, $"Context {i}", null, "Student", input.AcademicYearId,
				original.AcademicBoardId!.Value, original.AcademicMediumId, original.AcademicMediumStageId!.Value,
				original.StandardId, status: StudentLifeCycleStatusEnum.Enrolled);
			await students.InsertAsync(student, true); ids.Add(student.Id);
		}
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		if (assigned)
			foreach (var id in ids)
			{
				var command = new ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.AssignStudentFeeStructureDto {
					StudentId = id, AcademicYearId = input.AcademicYearId, StandardFeeStructureId = input.StandardFeeStructureId };
				command.PreviewToken = (await service.PreviewAsync(command)).PreviewToken; await service.AssignAsync(command);
			}
		async Task<int> CountAsync(System.Collections.Generic.List<Guid> selected)
		{
			var count = 0;
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
				var result = await service.GetContextsAsync(new() { AcademicYearId = input.AcademicYearId, StudentIds = selected });
				result.Select(x => x.StudentId).ShouldBe(selected);
				result.ShouldAllBe(x => x.Status == (assigned ? "Assigned" : "Unassigned"));
				if (assigned) result.ShouldAllBe(x => x.AssignedAssignment!.ProratedTotal == 1000m && x.CanEditStructure);
				count = reads.Commands.Count();
			});
			return count;
		}
		var single = await CountAsync([ids[0]]);
		var batch = await CountAsync(ids);
		batch.ShouldBeLessThanOrEqualTo(single + 3, $"one student used {single} reads; ten used {batch}");
	}
	[Fact]
	public async Task Assignment_history_retains_division_and_roll_after_promotion_for_division_only_reviewers()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var divisionId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await students.GetAsync(input.StudentId);
			var division = await GetRequiredService<IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>().InsertAsync(
				await GetRequiredService<ShikshaMitra.TenantSettings.AcademicSetups.DivisionManager>().CreateAsync(student.StandardId!.Value, "Historical assignment division", 30), true);
			divisionId = division.Id;
			student.UpdateAcademicPlacement(input.AcademicYearId, student.AcademicBoardId!.Value, student.AcademicMediumId,
				student.AcademicMediumStageId!.Value, student.StandardId.Value, divisionId);
			student.AssignRollNo(17); await students.UpdateAsync(student, true);
		});
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		await service.AssignAsync(input);
		await WithUnitOfWorkAsync(async () =>
		{
			var nextYear = await _academicYearRepo.InsertAsync(new ShikshaMitra.TenantSettings.AcademicYears.AcademicYear(Guid.NewGuid(), TestTenantId,
				"Historical context next year", new(2026, 4, 1), new(2027, 3, 31)), true);
			var student = await students.GetAsync(input.StudentId);
			student.UpdateAcademicPlacement(nextYear.Id, student.AcademicBoardId!.Value, student.AcademicMediumId,
				student.AcademicMediumStageId!.Value, student.StandardId!.Value, null);
			student.AssignRollNo(99); await students.UpdateAsync(student, true);
		});
		async Task CheckHistoricalAsync()
		{
			var list = await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, DivisionId = divisionId, MaxResultCount = 10 });
			var historical = list.Items.Single(x => x.StudentId == input.StudentId);
			historical.DivisionId.ShouldBe(divisionId); historical.RollNumber.ShouldBe(17); historical.IsActive.ShouldBeFalse();
			(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).StudentId.ShouldBe(input.StudentId);
		}
		await CheckHistoricalAsync();
		var limitedId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await students.GetAsync(input.StudentId);
			await GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>().InsertAsync(new Volo.Abp.Identity.IdentityUser(limitedId,
				"assignment-division-reviewer", "assignment-division-reviewer@example.test", TestTenantId), true);
			await GetRequiredService<IRepository<ShikshaMitra.Security.UserScope.UserScopeAssignment, Guid>>().InsertAsync(
				new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: limitedId,
					academicYearId: input.AcademicYearId, educationBoardId: student.AcademicBoardId, educationMediumId: student.AcademicMediumId,
					educationMediumStageId: student.AcademicMediumStageId, standardId: student.StandardId, divisionId: divisionId), true);
		});
		using var limited = GetRequiredService<Volo.Abp.Security.Claims.ICurrentPrincipalAccessor>().Change(new System.Security.Claims.ClaimsPrincipal(
			new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(Volo.Abp.Security.Claims.AbpClaimTypes.UserId, limitedId.ToString())], "Test")));
		await CheckHistoricalAsync();
	}

	[Fact]
	public async Task Assignment_legacy_history_does_not_infer_division_or_roll_from_the_students_new_year()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var divisionId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(
				new StudentFeeStructure(Guid.NewGuid(), TestTenantId, input.StudentId, input.StandardFeeStructureId, input.AcademicYearId), true);
			var students = GetRequiredService<IRepository<Student, Guid>>(); var student = await students.GetAsync(input.StudentId);
			var division = await GetRequiredService<IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>().InsertAsync(
				await GetRequiredService<ShikshaMitra.TenantSettings.AcademicSetups.DivisionManager>().CreateAsync(student.StandardId!.Value, "Current year only", 30), true);
			divisionId = division.Id;
			var nextYear = await _academicYearRepo.InsertAsync(new ShikshaMitra.TenantSettings.AcademicYears.AcademicYear(Guid.NewGuid(),
				TestTenantId, "Legacy next year", new(2026, 4, 1), new(2027, 3, 31)), true);
			student.UpdateAcademicPlacement(nextYear.Id, student.AcademicBoardId!.Value, student.AcademicMediumId,
				student.AcademicMediumStageId!.Value, student.StandardId!.Value, divisionId); student.AssignRollNo(87);
			await students.UpdateAsync(student, true);
		});
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var row = (await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 100 })).Items.Single(x => x.StudentId == input.StudentId);
		row.DivisionId.ShouldBeNull(); row.RollNumber.ShouldBeNull();
		(await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, DivisionId = divisionId, MaxResultCount = 100 }))
			.Items.ShouldNotContain(x => x.StudentId == input.StudentId);
	}

	[Fact]
	public async Task Assignment_policy_options_do_not_materialize_superseded_or_inactive_snapshots()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var versions = GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Policies.FinancePolicyVersion, Guid>>();
		var activePolicy = Guid.NewGuid(); var archivedPolicy = Guid.NewGuid();
		var old = new System.Collections.Generic.List<Guid>();
		foreach (var policy in new[] { activePolicy, archivedPolicy })
			for (var revision = 1; revision <= 12; revision++)
			{
				var id = Guid.NewGuid(); if (revision < 12 || policy == archivedPolicy) old.Add(id);
				await versions.InsertAsync(new(id, TestTenantId, "refund", policy, input.AcademicYearId, revision,
					policy == activePolicy ? "Active choice" : "Archived choice", revision == 12 && policy == archivedPolicy ? "Archived" : "Active", "{}", "changed"), true);
			}
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			var result = await GetRequiredService<IStudentFeeStructureAppService>().GetPolicyOptionsAsync(input.StudentId, input.AcademicYearId);
			result.ShouldContain(x => x.PolicyId == activePolicy && x.Version == 12);
			result.ShouldNotContain(x => x.PolicyId == archivedPolicy);
			db.ChangeTracker.Entries<ShikshaMitra.Domain.Finance.Policies.FinancePolicyVersion>()
				.ShouldNotContain(x => old.Contains(x.Entity.Id), "superseded or inactive snapshots must be excluded in SQL");
		});
	}

	[Fact]
	public async Task Assignment_context_view_omits_edit_snapshot_and_blocker_dependency_reads()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 1000m);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken; await service.AssignAsync(input);
		// Deserializing keeps this regression executable against the prior contract.
		var request = System.Text.Json.JsonSerializer.Deserialize<ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.AssignmentContextsInput>(
			System.Text.Json.JsonSerializer.Serialize(new { input.AcademicYearId, StudentIds = new[] { input.StudentId }, IncludeEditDetails = false }))!;
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var context = (await service.GetContextsAsync(request)).Single();
			context.Status.ShouldBe("Assigned"); context.AssignmentId.ShouldNotBeNull();
			context.AssignedAssignment.ShouldBeNull(); context.CanEditStructure.ShouldBeFalse();
			reads.Commands.ShouldNotContain(sql => sql.Contains("Finance_DueFees", StringComparison.Ordinal));
		});
		var details = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		details.CanEditStructure.ShouldBeTrue(); details.Assignment!.ProratedTotal.ShouldBe(1000m);
	}
}
