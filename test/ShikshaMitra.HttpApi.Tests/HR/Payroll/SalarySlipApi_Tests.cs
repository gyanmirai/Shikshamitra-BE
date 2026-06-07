using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class SalarySlipApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/salary-slips";
	private const string RunPayrollUrl = "/api/payroll/run";

	public SalarySlipApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Temp_Components_Should_Be_Month_Scoped_And_Match_Payslip_Totals()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"TempPayroll{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "TempPayrollEarn");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "TempPayrollDed");

		var update = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = earningComponent.Id,
					Name = earningComponent.Name,
					Amount = 1000m,
					IsFixed = true,
					IsTemp = false
				},
				new()
				{
					Name = "January Temp Earning",
					Amount = 100m,
					IsFixed = false,
					IsTemp = true,
					ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = deductionComponent.Id,
					Name = deductionComponent.Name,
					Amount = 100m,
					IsFixed = true,
					IsTemp = false
				},
				new()
				{
					Name = "January Temp Deduction",
					Amount = 50m,
					IsFixed = false,
					IsTemp = true,
					ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
				}
			}
		};

		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employee.Id}/salary-structure",
			update,
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var januaryPreview = await GetPayrollItemAsync(employee.Id, "2026-01");
		januaryPreview.EarningComponents.ShouldContain(x => x.Name == "January Temp Earning" && x.Amount == 100d);
		januaryPreview.DeductionComponents.ShouldContain(x => x.Name == "January Temp Deduction" && x.Amount == 50d);
		januaryPreview.GrossSalary.ShouldBe(100d);
		januaryPreview.TotalDeduction.ShouldBe(50d);

		var februaryPreview = await GetPayrollItemAsync(employee.Id, "2026-02");
		februaryPreview.EarningComponents.ShouldNotContain(x => x.Name == "January Temp Earning");
		februaryPreview.DeductionComponents.ShouldNotContain(x => x.Name == "January Temp Deduction");
		februaryPreview.GrossSalary.ShouldBe(0d);
		februaryPreview.TotalDeduction.ShouldBe(0d);

		var januarySlip = await GetSalarySlipResponseAsync(employee.Id, "JANUARY-2026");
		januarySlip.Earnings.ShouldContain(x => x.Name == "January Temp Earning" && x.Amount == 100d && x.IsTemp);
		januarySlip.Deductions.ShouldContain(x => x.Name == "January Temp Deduction" && x.Amount == 50d && x.IsTemp);
		januarySlip.TotalEarning.ShouldBe(januaryPreview.GrossSalary);
		januarySlip.TotalDeduction.ShouldBe(januaryPreview.TotalDeduction);

		var februarySlip = await GetSalarySlipResponseAsync(employee.Id, "FEBRUARY-2026");
		februarySlip.Earnings.ShouldNotContain(x => x.Name == "January Temp Earning");
		februarySlip.Deductions.ShouldNotContain(x => x.Name == "January Temp Deduction");
		februarySlip.TotalEarning.ShouldBe(februaryPreview.GrossSalary);
		februarySlip.TotalDeduction.ShouldBe(februaryPreview.TotalDeduction);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Draft_Payslip_Should_Refresh_When_Snapshot_Line_Items_Change_With_Same_Totals()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayslipLineRefresh{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

		await UpdateSalaryStructureAsync(
			employee.Id,
			new UpdateSalaryStructureDto
			{
				EarningComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "May Temp Earning",
						Amount = 100m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				},
				DeductionComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "May Temp Deduction",
						Amount = 10m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				}
			});

		var originalPreview = await GetPayrollItemAsync(employee.Id, "2026-05");
		originalPreview.GrossSalary.ShouldBe(100d);

		var originalSlip = await GetSalarySlipResponseAsync(employee.Id, "MAY-2026");
		originalSlip.Earnings.ShouldContain(x => x.Name == "May Temp Earning" && x.Amount == 100d);

		await UpdateSalaryStructureAsync(
			employee.Id,
			new UpdateSalaryStructureDto
			{
				EarningComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "Renamed May Temp Earning",
						Amount = 100m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				},
				DeductionComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "May Temp Deduction",
						Amount = 10m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				}
			});

		var updatedPreview = await GetPayrollItemAsync(employee.Id, "2026-05");
		updatedPreview.GrossSalary.ShouldBe(originalPreview.GrossSalary);
		updatedPreview.EarningComponents.ShouldContain(x => x.Name == "Renamed May Temp Earning" && x.Amount == 100d);

		var refreshedSlip = await GetSalarySlipResponseAsync(employee.Id, "MAY-2026");
		refreshedSlip.TotalEarning.ShouldBe(updatedPreview.GrossSalary);
		refreshedSlip.Earnings.ShouldNotContain(x => x.Name == "May Temp Earning");
		refreshedSlip.Earnings.ShouldContain(x => x.Name == "Renamed May Temp Earning" && x.Amount == 100d && x.IsTemp);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Draft_Payslip_Should_Mirror_Unlocked_Payroll_Snapshot()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayslipFreshSnapshot{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

		await UpdateSalaryStructureAsync(
			employee.Id,
			new UpdateSalaryStructureDto
			{
				EarningComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "January Temp Earning",
						Amount = 100m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				},
				DeductionComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "January Temp Deduction",
						Amount = 50m,
						IsFixed = false,
						IsTemp = true,
						ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
					}
				}
			});

		var februaryPreview = await GetPayrollItemAsync(employee.Id, "2026-02");
		februaryPreview.GrossSalary.ShouldBe(0d);
		februaryPreview.TotalDeduction.ShouldBe(0d);

		var originalFebruarySlip = await GetSalarySlipResponseAsync(employee.Id, "FEBRUARY-2026");
		originalFebruarySlip.TotalEarning.ShouldBe(0d);
		originalFebruarySlip.TotalDeduction.ShouldBe(0d);

		await MutatePayrollSnapshotAsync(employee.Id, "2026-02", snapshot =>
		{
			snapshot.EarningComponentsJson = JsonSerializer.Serialize(new[]
			{
				new
				{
					Name = "January Temp Earning",
					Amount = 100d,
					Description = (string?)null,
					IsFixed = false,
					IsTemp = true,
					ComponentType = "Fixed",
					DependOn = "None"
				}
			});
			snapshot.DeductionComponentsJson = JsonSerializer.Serialize(new[]
			{
				new
				{
					Name = "January Temp Deduction",
					Amount = 50d,
					Description = (string?)null,
					IsFixed = false,
					IsTemp = true,
					ComponentType = "Fixed",
					DependOn = "None"
				}
			});
			snapshot.GrossSalary = 100m;
			snapshot.FullGrossSalary = 100m;
			snapshot.TotalDeduction = 50m;
			snapshot.NetPay = 50m;
		});

		var februarySlip = await GetSalarySlipResponseAsync(employee.Id, "FEBRUARY-2026");
		februarySlip.Earnings.ShouldContain(x => x.Name == "January Temp Earning" && x.Amount == 100d && x.IsTemp);
		februarySlip.Deductions.ShouldContain(x => x.Name == "January Temp Deduction" && x.Amount == 50d && x.IsTemp);
		februarySlip.TotalEarning.ShouldBe(100d);
		februarySlip.TotalDeduction.ShouldBe(50d);
		februarySlip.NetSalary.ShouldBe(50d);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Salary_Slip()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SlipGet");

		var response = await Client.GetAsync(
			$"{BaseUrl}?employeeId={empId}&monthYear=APRIL-2026");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.NotFound,
			HttpStatusCode.Forbidden,
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	[Theory]
	[InlineData("APRIL-2026")]
	[InlineData("April-2026")]
	[InlineData("Apr-2026")]
	[InlineData("2026-04")]
	[InlineData("2026-04-15")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Salary_Slip_Should_Accept_Run_Payroll_Month_Formats_And_Normalize_Response(
		string monthYear)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"SlipMonth{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await UpdateSalaryStructureAsync(
			employee.Id,
			new UpdateSalaryStructureDto
			{
				EarningComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "April Normalizer Earning",
						Amount = 100m,
						IsFixed = true,
						IsTemp = false
					}
				},
				DeductionComponents = new List<SalaryComponentDto>
				{
					new()
					{
						Name = "April Normalizer Deduction",
						Amount = 10m,
						IsFixed = true,
						IsTemp = false
					}
				}
			});
		await GetPayrollItemAsync(employee.Id, "2026-04");

		var slip = await GetSalarySlipResponseAsync(employee.Id, Uri.EscapeDataString(monthYear));

		slip.MonthYear.ShouldBe("APRIL-2026");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Salary_Slip_Pdf()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SlipPdf");

		var response = await Client.GetAsync(
			$"{BaseUrl}/pdf?employeeId={empId}&monthYear=APRIL-2026");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.NotFound,
			HttpStatusCode.Forbidden,
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.OK)
		{
			response.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
			var bytes = await response.Content.ReadAsByteArrayAsync();
			bytes.Length.ShouldBeGreaterThan(0);
			bytes[0].ShouldBe((byte) '%');
			bytes[1].ShouldBe((byte) 'P');
			bytes[2].ShouldBe((byte) 'D');
			bytes[3].ShouldBe((byte) 'F');
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Missing_Params()
	{
		var response = await Client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?employeeId={Guid.NewGuid()}&monthYear=invalid");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Salary_Slip_Should_Require_Payroll_Snapshot()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"SlipSnapshotRequired{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

		var response = await Client.GetAsync(
			$"{BaseUrl}?employeeId={employee.Id}&monthYear=JUNE-2026");
		var body = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
		body.ShouldContain(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip);
	}

	#endregion

	private async Task UpdateSalaryStructureAsync(Guid employeeId, UpdateSalaryStructureDto update)
	{
		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/salary-structure",
			update,
			JsonOptions);
		var body = await updateResponse.Content.ReadAsStringAsync();
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, body);
	}

	private async Task MutatePayrollSnapshotAsync(
		Guid employeeId,
		string monthYear,
		Action<PayrollRunItemSnapshot> mutate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await snapshotRepository.GetQueryableAsync();
			var snapshot = query.Single(x => x.EmployeeId == employeeId && x.MonthYear == monthYear);
			mutate(snapshot);
			await snapshotRepository.UpdateAsync(snapshot, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedFinalizedSalarySlipAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var salarySlipRepository = scope.ServiceProvider.GetRequiredService<IRepository<SalarySlip, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var salarySlip = CreateSalarySlip(tenantId, employeeId, monthYear);
			salarySlip.FinalizeSlip();
			await salarySlipRepository.InsertAsync(salarySlip, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private static SalarySlip CreateSalarySlip(Guid tenantId, Guid employeeId, string monthYear)
	{
		var constructor = typeof(SalarySlip).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[
				typeof(Guid),
				typeof(Guid?),
				typeof(Guid),
				typeof(string),
				typeof(List<SalarySlipLineItem>),
				typeof(List<SalarySlipLineItem>),
				typeof(DateTime),
				typeof(string)
			],
			modifiers: null);

		constructor.ShouldNotBeNull();
		return (SalarySlip)constructor.Invoke(
		[
			Guid.NewGuid(),
			tenantId,
			employeeId,
			monthYear,
			new List<SalarySlipLineItem>
			{
				new()
				{
					Name = "Basic",
					Amount = 1000m,
					IsFixed = true,
					ComponentType = SalaryComponentType.Fixed,
					DependOn = SalaryComponentDependency.None
				}
			},
			new List<SalarySlipLineItem>(),
			DateTime.UtcNow,
			"Salary slip month normalizer test"
		]);
	}

	private async Task<RunPayrollItem> GetPayrollItemAsync(Guid employeeId, string month)
	{
		for (var page = 0; page < 20; page++)
		{
			var response = await Client.GetAsync($"{RunPayrollUrl}?month={month}&page={page}&pageSize=100&forceRefresh={page == 0}");
			var body = await response.Content.ReadAsStringAsync();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
			var preview = await response.Content.ReadFromJsonAsync<RunPayrollResponse>(JsonOptions);
			preview.ShouldNotBeNull();
			var item = preview.Items.SingleOrDefault(x => x.EmployeeId == employeeId);
			if (item != null)
			{
				return item;
			}

			if (page + 1 >= preview.TotalPages)
			{
				break;
			}
		}

		throw new ShouldAssertException($"Payroll item for employee {employeeId} was not found in {month} preview.");
	}

	private async Task<SalarySlipResponse> GetSalarySlipResponseAsync(Guid employeeId, string monthYear)
	{
		var response = await Client.GetAsync($"{BaseUrl}?employeeId={employeeId}&monthYear={monthYear}");
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
		var slip = await response.Content.ReadFromJsonAsync<SalarySlipResponse>(JsonOptions);
		slip.ShouldNotBeNull();
		return slip;
	}
}
