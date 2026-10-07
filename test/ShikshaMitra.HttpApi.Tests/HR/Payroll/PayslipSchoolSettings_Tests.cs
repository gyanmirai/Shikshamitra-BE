using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipSchoolSettingsFactAttribute : FactAttribute
{
	public PayslipSchoolSettingsFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_SCHOOL_SETTINGS_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_SCHOOL_SETTINGS_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public class PayslipSchoolSettingsTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_SCHOOL_SETTINGS_TEST_POSTGRES";
}

// Reopen persisted application state without recreating tables or reseeding any identities or grants.
public sealed class PayslipSchoolSettingsAttachFactory : PayslipSchoolSettingsTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("School settings tests require a disposable loopback payroll_tests_* database.");
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(connection.ConnectionString)));
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipSchoolSettings_Tests
{
	private const string SettingsUrl = "/api/payroll/payslip-settings";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[PayslipSchoolSettingsFact]
	public async Task New_school_defaults_off_and_settings_survive_academic_year_creation_and_host_recreation()
	{
		Guid tenantId;
		Guid adminId;
		PayslipSettingsDto saved;
		PayslipSettingsDto existingSchool;
		var schoolName = $"syntheticpayslip{Guid.NewGuid():N}";
		var email = $"{schoolName}@test.shikshamitra.local";
		using (var factory = new PayslipSchoolSettingsTestFactory())
		{
			using var host = factory.CreateClient();
			host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
			host.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				string.Join(',', TenantManagementPermissions.Tenants.Default, TenantManagementPermissions.Tenants.Create));
			var create = await host.PostAsJsonAsync("/api/multi-tenancy/tenants", new
			{
				Name = schoolName,
				AdminEmailAddress = email,
				AdminPassword = $"Fixture@9{Guid.NewGuid():N}",
				ExtraProperties = new Dictionary<string, object?>
				{
					["Email"] = email,
					["InstituteName"] = schoolName,
					["Address1"] = "Synthetic Test Road",
					["Address2"] = "Fixture Area",
					["Address3"] = "Test Building",
					["City"] = "Ahmedabad",
					["State"] = "Gujarat",
					["PostalCode"] = "380001",
					["Country"] = "India",
					["ContactNumber"] = "9999999999"
				}
			}, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(create);
			tenantId = (await create.Content.ReadFromJsonAsync<TenantDto>(Json))!.Id;
			tenantId.ShouldNotBe(Guid.Parse(TestAuthHandler.TestTenantId));
			using (var scope = factory.Services.CreateScope())
			using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
			using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
			{
				var admin = await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>()
					.FindByNormalizedEmailAsync(email.ToUpperInvariant());
				admin.ShouldNotBeNull("The supported tenant-creation workflow must create the synthetic school administrator.");
				admin.TenantId.ShouldBe(tenantId);
				adminId = admin.Id;
				await unit.CompleteAsync();
			}
			using var api = CreateSchoolClient(factory, tenantId, adminId);
			using var originalSchool = CreateSchoolClient(factory, Guid.Parse(TestAuthHandler.TestTenantId), Guid.Parse(TestAuthHandler.TestUserId));
			existingSchool = (await originalSchool.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!;
			for (var read = 0; read < 3; read++)
			{
				var defaults = (await api.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!;
				defaults.SchoolName.ShouldBe(schoolName);
				defaults.SchoolAddress.ShouldBeEmpty();
				defaults.AutomaticEmail.ShouldBeFalse();
				defaults.ConcurrencyStamp.ShouldBeNull();
				defaults.IdempotencyKey.ShouldBe(Guid.Empty);
			}
			(await SettingsCountAsync(factory, tenantId)).ShouldBe(0);
			using (var denied = CreateSchoolClient(factory, tenantId, adminId))
			{
				denied.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
				denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
				(await denied.GetAsync(SettingsUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
				denied.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
				denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
					ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
				var forbiddenSave = await denied.PutAsJsonAsync(SettingsUrl, new PayslipSettingsDto
				{
					SchoolName = schoolName,
					SchoolAddress = "Synthetic Test Road, Ahmedabad, Gujarat",
					AutomaticEmail = true,
					IdempotencyKey = Guid.NewGuid()
				}, Json);
				forbiddenSave.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			}
			(await SettingsCountAsync(factory, tenantId)).ShouldBe(0);

			// Assign scope through the supported API only for this newly created synthetic administrator.
			var assignment = await api.PostAsJsonAsync("/api/app/user-scope/assign-scope", new CreateUserScopeAssignmentDto
			{
				UserId = adminId,
				Description = "Synthetic payslip school-settings acceptance fixture"
			}, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(assignment);
			var input = new PayslipSettingsDto
			{
				SchoolName = schoolName,
				SchoolAddress = "Synthetic Test Road, Ahmedabad, Gujarat",
				AutomaticEmail = true,
				IdempotencyKey = Guid.NewGuid()
			};
			using var interruptedApi = factory.CreateDefaultClient(new CommittedSettingsResponseLoss());
			foreach (var header in api.DefaultRequestHeaders)
				interruptedApi.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
			await Should.ThrowAsync<HttpRequestException>(() => interruptedApi.PutAsJsonAsync(SettingsUrl, input, Json));
			// Read the actual committed state after the response was consumed and lost at transport.
			saved = (await api.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!;
			saved.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
			saved.SchoolName.ShouldBe(input.SchoolName);
			saved.SchoolAddress.ShouldBe(input.SchoolAddress);
			saved.AutomaticEmail.ShouldBe(input.AutomaticEmail);
			saved.IdempotencyKey.ShouldBe(input.IdempotencyKey);
			var repeat = await api.PutAsJsonAsync(SettingsUrl, input, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(repeat);
			AssertSettings((await repeat.Content.ReadFromJsonAsync<PayslipSettingsDto>(Json))!, saved);
			var conflictingRetry = await api.PutAsJsonAsync(SettingsUrl, new PayslipSettingsDto
			{
				SchoolName = input.SchoolName,
				SchoolAddress = input.SchoolAddress,
				AutomaticEmail = false,
				IdempotencyKey = input.IdempotencyKey
			}, Json);
			conflictingRetry.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await conflictingRetry.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipConflict);
			var staleSave = await api.PutAsJsonAsync(SettingsUrl, new PayslipSettingsDto
			{
				SchoolName = input.SchoolName,
				SchoolAddress = input.SchoolAddress,
				AutomaticEmail = false,
				ConcurrencyStamp = input.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid()
			}, Json);
			staleSave.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await staleSave.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipConflict);
			AssertSettings((await api.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!, saved);

			// Settings are tenant-wide; exercise actual academic-year records rather than an ignored settings query parameter.
			foreach (var year in new[] { 2040, 2041 })
			{
				var createdYear = await api.PostAsJsonAsync("/api/app/academic-year", new CreateUpdateAcademicYearDto
				{
					Name = $"Payslip acceptance {year}-{year + 1}",
					StartYear = new DateTime(year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
					EndYear = new DateTime(year + 1, 3, 31, 0, 0, 0, DateTimeKind.Utc)
				}, Json);
				await EsiPayrollWorkflowScenario.RequireSuccess(createdYear);
				var academicYear = (await createdYear.Content.ReadFromJsonAsync<AcademicYearDto>(Json))!;
				var loadedYear = (await api.GetFromJsonAsync<AcademicYearDto>($"/api/app/academic-year/{academicYear.Id}", Json))!;
				loadedYear.Name.ShouldBe(academicYear.Name);
				AssertSettings((await api.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!, saved);
			}
			(await SettingsCountAsync(factory, tenantId)).ShouldBe(1);
			AssertSettings((await originalSchool.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!, existingSchool);
			factory.Messages.ShouldBeEmpty();
		}

		using var reopened = new PayslipSchoolSettingsAttachFactory();
		using var restored = CreateSchoolClient(reopened, tenantId, adminId);
		AssertSettings((await restored.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!, saved);
		(await SettingsCountAsync(reopened, tenantId)).ShouldBe(1);
		using var unchanged = CreateSchoolClient(reopened, Guid.Parse(TestAuthHandler.TestTenantId), Guid.Parse(TestAuthHandler.TestUserId));
		AssertSettings((await unchanged.GetFromJsonAsync<PayslipSettingsDto>(SettingsUrl, Json))!, existingSchool);
		reopened.Messages.ShouldBeEmpty();
	}

	private static HttpClient CreateSchoolClient(PayslipPostgresTestFactory factory, Guid tenantId, Guid userId)
	{
		var api = factory.CreateClient();
		api.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		api.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		api.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add,
			ShikshaMitraPermissions.UserScope.Assign));
		return api;
	}

	private sealed class CommittedSettingsResponseLoss : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = await base.SendAsync(request, cancellationToken);
			if (request.Method != HttpMethod.Put || request.RequestUri?.AbsolutePath != SettingsUrl) return response;
			response.EnsureSuccessStatusCode();
			await response.Content.ReadAsByteArrayAsync(cancellationToken);
			response.Dispose();
			throw new HttpRequestException("Synthetic response lost after committed school-settings write.");
		}
	}

	private static void AssertSettings(PayslipSettingsDto actual, PayslipSettingsDto expected)
	{
		actual.SchoolName.ShouldBe(expected.SchoolName);
		actual.SchoolAddress.ShouldBe(expected.SchoolAddress);
		actual.AutomaticEmail.ShouldBe(expected.AutomaticEmail);
		actual.ConcurrencyStamp.ShouldBe(expected.ConcurrencyStamp);
		actual.IdempotencyKey.ShouldBe(expected.IdempotencyKey);
	}

	private static async Task<int> SettingsCountAsync(PayslipPostgresTestFactory factory, Guid tenantId)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var count = await scope.ServiceProvider.GetRequiredService<IRepository<PayslipSettings, Guid>>()
			.CountAsync(x => x.Id == tenantId);
		await unit.CompleteAsync();
		return count;
	}
}
