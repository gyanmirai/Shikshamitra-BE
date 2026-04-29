using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeSalaryStructureApi_Tests : ShikshaMitraApiTestBase
{
	public EmployeeSalaryStructureApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	private static string Url(Guid empId) => $"/api/payroll/employees/{empId}/salary-structure";

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Salary_Structure()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalGet");

		var response = await Client.GetAsync(Url(empId));

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_NotFound_When_Getting_Salary_Structure_For_NonExistent_Employee()
	{
		var response = await Client.GetAsync(Url(Guid.NewGuid()));
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Salary_Structure()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalPut");
		var clientProvidedEarningRowId = Guid.NewGuid();
		var clientProvidedDeductionRowId = Guid.NewGuid();
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalEarn");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalDed");
		var applicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = clientProvidedEarningRowId,
					ComponentId = earningComponent.Id,
					Name = "Basic Salary",
					Amount = 25000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = applicableMonth,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = clientProvidedDeductionRowId,
					ComponentId = deductionComponent.Id,
					Name = "PF Deduction",
					Amount = 3000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = applicableMonth,
				}
			},
		};

		var response = await Client.PutAsJsonAsync(Url(empId), updateDto, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync(Url(empId));
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		payload.ShouldNotBeNull();
		payload.EarningComponents.Count.ShouldBe(1);
		payload.DeductionComponents.Count.ShouldBe(1);

		payload.EarningComponents[0].Id.ShouldNotBe(Guid.Empty);
		payload.EarningComponents[0].Id.ShouldNotBe(clientProvidedEarningRowId);
		payload.EarningComponents[0].ComponentId.ShouldBe(earningComponent.Id);
		payload.EarningComponents[0].Name.ShouldBe("Basic Salary");
		payload.EarningComponents[0].Amount.ShouldBe(25000m);
		payload.EarningComponents[0].IsFixed.ShouldBeTrue();
		payload.EarningComponents[0].IsTemp.ShouldBeFalse();
		payload.EarningComponents[0].ApplicableMonth.ShouldBe(applicableMonth);

		payload.DeductionComponents[0].Id.ShouldNotBe(Guid.Empty);
		payload.DeductionComponents[0].Id.ShouldNotBe(clientProvidedDeductionRowId);
		payload.DeductionComponents[0].ComponentId.ShouldBe(deductionComponent.Id);
		payload.DeductionComponents[0].Name.ShouldBe("PF Deduction");
		payload.DeductionComponents[0].Amount.ShouldBe(3000m);
		payload.DeductionComponents[0].IsFixed.ShouldBeTrue();
		payload.DeductionComponents[0].IsTemp.ShouldBeFalse();
		payload.DeductionComponents[0].ApplicableMonth.ShouldBe(applicableMonth);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_NotFound_When_Updating_Salary_Structure_For_NonExistent_Employee()
	{
		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					Name = "Basic Salary",
					Amount = 10000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					Name = "PF",
					Amount = 1200m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				}
			}
		};

		var response = await Client.PutAsJsonAsync(Url(Guid.NewGuid()), updateDto, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Null_ApplicableMonth_When_Not_Provided()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalNullMonth");

		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					Name = "Basic Salary",
					Amount = 18000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					Name = "PF",
					Amount = 1000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			}
		};

		var putResponse = await Client.PutAsJsonAsync(Url(empId), updateDto, JsonOptions);
		putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync(Url(empId));
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		payload.ShouldNotBeNull();
		payload.EarningComponents.Count.ShouldBe(1);
		payload.DeductionComponents.Count.ShouldBe(1);
		payload.EarningComponents[0].ApplicableMonth.ShouldBeNull();
		payload.DeductionComponents[0].ApplicableMonth.ShouldBeNull();
	}

	#endregion

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Legacy_Organization_PayrollStructure_Endpoint_Should_Return_Deprecation_Error()
	{
		var stageId = Guid.NewGuid();
		var response = await Client.GetAsync($"/api/education-medium-stage/{stageId}/payroll-structure");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain("HR:Payroll:030020");
		body.ShouldContain("/api/payroll/employees/{employeeId}/salary-structure");
	}
}
