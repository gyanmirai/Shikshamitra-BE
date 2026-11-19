using System;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EmployeeAccountEmailApi_Tests
{
	[PayrollRegistrationPostgresFact]
	public async Task Controlled_account_email_change_updates_the_employee_and_identity_together()
	{
		using var factory = new PayslipPostgresTestFactory();
		var api = new EsiPayrollWorkflowScenario(factory).Api;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "AccountEmailRepair");
		var url = $"/api/employees/{employee.Id}/account-email";
		var read = await api.GetAsync(url);
		await EsiPayrollWorkflowScenario.RequireSuccess(read);
		var before = await read.Content.ReadFromJsonAsync<JsonElement>();
		var email = "controlled-repair-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local";
		var input = new
		{
			Email = email,
			Reason = "HR verified the employee's corrected address",
			ConfirmEmployeeVerified = true,
			ConcurrencyToken = before.GetProperty("concurrencyToken").GetString(),
			IdempotencyKey = Guid.NewGuid()
		};
		// Lose the HTTP acknowledgement only after the real PostgreSQL-backed write completes.
		using var interruptedApi = factory.CreateDefaultClient(new CommittedAccountEmailResponseLoss(url));
		foreach (var header in api.DefaultRequestHeaders)
			interruptedApi.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
		await Should.ThrowAsync<HttpRequestException>(() => interruptedApi.PutAsJsonAsync(url, input));
		var committed = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		committed.EmployeeEmail.ShouldBe(email);
		committed.AccountEmail.ShouldBe(email);
		committed.ConcurrencyToken.ShouldNotBe(input.ConcurrencyToken);
		var committedStamp = await InDatabaseAsync(factory, async services =>
			(await services.GetRequiredService<IdentityUserManager>().FindByIdAsync(committed.UserId.ToString()))!.SecurityStamp);
		var saved = await api.PutAsJsonAsync(url, input); // The retained payload/key acknowledge the existing write.
		await EsiPayrollWorkflowScenario.RequireSuccess(saved);
		var after = await saved.Content.ReadFromJsonAsync<JsonElement>();
		after.GetProperty("employeeEmail").GetString().ShouldBe(email);
		after.GetProperty("accountEmail").GetString().ShouldBe(email);
		after.GetProperty("userName").GetString().ShouldBe(email);
		after.GetProperty("concurrencyToken").GetString().ShouldBe(committed.ConcurrencyToken);
		await InDatabaseAsync(factory, async services =>
		{
			var user = (await services.GetRequiredService<IdentityUserManager>().FindByIdAsync(committed.UserId.ToString()))!;
			user.SecurityStamp.ShouldBe(committedStamp);
			var audit = await services.GetRequiredService<IRepository<HrAuditEvent, Guid>>().GetListAsync(x =>
				x.EntityId == employee.Id && x.Action == "account_email_changed" && x.CorrelationId == input.IdempotencyKey.ToString("D"));
			audit.Count.ShouldBe(1);
			return true;
		});
		factory.Messages.ShouldBeEmpty();

		var savedAccount = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		var (oldStamp, oldPasswordHash) = await InDatabaseAsync(factory, async services =>
		{
			var user = (await services.GetRequiredService<IdentityUserManager>().FindByIdAsync(savedAccount.UserId.ToString()))!;
			return (user.SecurityStamp, user.PasswordHash);
		});
		var request = new ChangeEmployeeAccountEmailDto
		{
			Email = "verified-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local",
			Reason = "Verified a second address with the employee",
			ConfirmEmployeeVerified = true,
			ConcurrencyToken = savedAccount.ConcurrencyToken,
			IdempotencyKey = Guid.NewGuid()
		};
		var tokenId = await InDatabaseAsync(factory, async services =>
		{
			var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
			var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
			{
				Subject = savedAccount.UserId.ToString(),
				Status = OpenIddictConstants.Statuses.Valid,
				Type = OpenIddictConstants.TokenTypeHints.RefreshToken
			});
			return (await tokens.GetIdAsync(token))!;
		});
		var authorizationId = await InDatabaseAsync(factory, async services =>
		{
			var manager = services.GetRequiredService<IOpenIddictAuthorizationManager>();
			var authorization = await manager.CreateAsync(new OpenIddictAuthorizationDescriptor
			{
				Subject = savedAccount.UserId.ToString(),
				Status = OpenIddictConstants.Statuses.Valid,
				Type = OpenIddictConstants.AuthorizationTypes.Permanent
			});
			return (await manager.GetIdAsync(authorization))!;
		});
		// A retry and simultaneous duplicate request produce one account change and one audit record.
		var concurrent = await Task.WhenAll(api.PutAsJsonAsync(url, request), api.PutAsJsonAsync(url, request));
		foreach (var response in concurrent) await EsiPayrollWorkflowScenario.RequireSuccess(response);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(url, request));
		var changed = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		changed.EmployeeEmail.ShouldBe(request.Email);
		changed.AccountEmail.ShouldBe(request.Email);
		changed.ConcurrencyToken.ShouldNotBe(savedAccount.ConcurrencyToken);
		await InDatabaseAsync(factory, async services =>
		{
			var user = (await services.GetRequiredService<IdentityUserManager>().FindByIdAsync(savedAccount.UserId.ToString()))!;
			user.EmailConfirmed.ShouldBeTrue(); user.SecurityStamp.ShouldNotBe(oldStamp); user.PasswordHash.ShouldBe(oldPasswordHash);
			(await services.GetRequiredService<IdentityUserManager>().FindByNameAsync(request.Email))!.Id.ShouldBe(savedAccount.UserId);
			var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
			(await tokens.GetStatusAsync((await tokens.FindByIdAsync(tokenId))!)).ShouldBe(OpenIddictConstants.Statuses.Revoked);
			var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
			(await authorizations.GetStatusAsync((await authorizations.FindByIdAsync(authorizationId))!)).ShouldBe(OpenIddictConstants.Statuses.Revoked);
			var events = await services.GetRequiredService<IRepository<HrAuditEvent, Guid>>().GetListAsync(x => x.EntityId == employee.Id && x.Action == "account_email_changed");
			events.Count.ShouldBe(2);
			var entry = events.Single(x => x.CorrelationId == request.IdempotencyKey.ToString("D"));
			entry.Reason.ShouldBe(request.Reason); entry.ActorUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			entry.ChangedFieldsJson.ShouldNotBeNull().ShouldContain(request.Email);
			return true;
		});
		var changedPayload = new ChangeEmployeeAccountEmailDto
		{
			Email = request.Email,
			Reason = "Different reason for the same request key",
			ConfirmEmployeeVerified = true,
			ConcurrencyToken = request.ConcurrencyToken,
			IdempotencyKey = request.IdempotencyKey
		};
		(await api.PutAsJsonAsync(url, changedPayload)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request.IdempotencyKey = Guid.NewGuid(); // An old read cannot authorize a new write.
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var other = await EmployeeTestHelper.CreateEmployeeAsync(api, "EmailDuplicate");
		request.ConcurrencyToken = changed.ConcurrencyToken; request.Email = other.Email!;
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request.Email = changed.AccountEmail; request.ConfirmEmployeeVerified = false;
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request.ConfirmEmployeeVerified = true; request.Reason = "     ";
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request.Reason = "HR verified this address"; request.Email = "Name <bad@example.test>";
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		// Generic profile editing still cannot bypass the account-change controls.
		var personal = (await api.GetFromJsonAsync<PersonalInfoResponse>($"/api/employees/{employee.Id}/personal-info"))!;
		personal.Email = other.Email;
		var ordinary = await api.PutAsJsonAsync($"/api/employees/{employee.Id}/personal-info", personal);
		ordinary.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await ordinary.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EmployeeEmailCannotBeChanged);
		var identity = (await api.GetFromJsonAsync<IdentityUserDto>($"/api/identity/users/{savedAccount.UserId}"))!;
		var genericIdentityUpdate = new IdentityUserUpdateDto
		{
			UserName = identity.UserName,
			Email = "generic-bypass-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local",
			Name = identity.Name,
			Surname = identity.Surname,
			IsActive = identity.IsActive,
			LockoutEnabled = identity.LockoutEnabled,
			ConcurrencyStamp = identity.ConcurrencyStamp
		};
		var genericIdentityResult = await api.PutAsJsonAsync($"/api/identity/users/{savedAccount.UserId}", genericIdentityUpdate);
		genericIdentityResult.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!.AccountEmail.ShouldBe(changed.AccountEmail);
		// A normal Identity edit that retains the email is still supported.
		genericIdentityUpdate.Email = changed.AccountEmail;
		genericIdentityUpdate.Name = "Controlled workflow test display name";
		genericIdentityUpdate.ConcurrencyStamp = (await api.GetFromJsonAsync<IdentityUserDto>($"/api/identity/users/{savedAccount.UserId}"))!.ConcurrencyStamp;
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/identity/users/{savedAccount.UserId}", genericIdentityUpdate));

		// Both HTTP and unproxied application calls require each exact permission.
		foreach (var permission in new[] { "HR.Employee.EmployeeList.Edit", "AbpIdentity.Users.Update" })
		{
			using var deniedApi = factory.CreateClient();
			deniedApi.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
			(await deniedApi.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			(await deniedApi.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			await AssertDirectDeniedAsync(factory, permission, employee.Id, request);
		}
		using (var outsideApi = factory.CreateClient())
		{
			outsideApi.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
			(await outsideApi.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			(await outsideApi.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest); // Invalid address is rejected without a write.
			request.Email = "outside@example.test";
			(await outsideApi.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		using (var foreignApi = factory.CreateClient())
		{
			foreignApi.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
			(await foreignApi.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			(await foreignApi.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		}

		// Failure after Identity persistence rolls back both records, credentials and the audit.
		var beforeRollback = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		var rollbackTokenId = await InDatabaseAsync(factory, async services =>
		{
			var manager = services.GetRequiredService<IOpenIddictTokenManager>();
			var token = await manager.CreateAsync(new OpenIddictTokenDescriptor
			{
				Subject = savedAccount.UserId.ToString(),
				Status = OpenIddictConstants.Statuses.Valid,
				Type = OpenIddictConstants.TokenTypeHints.RefreshToken
			});
			return (await manager.GetIdAsync(token))!;
		});
		var rollbackAuthorizationId = await InDatabaseAsync(factory, async services =>
		{
			var manager = services.GetRequiredService<IOpenIddictAuthorizationManager>();
			var authorization = await manager.CreateAsync(new OpenIddictAuthorizationDescriptor
			{
				Subject = savedAccount.UserId.ToString(),
				Status = OpenIddictConstants.Statuses.Valid,
				Type = OpenIddictConstants.AuthorizationTypes.Permanent
			});
			return (await manager.GetIdAsync(authorization))!;
		});
		request.ConcurrencyToken = beforeRollback.ConcurrencyToken; request.Email = "rollback-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local";
		request.IdempotencyKey = Guid.NewGuid();
		factory.Barrier.FailNextCommand("INSERT INTO \"HR_AuditEvents\"");
		(await api.PutAsJsonAsync(url, request)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		var afterRollback = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		afterRollback.ConcurrencyToken.ShouldBe(beforeRollback.ConcurrencyToken);
		afterRollback.EmployeeEmail.ShouldBe(beforeRollback.EmployeeEmail);
		afterRollback.AccountEmail.ShouldBe(beforeRollback.AccountEmail);
		await InDatabaseAsync(factory, async services =>
		{
			var manager = services.GetRequiredService<IOpenIddictTokenManager>();
			(await manager.GetStatusAsync((await manager.FindByIdAsync(rollbackTokenId))!)).ShouldBe(OpenIddictConstants.Statuses.Valid);
			var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
			(await authorizations.GetStatusAsync((await authorizations.FindByIdAsync(rollbackAuthorizationId))!)).ShouldBe(OpenIddictConstants.Statuses.Valid);
			return true;
		});
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(url, request));

		// Preserve a distinct established username rather than renaming an alias.
		var alias = "employee-alias-" + Guid.NewGuid().ToString("N");
		await InDatabaseAsync(factory, async services =>
		{
			var manager = services.GetRequiredService<IdentityUserManager>();
			var user = (await manager.FindByIdAsync(savedAccount.UserId.ToString()))!;
			(await manager.SetUserNameAsync(user, alias)).Succeeded.ShouldBeTrue();
			return true;
		});
		var beforeAlias = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		request.ConcurrencyToken = beforeAlias.ConcurrencyToken; request.IdempotencyKey = Guid.NewGuid();
		request.Email = "alias-address-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local";
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(url, request));
		(await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!.UserName.ShouldBe(alias);
		// Different requests from the same read serialize: one wins and the other remains stale.
		var raceBaseline = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		var first = new ChangeEmployeeAccountEmailDto
		{
			Email = "race-a-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local",
			Reason = "HR verified the first candidate address",
			ConfirmEmployeeVerified = true,
			ConcurrencyToken = raceBaseline.ConcurrencyToken,
			IdempotencyKey = Guid.NewGuid()
		};
		var second = new ChangeEmployeeAccountEmailDto
		{
			Email = "race-b-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local",
			Reason = "HR verified the second candidate address",
			ConfirmEmployeeVerified = true,
			ConcurrencyToken = raceBaseline.ConcurrencyToken,
			IdempotencyKey = Guid.NewGuid()
		};
		var competing = await Task.WhenAll(api.PutAsJsonAsync(url, first), api.PutAsJsonAsync(url, second));
		competing.Count(x => x.IsSuccessStatusCode).ShouldBe(1);
		competing.Count(x => x.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1);
		var winner = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!;
		winner.EmployeeEmail.ShouldBe(winner.AccountEmail); winner.UserName.ShouldBe(alias);
		// A delayed replay of the first acknowledged change cannot restore its older address.
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(url, input));
		(await api.GetFromJsonAsync<EmployeeAccountEmailDto>(url))!.AccountEmail.ShouldBe(winner.AccountEmail);
		await InDatabaseAsync(factory, async services =>
		{
			(await services.GetRequiredService<IRepository<HrAuditEvent, Guid>>()
				.CountAsync(x => x.EntityId == employee.Id && x.Action == "account_email_changed")).ShouldBe(5);
			return true;
		});
		factory.Messages.ShouldBeEmpty();
	}

	private static async Task<T> InDatabaseAsync<T>(PayslipPostgresTestFactory factory, Func<IServiceProvider, Task<T>> action)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var result = await action(scope.ServiceProvider);
		await unit.CompleteAsync();
		return result;
	}

	private sealed class CommittedAccountEmailResponseLoss(string path) : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = await base.SendAsync(request, cancellationToken);
			if (request.Method != HttpMethod.Put || request.RequestUri?.AbsolutePath != path) return response;
			response.EnsureSuccessStatusCode();
			await response.Content.ReadAsByteArrayAsync(cancellationToken);
			response.Dispose();
			throw new HttpRequestException("Synthetic response lost after committed account-email write.");
		}
	}

	private static async Task AssertDirectDeniedAsync(PayslipPostgresTestFactory factory, string permission, Guid employeeId, ChangeEmployeeAccountEmailDto input)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var http = services.GetRequiredService<IHttpContextAccessor>();
		var previous = http.HttpContext;
		var principal = new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId), new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)
		], "account-email-direct"));
		http.HttpContext = new DefaultHttpContext { User = principal };
		http.HttpContext.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = permission;
		try
		{
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var actor = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
			var service = ActivatorUtilities.CreateInstance<EmployeeAccountEmailAppService>(services);
			service.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
			service.GetType().ShouldBe(typeof(EmployeeAccountEmailAppService));
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(employeeId));
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.ChangeAsync(employeeId, input));
		}
		finally { http.HttpContext = previous; }
	}
}
