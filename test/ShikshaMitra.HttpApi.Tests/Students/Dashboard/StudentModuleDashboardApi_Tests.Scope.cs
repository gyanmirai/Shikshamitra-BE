using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	[Fact]
	public async Task Division_scope_does_not_expand_to_a_sibling_division_in_the_same_standard()
	{
		var id = await SeedUserAsync(); var divisionId = Guid.NewGuid();
		await WithTenantAsync(async services =>
		{
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId, "Dashboard narrow class", 100, "DASH");
			await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			divisionId = division.Id;
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: id, academicYearId: Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId, educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId, standardId: Factory.SeededStandardId, divisionId: divisionId), true);
			await SeedCaseAsync(services, id, divisionId);
			await SeedCaseAsync(services, id, Factory.SeededDivisionId);
		});
		using var client = Permissions(P.Dashboard.View, P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		var response = await client.GetAsync($"{Root}/sections/records?academicYearId={Factory.SeededAcademicYearId}");
		response.EnsureSuccessStatusCode();
		var section = await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
		section!.Metrics.Single(metric => metric.Id == "active").Value.ShouldBe(1);
		section.ClassStrength.Single().Students.ShouldBe(1);
	}

	[Fact]
	public async Task Parent_context_returns_only_verified_links_even_with_staff_grants_and_tenant_scope()
	{
		var id = await SeedUserAsync(); var ownStudentId = Guid.Empty;
		await WithTenantAsync(async services =>
		{
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
			var roles = services.GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Parent")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", tenant))).Succeeded.ShouldBeTrue();
			var users = services.GetRequiredService<IdentityUserManager>();
			var user = await users.GetByIdAsync(id); user.SetEmailConfirmed(true);
			(await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
			(await users.AddToRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: id), true);
			ownStudentId = (await SeedCaseAsync(services, id)).StudentId;
			var otherStudentId = (await SeedCaseAsync(services, id)).StudentId;
			var links = services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			await links.InsertAsync(new StudentPortalAccountLink(Guid.NewGuid(), tenant, ownStudentId, id, "guardian", "mother", Guid.Parse(TestAuthHandler.TestUserId)), true);
			await links.InsertAsync(new StudentPortalAccountLink(Guid.NewGuid(), tenant, otherStudentId, Guid.Parse(TestAuthHandler.TestUserId), "guardian", "father", Guid.Parse(TestAuthHandler.TestUserId)), true);
		});
		using var client = Permissions(P.Dashboard.View, P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		var context = await ReadContextAsync(client);
		context.MyStudents.Single().StudentId.ShouldBe(ownStudentId);
		context.Sections.ShouldBeEmpty(); context.Links.ShouldBeEmpty();
		await WithTenantAsync(async services =>
		{
			var links = services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			await links.DeleteAsync(link => link.UserId == id && link.StudentId == ownStudentId, true);
		});
		(await ReadContextAsync(client)).MyStudents.ShouldBeEmpty();
	}
}
