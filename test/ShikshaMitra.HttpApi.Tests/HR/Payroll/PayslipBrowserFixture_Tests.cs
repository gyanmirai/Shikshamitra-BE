using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenIddict.Abstractions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipBrowserFixtureFactAttribute : FactAttribute
{
	public PayslipBrowserFixtureFactAttribute(string mode = "create-synthetic-identity")
	{
		if (Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_FIXTURE") != mode)
			Skip = "Explicit local browser fixture bootstrap only; never changes an existing identity.";
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipBrowserFixture_Tests
{
	[PayslipBrowserFixtureFact("prepare-settlement-attendance")]
	public async Task Prepare_only_the_applied_synthetic_settlement_employee_target_attendance()
	{
		var employeeId = Guid.Parse(Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_SETTLEMENT_EMPLOYEE")!);
		var month = DateTime.ParseExact(Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_SETTLEMENT_MONTH")! + "-01",
			"yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
		using var factory = new PayslipWorkerAttachFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		string originalHash;
		Guid sourceDocumentId;
		long originalGrants;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var services = scope.ServiceProvider;
			var employee = await services.GetRequiredService<IRepository<Employee, Guid>>().GetAsync(employeeId);
			employee.FirstName!.StartsWith("Test_PayslipMatrix_", StringComparison.Ordinal).ShouldBeTrue();
			employee.Email!.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal).ShouldBeTrue();
			var correction = (await services.GetRequiredService<IRepository<PayslipCorrection, Guid>>()
				.GetListAsync(x => x.EmployeeId == employeeId && x.Status == "APPLIED" && x.SettlementMonth == month.ToString("yyyy-MM"))).Single();
			var source = await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetAsync(correction.SourceDocumentId);
			(await services.GetRequiredService<ISalarySlipRepository>().GetAsync(source.SalarySlipId)).Status.ShouldBe(SalarySlipStatus.Paid);
			sourceDocumentId = source.Id;
			originalHash = source.Sha256;
			originalGrants = await services.GetRequiredService<IRepository<PermissionGrant, Guid>>().GetCountAsync();
			var assignments = await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().GetListAsync(x => x.UserId == employee.UserId);
			EmployeeTestHelper.DefaultMediumStageId = assignments.First(x => x.EducationMediumStageId.HasValue).EducationMediumStageId!.Value;
			(await services.GetRequiredService<IRepository<EmployeeAttendance, Guid>>().GetListAsync(x =>
				x.EmployeeId == employeeId && x.AttendanceDate >= DateOnly.FromDateTime(month) &&
				x.AttendanceDate < DateOnly.FromDateTime(month.AddMonths(1)))).ShouldBeEmpty("Never replace existing attendance.");
		}
		// Explicit disposable-fixture preparation through EF keeps normal payroll input revision tracking.
		// The product's future-date attendance guard remains unchanged.
		await workflow.SeedAttendanceAsync(employeeId, month);
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var services = scope.ServiceProvider;
			(await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetAsync(sourceDocumentId)).Sha256.ShouldBe(originalHash);
			(await services.GetRequiredService<IRepository<PermissionGrant, Guid>>().GetCountAsync()).ShouldBe(originalGrants);
			(await services.GetRequiredService<IRepository<EmployeeAttendance, Guid>>().GetListAsync(x =>
				x.EmployeeId == employeeId && x.AttendanceDate >= DateOnly.FromDateTime(month) &&
				x.AttendanceDate < DateOnly.FromDateTime(month.AddMonths(1)))).Count.ShouldBe(DateTime.DaysInMonth(month.Year, month.Month));
		}
		factory.Messages.ShouldBeEmpty();
	}

	[PayslipBrowserFixtureFact("prepare-synthetic-login-client")]
	public async Task Prepare_owned_synthetic_login_client_without_reseeding_user_grants()
	{
		var clientId = Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_CLIENT_ID") ?? "";
		var clientSecret = Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_CLIENT_SECRET") ?? "";
		clientId.StartsWith("payslip-browser-client-", StringComparison.Ordinal).ShouldBeTrue();
		clientSecret.Length.ShouldBeGreaterThanOrEqualTo(32);
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var host = services.GetRequiredService<ICurrentTenant>().Change(null);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var grants = services.GetRequiredService<IRepository<PermissionGrant, Guid>>();
		var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
		long beforeGrants;
		long beforeUsers;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			beforeGrants = await grants.GetCountAsync();
			beforeUsers = await users.GetCountAsync();
		}
		var scopes = services.GetRequiredService<IOpenIddictScopeManager>();
		if (await scopes.FindByNameAsync("ShikshaMitra") == null)
			await scopes.CreateAsync(new OpenIddictScopeDescriptor
			{
				Name = "ShikshaMitra",
				DisplayName = "Synthetic local ShikshaMitra API",
				Resources = { "ShikshaMitra" }
			});
		var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
		var application = await applications.FindByClientIdAsync(clientId);
		if (application == null)
			application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
			{
				ClientId = clientId,
				ClientSecret = clientSecret,
				ClientType = OpenIddictConstants.ClientTypes.Confidential,
				DisplayName = "Capture-only local payslip browser verification",
				Permissions =
				{
					OpenIddictConstants.Permissions.Endpoints.Token,
					OpenIddictConstants.Permissions.GrantTypes.Password,
					OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
					OpenIddictConstants.Permissions.Scopes.Profile,
					OpenIddictConstants.Permissions.Scopes.Email,
					OpenIddictConstants.Permissions.Scopes.Roles,
					OpenIddictConstants.Permissions.Prefixes.Scope + "ShikshaMitra"
				}
			});
		(await applications.ValidateClientSecretAsync(application, clientSecret)).ShouldBeTrue();
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			(await grants.GetCountAsync()).ShouldBe(beforeGrants, "Creating an owned login client must not restore or broaden grants.");
			(await users.GetCountAsync()).ShouldBe(beforeUsers, "Preserve the existing synthetic identity.");
		}
		await unit.CompleteAsync();
	}

	[PayslipBrowserFixtureFact("prepare-history-employee")]
	public async Task Prepare_second_issued_employee_for_capture_only_history_selection()
	{
		using var factory = new PayslipWorkerAttachFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		Guid employeeId;
		PayslipDocument sourceDocument;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var employees = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
			var source = (await employees.GetListAsync(x => x.FirstName == "Test_PayslipDocument_46d54e90")).Single();
			source.Email!.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal).ShouldBeTrue();
			sourceDocument = (await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDocument, Guid>>()
				.GetListAsync(x => x.EmployeeId == source.Id && x.MonthYear == month.ToString("yyyy-MM")))
				.OrderByDescending(x => x.Revision).First();
			var membership = (await scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>()
				.GetListAsync(x => x.UserId == source.UserId && x.EducationMediumStageId != null)).First();
			EmployeeTestHelper.DefaultRoleId = source.RoleId;
			EmployeeTestHelper.DefaultShiftId = source.ShiftId!.Value;
			EmployeeTestHelper.DefaultAcademicYearId = membership.AcademicYearId!.Value;
			EmployeeTestHelper.DefaultBoardId = membership.EducationBoardId!.Value;
			EmployeeTestHelper.DefaultMediumId = membership.EducationMediumId!.Value;
			EmployeeTestHelper.DefaultMediumStageId = membership.EducationMediumStageId!.Value;
			employeeId = (await employees.GetListAsync(x => x.FirstName!.StartsWith("Test_PayslipHistorySelection_")))
				.SingleOrDefault()?.Id ?? Guid.Empty;
		}
		(await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!.AutomaticEmail.ShouldBeFalse();
		if (employeeId == Guid.Empty)
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipHistorySelection", dateOfJoining: month);
			employeeId = employee.Id;
			await workflow.EnrollAsync(employeeId, "2345678901");
			await workflow.SaveCompensationAsync(employeeId, month, "GROSS", 8000m);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employeeId));
			await workflow.SeedAttendanceAsync(employeeId, month);
			var preview = await workflow.CalculateAsync(month);
			(await workflow.EmployeeAsync(preview.Id, employeeId)).NetPay.ShouldBe(7940);
			await workflow.ReviewAsync(preview.Id, employeeId, month);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(preview.Id, employeeId, month));
		}
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employeeId}&monthYear={month:yyyy-MM}", json))!.Single();
		document.RecipientEmail!.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal).ShouldBeTrue();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		if (!(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Any())
		{
			var response = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto
			{
				DocumentId = document.Id,
				IdempotencyKey = Guid.NewGuid()
			}, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var delivery = (await response.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
			using var scope = factory.Services.CreateScope();
			await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(new()
			{
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				DeliveryId = delivery.Id
			});
			var capture = Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_ARTIFACTS")!;
			(await File.ReadAllTextAsync(Path.Combine(capture, "recipient"))).ShouldBe(document.RecipientEmail);
			Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(capture, "captured.pdf"))))
				.ShouldBe(document.Sha256);
		}
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{sourceDocument.Id}/pdf")))
			.ShouldBe(sourceDocument.Sha256);
	}

	[PayslipBrowserFixtureFact("verify-runtime-migration-guard")]
	public async Task Runtime_timestamp_discrepancy_remains_after_applying_real_migrations()
	{
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		// This opt-in probe requires a NEW owned disposable database, never the populated browser database.
		(await db.Database.GetAppliedMigrationsAsync()).ShouldBeEmpty();
		var expectedMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
		expectedMigrations.ShouldNotBeEmpty();
		db.Database.HasPendingModelChanges().ShouldBeTrue();
		await db.Database.MigrateAsync();
		(await db.Database.GetAppliedMigrationsAsync()).ShouldBe(expectedMigrations);
		(await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
		// Applying authoritative migrations does not resolve the unchanged runtime/design-time model mismatch.
		db.Database.HasPendingModelChanges().ShouldBeTrue();
	}

	[PayslipBrowserFixtureFact("verify-settlement-result")]
	public async Task Verify_authenticated_browser_settlement_evidence_and_migration_model()
	{
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var employee = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(
			x => x.FirstName == "Test_PayslipDocument_46d54e90" && x.LastName == "TestDoe")).Single();
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var sourceMonth = month.ToString("yyyy-MM");
		var targetMonth = month.AddMonths(1).ToString("yyyy-MM");
		var snapshots = await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetListAsync(x => x.EmployeeId == employee.Id);
		var source = snapshots.Single(x => x.MonthYear == sourceMonth);
		var target = snapshots.Single(x => x.MonthYear == targetMonth);
		source.IsLocked.ShouldBeTrue();
		source.NetPay.ShouldBe(9925m);
		source.GrossSalary.ShouldBe(10000m);
		target.IsLocked.ShouldBeTrue();
		target.NetPay.ShouldBe(9473.45m);
		target.TotalDeduction.ShouldBe(529.55m);
		target.EmployerCost.ShouldBe(9856.45m);
		var corrections = await services.GetRequiredService<IRepository<PayslipCorrection, Guid>>().GetListAsync(x => x.EmployeeId == employee.Id && x.Status == "APPLIED");
		var correction = corrections.Single();
		correction.SourceMonth.ShouldBe(sourceMonth);
		correction.SettlementMonth.ShouldBe(targetMonth);
		target.DeductionComponentsJson.ShouldContain(correction.Id.ToString());
		var slips = await services.GetRequiredService<IRepository<SalarySlip, Guid>>().GetListAsync(x => x.EmployeeId == employee.Id);
		slips.Single(x => x.MonthYear == sourceMonth).Status.ShouldBe(SalarySlipStatus.Paid);
		slips.Single(x => x.MonthYear == targetMonth).NetSalary.ShouldBe(target.NetPay);
		var documents = await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetListAsync(x => x.EmployeeId == employee.Id);
		var sourceDocument = documents.Where(x => x.MonthYear == sourceMonth).OrderByDescending(x => x.Revision).First();
		var targetDocument = documents.Single(x => x.MonthYear == targetMonth);
		sourceDocument.Revision.ShouldBe(4);
		foreach (var document in new[] { sourceDocument, targetDocument })
			Convert.ToHexString(SHA256.HashData(document.Pdf)).ShouldBe(document.Sha256);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var output = Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_VERIFY_OUTPUT") ?? throw new InvalidOperationException("An ignored local verification output path is required.");
		await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
		{
			correctionId = correction.Id,
			sourceMonth,
			targetMonth,
			sourceSha256 = sourceDocument.Sha256,
			targetSha256 = targetDocument.Sha256,
			target.NetPay,
			target.EmployerCost
		}));
		var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
		var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());
		await File.WriteAllTextAsync(output + ".model-diff.json", JsonSerializer.Serialize(differences.Select(x => new
		{
			type = x.GetType().Name,
			name = x.GetType().GetProperty("Name")?.GetValue(x),
			table = x.GetType().GetProperty("Table")?.GetValue(x),
			columnType = x.GetType().GetProperty("ColumnType")?.GetValue(x),
			oldColumnType = x is Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation column ? column.OldColumn.ColumnType : null
		})));
		db.Database.HasPendingModelChanges().ShouldBeFalse();
	}

	[PayslipBrowserFixtureFact("prepare-settlement-target")]
	public async Task Prepare_synthetic_later_month_attendance_without_modifying_issued_source()
	{
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var employee = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(
			x => x.FirstName == "Test_PayslipDocument_46d54e90" && x.LastName == "TestDoe")).Single();
		employee.Email!.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal).ShouldBeTrue();
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
		var monthYear = month.ToString("yyyy-MM");
		(await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetListAsync(
			x => x.EmployeeId == employee.Id && x.MonthYear == monthYear && x.IsLocked)).ShouldBeEmpty();
		(await services.GetRequiredService<IRepository<SalarySlip, Guid>>().GetListAsync(
			x => x.EmployeeId == employee.Id && x.MonthYear == monthYear)).ShouldBeEmpty();
		var repository = services.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var start = DateOnly.FromDateTime(month);
		var end = DateOnly.FromDateTime(month.AddMonths(1));
		var existing = await repository.GetListAsync(x => x.EmployeeId == employee.Id && x.AttendanceDate >= start && x.AttendanceDate < end);
		if (existing.Count > 0)
		{
			existing.Count.ShouldBe(DateTime.DaysInMonth(month.Year, month.Month));
			existing.All(x => x.Remarks == "Synthetic payslip settlement browser attendance" && x.StatusType == EmployeeAttendanceStatusType.Present).ShouldBeTrue();
			return;
		}
		var source = (await repository.GetListAsync(x => x.EmployeeId == employee.Id)).First();
		for (var date = month; date < month.AddMonths(1); date = date.AddDays(1))
			await repository.InsertAsync(new EmployeeAttendance
			{
				TenantId = employee.TenantId,
				EmployeeId = employee.Id,
				EducationMediumStageId = source.EducationMediumStageId,
				AttendanceDate = DateOnly.FromDateTime(date),
				StatusType = EmployeeAttendanceStatusType.Present,
				TimeIn = new DateTimeOffset(date.AddHours(9)),
				TimeOut = new DateTimeOffset(date.AddHours(17)),
				Remarks = "Synthetic payslip settlement browser attendance"
			});
		await uow.CompleteAsync();
	}

	[PayslipBrowserFixtureFact]
	public async Task Create_dedicated_browser_identity_without_reseeding_or_changing_existing_grants()
	{
		// Attach factory validates a loopback payroll_tests_* database and skips all seeding.
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var userName = "payslip-browser-" + Guid.NewGuid().ToString("N");
		var password = "LocalOnly!9" + Guid.NewGuid().ToString("N");
		var user = new IdentityUser(Guid.NewGuid(), userName, userName + "@test.shikshamitra.local", Guid.Parse(TestAuthHandler.TestTenantId));
		var users = services.GetRequiredService<IdentityUserManager>();
		var result = await users.CreateAsync(user, password);
		result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(x => x.Description)));
		// Existing employee search has an HR-role visibility gate in addition to explicit permissions.
		// Assign only this newly-created synthetic identity; never seed or change existing role grants.
		var roles = services.GetRequiredService<IdentityRoleManager>();
		if (await roles.FindByNameAsync("HR Manager") == null)
			(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "HR Manager", user.TenantId))).Succeeded.ShouldBeTrue();
		(await users.AddToRoleAsync(user, "HR Manager")).Succeeded.ShouldBeTrue();
		var definitions = await services.GetRequiredService<IPermissionDefinitionManager>().GetPermissionsAsync();
		var permissions = services.GetRequiredService<IPermissionManager>();
		var names = definitions.Where(x => x.Name.StartsWith("HR.PayrollManagement", StringComparison.Ordinal) ||
			x.Name is "HR.View" or "HR.Employee.EmployeeList.View" or "HR.Employee.EmployeeList.Edit" or
				"AbpIdentity.Users.Update" or "ShikshaMitra.Module.HR" or
				"ShikshaMitra.Module.HR.Payroll" ||
			x.Name.EndsWith("AcademicYear.View", StringComparison.Ordinal)).Select(x => x.Name).ToList();
		foreach (var name in names) await permissions.SetAsync(name, "U", user.Id.ToString(), true);
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
			Guid.NewGuid(), user.TenantId!.Value, user.Id, description: "Disposable synthetic payslip browser verification"));
		await uow.CompleteAsync();
		var file = Environment.GetEnvironmentVariable("PAYSLIP_BROWSER_FIXTURE_OUTPUT") ?? throw new InvalidOperationException("An ignored local artifact output path is required.");
		await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { userName, password, tenantId = user.TenantId, permissions = names }));
	}
}
