using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDeliveryAuthorityFactAttribute : FactAttribute
{
	public PayslipDeliveryAuthorityFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_AUTHORITY_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_AUTHORITY_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDeliveryAuthorityTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_AUTHORITY_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipDeliveryAuthority_Tests
{
	private const string SendPermission = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add;
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[PayslipDeliveryAuthorityFact]
	public async Task Worker_rechecks_persisted_direct_inherited_identity_and_scope_authority_before_sending()
	{
		using var factory = new PayslipDeliveryAuthorityTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipAuthority", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", Json))!.Single();
		document.RecipientEmail!.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal).ShouldBeTrue();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		factory.Messages.ShouldBeEmpty();

		// New identities and custom roles belong only to this fresh owned synthetic fixture.
		var roleName = "payslip-authority-" + Guid.NewGuid().ToString("N");
		var actorId = await CreateActorAsync(factory, roleName);
		using var actorApi = ActorClient(factory, actorId, roleName);
		var scopeAssignment = await AssignScopeAsync(api, actorId);
		await SetGrantAsync(api, "U", actorId.ToString(), true);
		await AssertRealPermissionAsync(factory, actorId, roleName, true); // Warm the real permission cache.
		var control = await QueueAsync(actorApi, document.Id);
		await ExecuteWithRealCheckerAsync(factory, control.Id);
		(await ResultAsync(api, historyUrl, control.Id)).Status.ShouldBe("PROVIDER_ACCEPTED");
		AssertCaptures(factory, document, 1);

		var revokedDirect = await QueueAsync(actorApi, document.Id);
		await SetGrantAsync(api, "U", actorId.ToString(), false);
		await AssertRealPermissionAsync(factory, actorId, roleName, false);
		await ExecuteWithRealCheckerAsync(factory, revokedDirect.Id);
		await AssertDeniedAsync(api, historyUrl, revokedDirect.Id);
		await ExecuteWithRealCheckerAsync(factory, revokedDirect.Id);
		AssertCaptures(factory, document, 1);

		await SetGrantAsync(api, "R", roleName, true);
		await SetGrantAsync(api, "U", actorId.ToString(), true);
		var inherited = await QueueAsync(actorApi, document.Id);
		await SetGrantAsync(api, "U", actorId.ToString(), false);
		await AssertRealPermissionAsync(factory, actorId, roleName, true);
		await ExecuteWithRealCheckerAsync(factory, inherited.Id);
		(await ResultAsync(api, historyUrl, inherited.Id)).Status.ShouldBe("PROVIDER_ACCEPTED");
		AssertCaptures(factory, document, 2);

		var revokedRole = await QueueAsync(actorApi, document.Id);
		await SetGrantAsync(api, "R", roleName, false);
		await AssertRealPermissionAsync(factory, actorId, roleName, false);
		await ExecuteWithRealCheckerAsync(factory, revokedRole.Id);
		await AssertDeniedAsync(api, historyUrl, revokedRole.Id);
		AssertCaptures(factory, document, 2);

		await SetGrantAsync(api, "R", roleName, true);
		var revokedScope = await QueueAsync(actorApi, document.Id);
		await RevokeScopeAsync(factory, scopeAssignment.Id);
		await AssertRealPermissionAsync(factory, actorId, roleName, true);
		await ExecuteWithRealCheckerAsync(factory, revokedScope.Id);
		await AssertDeniedAsync(api, historyUrl, revokedScope.Id);
		AssertCaptures(factory, document, 2);

		var restoredScope = await AssignScopeAsync(api, actorId);
		var roleChangedAfterClaim = await QueueAsync(actorApi, document.Id);
		await ExecuteWithRealCheckerAsync(factory, roleChangedAfterClaim.Id, async () =>
		{
			(await ResultAsync(api, historyUrl, roleChangedAfterClaim.Id)).Status.ShouldBe("PROCESSING");
			await SetGrantAsync(api, "R", roleName, false);
			await AssertRealPermissionAsync(factory, actorId, roleName, false);
		});
		await AssertDeniedAsync(api, historyUrl, roleChangedAfterClaim.Id);
		await ExecuteWithRealCheckerAsync(factory, roleChangedAfterClaim.Id);
		AssertCaptures(factory, document, 2);

		await SetGrantAsync(api, "R", roleName, true);
		var scopeChangedAfterClaim = await QueueAsync(actorApi, document.Id);
		await ExecuteWithRealCheckerAsync(factory, scopeChangedAfterClaim.Id, async () =>
		{
			(await ResultAsync(api, historyUrl, scopeChangedAfterClaim.Id)).Status.ShouldBe("PROCESSING");
			await RevokeScopeAsync(factory, restoredScope.Id);
			await AssertRealPermissionAsync(factory, actorId, roleName, true);
		});
		await AssertDeniedAsync(api, historyUrl, scopeChangedAfterClaim.Id);
		await ExecuteWithRealCheckerAsync(factory, scopeChangedAfterClaim.Id);
		AssertCaptures(factory, document, 2);

		await AssignScopeAsync(api, actorId);
		var inactive = await QueueAsync(actorApi, document.Id);
		await SetActorActiveAsync(factory, actorId, false);
		await ExecuteWithRealCheckerAsync(factory, inactive.Id);
		await AssertDeniedAsync(api, historyUrl, inactive.Id);
		AssertCaptures(factory, document, 2);

		await SetActorActiveAsync(factory, actorId, true);
		var identityChangedAfterClaim = await QueueAsync(actorApi, document.Id);
		await ExecuteWithRealCheckerAsync(factory, identityChangedAfterClaim.Id, async () =>
		{
			(await ResultAsync(api, historyUrl, identityChangedAfterClaim.Id)).Status.ShouldBe("PROCESSING");
			await SetActorActiveAsync(factory, actorId, false);
		});
		await AssertDeniedAsync(api, historyUrl, identityChangedAfterClaim.Id);
		await ExecuteWithRealCheckerAsync(factory, identityChangedAfterClaim.Id);
		AssertCaptures(factory, document, 2);

		var history = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!;
		history.Count.ShouldBe(9);
		history.ShouldAllBe(x => x.RequestedBy == actorId && !x.Automatic && x.DocumentId == document.Id);
		history.Count(x => x.Status == "PROVIDER_ACCEPTED").ShouldBe(2);
		history.Count(x => x.Status == "FAILED" && x.ErrorCategory == "ACCESS_REVOKED").ShouldBe(7);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
			.ShouldBe(document.Sha256);
		(await api.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", Json))!
			.Status.ShouldBe("Finalized");
	}

	private static async Task<Guid> CreateActorAsync(PayslipPostgresTestFactory factory, string roleName)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var roles = scope.ServiceProvider.GetRequiredService<IdentityRoleManager>();
		(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, EsiPayrollWorkflowScenario.Tenant))).Succeeded.ShouldBeTrue();
		var user = new IdentityUser(Guid.NewGuid(), roleName, roleName + "@test.shikshamitra.local", EsiPayrollWorkflowScenario.Tenant);
		var users = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
		(await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
		(await users.AddToRoleAsync(user, roleName)).Succeeded.ShouldBeTrue();
		await unit.CompleteAsync();
		return user.Id;
	}

	private static HttpClient ActorClient(PayslipPostgresTestFactory factory, Guid actor, string role)
	{
		var api = factory.CreateClient();
		api.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actor.ToString());
		api.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, role);
		api.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SendPermission);
		return api;
	}

	private static async Task<UserScopeAssignmentDto> AssignScopeAsync(HttpClient api, Guid actor)
	{
		var response = await api.PostAsJsonAsync("/api/app/user-scope/assign-scope", new CreateUserScopeAssignmentDto
		{
			UserId = actor,
			Description = "Fresh synthetic payslip delivery authority fixture"
		}, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		return (await response.Content.ReadFromJsonAsync<UserScopeAssignmentDto>(Json))!;
	}

	private static async Task SetGrantAsync(HttpClient editor, string provider, string key, bool granted)
	{
		var response = await editor.PutAsJsonAsync($"/api/permission-management/permissions?providerName={provider}&providerKey={Uri.EscapeDataString(key)}",
			new { permissions = new[] { new { name = SendPermission, isGranted = granted } } }, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
	}

	private static async Task<PayslipDeliveryDto> QueueAsync(HttpClient api, Guid document)
	{
		var response = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto
		{
			DocumentId = document,
			IdempotencyKey = Guid.NewGuid()
		}, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var delivery = (await response.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!;
		delivery.Status.ShouldBe("QUEUED");
		return delivery;
	}

	private static async Task ExecuteWithRealCheckerAsync(PayslipPostgresTestFactory factory, Guid delivery,
		Func<Task>? beforeSendFence = null)
	{
		using var scope = factory.Services.CreateScope();
		// The API bootstrap retains TestAuth; this worker uses the actual ABP provider/store/cache checker.
		// Do not model worker revocation through X-Test-Permissions or substitute a mock checker.
		var checker = ActivatorUtilities.CreateInstance<PermissionChecker>(scope.ServiceProvider);
		var fence = scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>();
		if (beforeSendFence != null) fence = new SendPhaseFence(fence, beforeSendFence);
		var worker = ActivatorUtilities.CreateInstance<PayslipDeliveryBackgroundJob>(scope.ServiceProvider, checker, fence);
		await worker.ExecuteAsync(new() { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = delivery });
	}

	// The first real fence and claim commit run normally. The test changes persisted authority
	// before acquiring the second real fence, so scope edits cannot deadlock behind the send lock.
	private sealed class SendPhaseFence(IPayrollInputRevisionStore inner, Func<Task> beforeSend) : IPayrollInputRevisionStore
	{
		private int _locks;
		public Task<Guid> ReadAsync() => inner.ReadAsync();
		public async Task<Guid> LockAsync()
		{
			if (++_locks == 2) await beforeSend();
			return await inner.LockAsync();
		}
	}

	private static async Task AssertRealPermissionAsync(PayslipPostgresTestFactory factory, Guid actor, string role, bool granted)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var principal = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(
			new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, actor.ToString()),
				new Claim(AbpClaimTypes.TenantId, EsiPayrollWorkflowScenario.Tenant.ToString()), new Claim(AbpClaimTypes.Role, role) }, "PersistedFixtureAuthority")));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		(await ActivatorUtilities.CreateInstance<PermissionChecker>(scope.ServiceProvider).IsGrantedAsync(SendPermission)).ShouldBe(granted);
	}

	private static async Task RevokeScopeAsync(PayslipPostgresTestFactory factory, Guid assignment)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var principal = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(
			new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
				new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId), new Claim(AbpClaimTypes.Role, "admin") }, "SyntheticFixtureEditor")));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await scope.ServiceProvider.GetRequiredService<IUserScopeAppService>().RevokeScopeAsync(assignment);
		await unit.CompleteAsync();
	}

	private static async Task SetActorActiveAsync(PayslipPostgresTestFactory factory, Guid actor, bool active)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var users = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
		var user = (await users.FindByIdAsync(actor.ToString()))!;
		user.SetIsActive(active);
		(await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
		await unit.CompleteAsync();
	}

	private static async Task<PayslipDeliveryDto> ResultAsync(HttpClient api, string url, Guid delivery) =>
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(url, Json))!.Single(x => x.Id == delivery);

	private static async Task AssertDeniedAsync(HttpClient api, string history, Guid delivery)
	{
		var result = await ResultAsync(api, history, delivery);
		result.Status.ShouldBe("FAILED");
		result.ErrorCategory.ShouldBe("ACCESS_REVOKED");
	}

	private static void AssertCaptures(PayslipPostgresTestFactory factory, PayslipDocumentDto document, int count)
	{
		factory.Messages.Count.ShouldBe(count);
		foreach (var message in factory.Messages)
		{
			message.Recipient.ShouldBe(document.RecipientEmail);
			Convert.ToHexString(SHA256.HashData(message.Pdf)).ShouldBe(document.Sha256);
		}
	}
}