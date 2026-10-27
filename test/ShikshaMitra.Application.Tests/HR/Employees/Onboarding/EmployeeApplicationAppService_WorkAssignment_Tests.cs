using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public partial class EmployeeApplicationAppService_Tests
{
	[Fact]
	public async Task Finalization_revalidates_saved_specific_assignment_before_employee_creation()
	{
		using var admin = GetRequiredService<ICurrentPrincipalAccessor>().Change(await BuildAdminPrincipalAsync());
		await CreateDefaultPoliciesAsync();
		var submitted = await CreateSubmittedApplicationAsync("stale-work-assignment");
		var application = await _applicationRepository.GetAsync(submitted.Id);
		var years = await _academicYearRepository.GetListAsync();
		application.SetWorkAssignment(EmployeeWorkAssignmentModes.SpecificOrganizations,
			years[0].Id, years[0].Name, [Guid.NewGuid()], ["Deleted organization snapshot"]);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		var personal = await BuildPersonalInfoAsync();
		var salary = await BuildSalaryAsync();
		await Should.ThrowAsync<AbpValidationException>(() => _applications.FinalizeAsync(submitted.Id, new()
		{
			PersonalInfo = personal, Salary = salary, WorkAssignmentConfirmed = true
		}));
		(await _employeeRepository.GetListAsync(x => x.Email == personal.Email)).ShouldBeEmpty();
		(await _applicationRepository.GetAsync(submitted.Id)).FinalizedEmployeeId.ShouldBeNull();
	}

	[Theory]
	[InlineData(EmployeeWorkAssignmentModes.SchoolWide)]
	[InlineData(EmployeeWorkAssignmentModes.DecideLater)]
	public async Task Invite_and_public_submission_preserve_assignment_without_granting_scope(string mode)
	{
		var before = await _scopeRepository.GetCountAsync();
		var invite = await _applications.CreateAsync(new()
		{
			Email = $"assignment.{Guid.NewGuid():N}@example.com", PositionAppliedFor = "Peon",
			DepartmentAppliedFor = "CBSE",
			WorkAssignment = new() { Mode = mode }
		});
		invite.Application.WorkAssignment!.Mode.ShouldBe(mode);
		(await _applications.GetPublicAsync(invite.Token)).WorkAssignment!.Mode.ShouldBe(mode);
		(await _applications.GetListAsync(new() { Search = invite.Application.Email })).Items[0].WorkAssignment!.Mode.ShouldBe(mode);
		var submitted = await _applications.SubmitPublicAsync(invite.Token, BuildSubmitDto(invite.Application.Email!),
			new EmployeeApplicationFileInput { FileName = "resume.pdf", ContentType = "application/pdf", Content = BuildMinimalPdf(), Size = BuildMinimalPdf().Length },
			new EmployeeApplicationFileInput { FileName = "photo.png", ContentType = "image/png", Content = BuildMinimalPng(), Size = BuildMinimalPng().Length });
		submitted.WorkAssignment!.Mode.ShouldBe(mode);
		submitted.DepartmentAppliedFor.ShouldBe("CBSE");
		(await _scopeRepository.GetCountAsync()).ShouldBe(before);
	}

	[Fact]
	public async Task School_wide_finalization_requires_confirmation_and_does_not_create_scope()
	{
		using var admin = GetRequiredService<ICurrentPrincipalAccessor>().Change(await BuildAdminPrincipalAsync());
		await CreateDefaultPoliciesAsync();
		var submitted = await CreateSubmittedApplicationAsync("school-wide");
		var personal = await BuildPersonalInfoAsync();
		personal.EmployeeGroup = EmployeeGroupType.NonTeaching;
		var input = new FinalizeEmployeeApplicationDto
		{
			PersonalInfo = personal, Salary = await BuildSalaryAsync(),
			WorkAssignment = new() { Mode = EmployeeWorkAssignmentModes.SchoolWide }
		};
		await Should.ThrowAsync<AbpValidationException>(() => _applications.FinalizeAsync(submitted.Id, input));
		(await _employeeRepository.GetListAsync(x => x.Email == personal.Email)).ShouldBeEmpty();
		var before = await _scopeRepository.GetCountAsync();
		input.WorkAssignmentConfirmed = true;
		var result = await _applications.FinalizeAsync(submitted.Id, input);
		result.Application.WorkAssignment!.Mode.ShouldBe(EmployeeWorkAssignmentModes.SchoolWide);
		(await _scopeRepository.GetCountAsync()).ShouldBe(before);
	}

	[Fact]
	public async Task School_wide_does_not_bypass_teaching_academic_permission_validation()
	{
		using var admin = GetRequiredService<ICurrentPrincipalAccessor>().Change(await BuildAdminPrincipalAsync());
		await CreateDefaultPoliciesAsync();
		var submitted = await CreateSubmittedApplicationAsync("school-wide-teaching");
		var personal = await BuildPersonalInfoAsync();
		personal.EmployeeGroup = EmployeeGroupType.Teaching;
		await Should.ThrowAsync<AbpValidationException>(async () => await _applications.FinalizeAsync(submitted.Id, new()
		{
			PersonalInfo = personal, Salary = await BuildSalaryAsync(),
			WorkAssignment = new() { Mode = EmployeeWorkAssignmentModes.SchoolWide }, WorkAssignmentConfirmed = true
		}));
		(await _employeeRepository.GetListAsync(x => x.Email == personal.Email)).ShouldBeEmpty();
	}
}
