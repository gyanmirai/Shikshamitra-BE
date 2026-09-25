using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public partial class CompensationRevisionApi_Tests
{
	[Fact]
	public async Task Protection_migration_requires_preview_and_converts_approved_gross_cost_idempotently()
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "ProtectionMigration");
		var template = await GetDefaultTemplateAsync(Client);
		var save = Request(template, 50000m, Guid.NewGuid().ToString("N"));
		save.SalaryBasis = "EMPLOYER_COST"; save.ProtectionPolicy = "PROTECT_EMPLOYER_COST";
		var created = await Client.PutAsJsonAsync($"/api/payroll/employees/{employeeId}/compensation", save, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var source = (await created.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		// Represent a pre-upgrade approved record without allowing gross protection through a new write API.
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repo = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeCompensationRevision, Guid>>();
			var legacy = await repo.GetAsync(source.Id);
			typeof(EmployeeCompensationRevision).GetProperty(nameof(EmployeeCompensationRevision.ProtectionPolicy))!
				.SetValue(legacy, CompensationProtectionPolicy.ProtectGross);
			await repo.UpdateAsync(legacy);
			await uow.CompleteAsync();
		}
		var endpoint = $"/api/payroll/employees/{employeeId}/compensation/protection-migration";
		var input = new PreviewProtectionMigrationDto { SourceRevisionId = source.Id, EffectiveMonth = save.EffectiveMonth.AddMonths(1) };
		using var viewOnly = CreateClientWithPermissions(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View);
		(await viewOnly.PostAsJsonAsync($"{endpoint}/preview", input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var previewResponse = await Client.PostAsJsonAsync($"{endpoint}/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<ProtectionMigrationPreviewDto>(JsonOptions))!;
		preview.PreviousPolicy.ShouldBe("PROTECT_GROSS");
		preview.Preview.Calculation.ProtectionPolicy.ShouldBe("PROTECT_EMPLOYER_COST");
		preview.Preview.Calculation.ProtectedMonthlyAmount.ShouldBe(source.EmployerCost, 0.01m);
		var history = await Client.GetFromJsonAsync<CompensationRevisionListDto>($"/api/payroll/employees/{employeeId}/compensation", JsonOptions);
		history!.Items.Count.ShouldBe(1);
		var apply = new ApplyProtectionMigrationDto { SourceRevisionId = source.Id, EffectiveMonth = input.EffectiveMonth, PreviewToken = "invalid" };
		(await Client.PostAsJsonAsync($"{endpoint}/apply", apply, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		apply.PreviewToken = preview.Preview.PreviewToken;
		var appliedResponse = await Client.PostAsJsonAsync($"{endpoint}/apply", apply, JsonOptions);
		appliedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await appliedResponse.Content.ReadAsStringAsync());
		var applied = (await appliedResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		applied.ProtectionPolicy.ShouldBe("PROTECT_EMPLOYER_COST");
		applied.ProtectionCalculationVersion.ShouldBe(1);
		var replayResponse = await Client.PostAsJsonAsync($"{endpoint}/apply", apply, JsonOptions);
		replayResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await replayResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!.Id.ShouldBe(applied.Id);
		history = await Client.GetFromJsonAsync<CompensationRevisionListDto>($"/api/payroll/employees/{employeeId}/compensation", JsonOptions);
		history!.Items.Count.ShouldBe(2);
		history.Items.Single(x => x.Id == source.Id).ProtectionPolicy.ShouldBe("PROTECT_GROSS");
	}

	[Theory]
	[InlineData("missing-snapshot")]
	[InlineData("locked-payroll")]
	[InlineData("stale-source")]
	public async Task Protection_migration_rejects_unsafe_sources(string scenario)
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "MigrationGuard");
		var template = await GetDefaultTemplateAsync(Client);
		var request = Request(template, 50000m, Guid.NewGuid().ToString("N"));
		request.ProtectionPolicy = "PROTECT_EMPLOYER_COST";
		var response = await Client.PutAsJsonAsync($"/api/payroll/employees/{employeeId}/compensation", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var source = (await response.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		var input = new PreviewProtectionMigrationDto { SourceRevisionId = source.Id, EffectiveMonth = request.EffectiveMonth.AddMonths(1) };
		var endpoint = $"/api/payroll/employees/{employeeId}/compensation/protection-migration";
		var previewResponse = await Client.PostAsJsonAsync($"{endpoint}/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<ProtectionMigrationPreviewDto>(JsonOptions))!;
		if (scenario == "stale-source")
		{
			request.MonthlyAmount = 51000m; request.ExpectedRevision = source.RevisionNumber;
			request.IdempotencyKey = Guid.NewGuid().ToString("N");
			(await Client.PutAsJsonAsync($"/api/payroll/employees/{employeeId}/compensation", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);
		}
		else
		{
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			if (scenario == "missing-snapshot")
			{
				var repo = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeCompensationRevision, Guid>>();
				var revision = await repo.GetAsync(source.Id);
				typeof(EmployeeCompensationRevision).GetProperty(nameof(EmployeeCompensationRevision.CalculationSnapshotJson))!.SetValue(revision, "{}");
				await repo.UpdateAsync(revision);
			}
			else
			{
				var repo = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), input.EffectiveMonth.ToString("yyyy-MM"), employeeId);
				snapshot.MarkLocked();
				await repo.InsertAsync(snapshot);
			}
			await uow.CompleteAsync();
		}
		var apply = await Client.PostAsJsonAsync($"{endpoint}/apply", new ApplyProtectionMigrationDto
		{
			SourceRevisionId = source.Id, EffectiveMonth = input.EffectiveMonth, PreviewToken = preview.Preview.PreviewToken
		}, JsonOptions);
		apply.IsSuccessStatusCode.ShouldBeFalse();
		((int)apply.StatusCode).ShouldBeLessThan(500);
		var payload = await apply.Content.ReadAsStringAsync();
		payload.ShouldContain(scenario == "missing-snapshot" ? "ProtectionMigrationSnapshotRequired" :
			scenario == "locked-payroll" ? "CompensationRevisionLockedPayroll" : "CompensationSaveConflict");
		var history = await Client.GetFromJsonAsync<CompensationRevisionListDto>($"/api/payroll/employees/{employeeId}/compensation", JsonOptions);
		history!.Items.Count.ShouldBe(scenario == "stale-source" ? 2 : 1);
	}

	[Fact]
	public async Task New_compensation_writes_must_reject_gross_protection()
	{
		var template = await GetDefaultTemplateAsync(Client);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RejectGrossProtection");
		var request = Request(template, 50000m, Guid.NewGuid().ToString("N"));
		request.ProtectionPolicy = "PROTECT_GROSS";
		(await Client.PutAsJsonAsync($"/api/payroll/employees/{employeeId}/compensation", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
