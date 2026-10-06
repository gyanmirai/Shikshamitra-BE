using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[Trait("Module", "Students")]
[Trait("Tier", "Critical")]
public sealed class StudentPortalLoginApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("credential-emails", ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData("documents/override", ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task Related_permissions_do_not_allow_email_sending_or_document_override(string route, string grantedPermissions)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grantedPermissions);
		using var response = await client.PostAsJsonAsync($"/api/student/onboarding/{Guid.NewGuid()}/{route}",
			new { accountType = "student", note = "Permission regression", reason = "Permission regression", concurrencyStamp = "test-stamp", overrideAll = true });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Temporary_credentials_require_password_proof_then_change_and_exit_denies_refresh()
	{
		var id = Guid.NewGuid(); var userId = Guid.NewGuid(); var studentId = Guid.NewGuid();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var username = "portal-token-" + id.ToString("N"); var email = username + "@example.test";
		var clientId = "portal-test-" + id.ToString("N"); var phone = Random.Shared.NextInt64(9000000000, 9999999999).ToString();
		await InTenantAsync(async services =>
		{
			var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
			await applications.CreateAsync(new OpenIddictApplicationDescriptor
			{
				ClientId = clientId, ClientType = OpenIddictConstants.ClientTypes.Public,
				Permissions = { OpenIddictConstants.Permissions.Endpoints.Token,
					OpenIddictConstants.Permissions.GrantTypes.Password, OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
					OpenIddictConstants.Permissions.Scopes.Profile, OpenIddictConstants.Permissions.Scopes.Email,
					OpenIddictConstants.Permissions.Prefixes.Scope + "ShikshaMitra" }
			});
			var roles = services.GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Student")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", tenantId))).Succeeded.ShouldBeTrue();
			var users = services.GetRequiredService<IdentityUserManager>();
			var user = new IdentityUser(userId, username, email, tenantId); user.SetPhoneNumber(phone, false);
			(await users.CreateAsync(user, "Initial-Test-Secret-29!")).Succeeded.ShouldBeTrue();
			(await users.AddToRoleAsync(user, "Student")).Succeeded.ShouldBeTrue();
			user.SetShouldChangePasswordOnNextLogin(true); (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
			await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new(studentId, tenantId,
				"Token", "", "Student", Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
				Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId, status: StudentLifeCycleStatusEnum.Enrolled), true);
			await services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), tenantId,
				studentId, userId, "student", "self", Guid.Parse(TestAuthHandler.TestUserId)), true);
		});
		using var client = CreateAnonymousClient(); client.BaseAddress = new Uri("https://localhost"); client.DefaultRequestHeaders.Add("__tenant", tenantId.ToString());
		foreach (var missingUsername in new[] { "", " " })
		{
			var missing = await TokenAsync(client, clientId, missingUsername, "Initial-Test-Secret-29!");
			missing.Status.ShouldBe(HttpStatusCode.BadRequest);
			missing.Body.TryGetProperty("access_token", out _).ShouldBeFalse();
			missing.Body.TryGetProperty("changePasswordToken", out _).ShouldBeFalse();
		}
		var wrong = await TokenAsync(client, clientId, email, "Wrong-Test-Secret-29!");
		wrong.Status.ShouldBe(HttpStatusCode.BadRequest); wrong.Body.GetProperty("error").GetString().ShouldBe("invalid_grant");
		wrong.Body.TryGetProperty("changePasswordToken", out _).ShouldBeFalse();
		await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>(); var user = await users.GetByIdAsync(userId);
			(await users.SetLockoutEnabledAsync(user, true)).Succeeded.ShouldBeTrue();
			(await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded.ShouldBeTrue();
		});
		var locked = await TokenAsync(client, clientId, email, "Initial-Test-Secret-29!");
		locked.Status.ShouldBe(HttpStatusCode.BadRequest); locked.Body.TryGetProperty("changePasswordToken", out _).ShouldBeFalse();
		await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>();
			(await users.SetLockoutEndDateAsync(await users.GetByIdAsync(userId), null)).Succeeded.ShouldBeTrue();
		});
		var initial = await TokenAsync(client, clientId, email, "Initial-Test-Secret-29!");
		initial.Status.ShouldBe(HttpStatusCode.BadRequest);
		initial.Body.GetProperty("error_description").GetString().ShouldBe("ShouldChangePasswordOnNextLogin");
		var changeToken = initial.Body.GetProperty("changePasswordToken").GetString()!;
		var changed = await TokenAsync(client, clientId, email, "Initial-Test-Secret-29!", new()
		{
			["ChangePasswordToken"] = changeToken, ["NewPassword"] = "Owner-New-Test-Secret-29!"
		});
		changed.Status.ShouldBe(HttpStatusCode.OK); changed.Body.GetProperty("access_token").GetString().ShouldNotBeNullOrWhiteSpace();
		await InTenantAsync(async services =>
		{
			var user = await services.GetRequiredService<IdentityUserManager>().GetByIdAsync(userId);
			user.EmailConfirmed.ShouldBeTrue(); user.ShouldChangePasswordOnNextLogin.ShouldBeFalse();
		});
		(await TokenAsync(client, clientId, phone, "Owner-New-Test-Secret-29!")).Status.ShouldBe(HttpStatusCode.OK);
		await InTenantAsync(async services =>
		{
			var repo = services.GetRequiredService<IRepository<Student, Guid>>();
			var student = await (await repo.GetQueryableAsync()).IgnoreAutoIncludes().AsSplitQuery().SingleAsync(value => value.Id == studentId);
			student.ChangeOperationalStatus(StudentStatus.Left, "Exit access regression"); await repo.UpdateAsync(student, true);
			await services.GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(x => x.StudentId == studentId);
		});
		(await TokenAsync(client, clientId, username, "Owner-New-Test-Secret-29!")).Status.ShouldBe(HttpStatusCode.BadRequest);
		using var refreshResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["client_id"] = clientId, ["grant_type"] = "refresh_token", ["refresh_token"] = changed.Body.GetProperty("refresh_token").GetString()!
		}));
		refreshResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var staffUsername = "staff-" + id.ToString("N"); var staffPhone = Random.Shared.NextInt64(9000000000, 9999999999).ToString();
		await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>();
			var staff = new IdentityUser(Guid.NewGuid(), staffUsername, staffUsername + "@example.test", tenantId);
			staff.SetEmailConfirmed(true); staff.SetPhoneNumber(staffPhone, false);
			(await users.CreateAsync(staff, "Staff-Test-Secret-29!")).Succeeded.ShouldBeTrue();
			(await users.AddToRoleAsync(staff, "Teacher")).Succeeded.ShouldBeTrue();
		});
		// Staff remain on ABP's normal username/email path without child-link checks.
		(await TokenAsync(client, clientId, staffUsername, "Staff-Test-Secret-29!")).Status.ShouldBe(HttpStatusCode.OK);
		(await TokenAsync(client, clientId, staffPhone, "Staff-Test-Secret-29!")).Status.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await work.CompleteAsync();
	}

	private static async Task<(HttpStatusCode Status, JsonElement Body)> TokenAsync(HttpClient client, string clientId, string username, string password, Dictionary<string, string>? extra = null)
	{
		var form = extra ?? new(); form["client_id"] = clientId; form["grant_type"] = "password";
		form["username"] = username; form["password"] = password; form["scope"] = "openid profile email offline_access ShikshaMitra";
		using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
		var content = await response.Content.ReadAsStringAsync();
		content.TrimStart().StartsWith("{").ShouldBeTrue($"Expected token JSON; status {(int)response.StatusCode}: {content}");
		using var document = JsonDocument.Parse(content);
		return (response.StatusCode, document.RootElement.Clone());
	}
}
