using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public partial class EmployeeCreationFamilyAccessApi_Tests
{
	[Fact]
	public async Task Scoped_Staff_Creator_Completes_Academic_Permission_Without_Global_Scope_Grants()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var input = await AcademicRequestAsync(creator, employeeId);
		(await creator.PostAsJsonAsync("/api/app/user-scope/bulk-assign", input, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await creator.GetAsync($"/api/app/user-scope/user-scope-hierarchy?userId={input.UserIds[0]}"))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var url = AcademicUrl(employeeId);
		var result = await creator.PutAsJsonAsync(url, input, JsonOptions);
		result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
		(await result.Content.ReadFromJsonAsync<BulkScopeAssignmentResultDto>(JsonOptions))!
			.AssignmentsCreated.ShouldBe(1);
		var read = await creator.GetAsync($"{url}?academicYearId={Factory.SeededAcademicYearId}");
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		(await read.Content.ReadFromJsonAsync<UserScopeHierarchyDto>(JsonOptions))!.UserId.ShouldBe(input.UserIds[0]);
		var retry = await creator.PutAsJsonAsync(url, input, JsonOptions);
		retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
		(await retry.Content.ReadFromJsonAsync<BulkScopeAssignmentResultDto>(JsonOptions))!.DeletedAssignments.ShouldBe(1);
		(await ReadSavedAcademicAssignmentsAsync(input.UserIds[0])).ShouldHaveSingleItem();
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
			.GetListAsync(x => x.EmployeeId == employeeId)).ShouldHaveSingleItem().EducationMediumStageId
			.ShouldBe(Factory.SeededMediumStageId);
	}

	[Theory]
	[InlineData(StaffAdd)]
	[InlineData(StaffEdit)]
	public async Task Academic_Draft_Assignment_Requires_Current_Staff_Actions(string removedPermission)
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var input = await AcademicRequestAsync(creator, employeeId);
		creator.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		creator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"{StaffView},{(removedPermission == StaffAdd ? StaffEdit : StaffAdd)}");
		(await creator.PutAsJsonAsync(AcademicUrl(employeeId), input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await ReadSavedAcademicAssignmentsAsync(input.UserIds[0])).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("board")]
	[InlineData("year")]
	[InlineData("mixed")]
	[InlineData("target")]
	[InlineData("tenant")]
	[InlineData("other-creator")]
	[InlineData("active")]
	[InlineData("missing-scope")]
	[InlineData("manual-assignment")]
	public async Task Academic_Assignment_Rejects_Escalation_And_Preserves_Existing_Rows(string scenario)
	{
		using var creator = await CreateScopedTeacherAsync(withScope: scenario != "missing-scope");
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var input = await AcademicRequestAsync(creator, employeeId);
		var targetUserId = input.UserIds[0];
		if (scenario == "board") input.Scopes = [new() { EducationBoardId = Factory.SeededBoardId }];
		if (scenario == "year") input.Scopes = [new()];
		if (scenario == "mixed") input.Scopes.Add(new() { EducationBoardId = Factory.SeededBoardId });
		if (scenario == "target") input.UserIds = [Guid.Parse(TestAuthHandler.TestUserId)];
		if (scenario == "tenant") creator.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		if (scenario == "active") await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, employeeId);
		if (scenario == "manual-assignment")
		{
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			await scope.ServiceProvider.GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), userId: targetUserId,
					academicYearId: Factory.SeededAcademicYearId, educationMediumStageId: Factory.SeededMediumStageId,
					description: "Manual protected assignment"), autoSave: true);
			await unit.CompleteAsync();
		}
		using var other = await CreateScopedTeacherAsync();
		var actor = scenario == "other-creator" ? other : creator;
		var result = await actor.PutAsJsonAsync(AcademicUrl(employeeId), input, JsonOptions);
		result.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
		var saved = await ReadSavedAcademicAssignmentsAsync(targetUserId);
		if (scenario == "manual-assignment") saved.ShouldHaveSingleItem().Description.ShouldBe("Manual protected assignment");
		else saved.ShouldBeEmpty();
	}

	[Fact]
	public async Task Invalid_Academic_Hierarchy_Rolls_Back_An_Existing_Draft_Assignment()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var input = await AcademicRequestAsync(creator, employeeId);
		(await creator.PutAsJsonAsync(AcademicUrl(employeeId), input, JsonOptions)).EnsureSuccessStatusCode();
		input.Scopes[0].DivisionId = Guid.NewGuid();
		input.Scopes[0].StandardId = Guid.NewGuid();
		var response = await creator.PutAsJsonAsync(AcademicUrl(employeeId), input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await ReadSavedAcademicAssignmentsAsync(input.UserIds[0])).ShouldHaveSingleItem().DivisionId.ShouldBeNull();
	}

	private static string AcademicUrl(Guid employeeId) => $"/api/employees/{employeeId}/academic-permission";

	private async Task<BulkScopeAssignmentDto> AcademicRequestAsync(HttpClient client, Guid employeeId)
	{
		var personal = await Client.GetFromJsonAsync<PersonalInfo.PersonalInfoResponse>($"/api/employees/{employeeId}/personal-info", JsonOptions);
		return new BulkScopeAssignmentDto
		{
			UserIds = [personal!.UserId!.Value], AcademicYearId = Factory.SeededAcademicYearId,
			Scopes = [new() { EducationBoardId = Factory.SeededBoardId, EducationMediumId = Factory.SeededMediumId,
				EducationMediumStageId = Factory.SeededMediumStageId }]
		};
	}

	private async Task<System.Collections.Generic.List<UserScopeAssignment>> ReadSavedAcademicAssignmentsAsync(Guid userId)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		return await scope.ServiceProvider.GetRequiredService<IUserScopeAssignmentRepository>().GetListAsync(x => x.UserId == userId);
	}
}
