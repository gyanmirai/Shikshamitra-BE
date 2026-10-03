using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
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
		await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { correctionId = correction.Id, sourceMonth, targetMonth,
			sourceSha256 = sourceDocument.Sha256, targetSha256 = targetDocument.Sha256, target.NetPay, target.EmployerCost }));
		var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
		var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());
		await File.WriteAllTextAsync(output + ".model-diff.json", JsonSerializer.Serialize(differences.Select(x => new {
			type = x.GetType().Name, name = x.GetType().GetProperty("Name")?.GetValue(x), table = x.GetType().GetProperty("Table")?.GetValue(x), columnType = x.GetType().GetProperty("ColumnType")?.GetValue(x), oldColumnType = x is Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation column ? column.OldColumn.ColumnType : null })));
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
			await repository.InsertAsync(new EmployeeAttendance { TenantId = employee.TenantId, EmployeeId = employee.Id,
				EducationMediumStageId = source.EducationMediumStageId, AttendanceDate = DateOnly.FromDateTime(date),
				StatusType = EmployeeAttendanceStatusType.Present, TimeIn = new DateTimeOffset(date.AddHours(9)),
				TimeOut = new DateTimeOffset(date.AddHours(17)), Remarks = "Synthetic payslip settlement browser attendance" });
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
			x.Name is "HR.View" or "HR.Employee.EmployeeList.View" or "ShikshaMitra.Module.HR" or
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
