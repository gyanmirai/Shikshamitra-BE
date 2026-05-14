using System;
using System.Collections.Generic;
using System.Linq;
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

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_ReSubmit_Temporary_Component_When_ComponentId_Is_Row_Id()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalTempRowId");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalTempMaster");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalTempDed");
		var tempApplicableMonth = new DateTime(2027, 8, 1, 0, 0, 0, DateTimeKind.Utc);

		var firstUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = earningComponent.Id,
					Name = earningComponent.Name,
					Amount = 10000m,
					IsFixed = true,
					IsTemp = false,
				},
				new()
				{
					Name = "Temp earning 1",
					Amount = 1222m,
					IsFixed = false,
					IsTemp = true,
					ApplicableMonth = tempApplicableMonth,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = deductionComponent.Id,
					Name = deductionComponent.Name,
					Amount = 220m,
					IsFixed = true,
					IsTemp = false,
				}
			}
		};

		var firstResponse = await Client.PutAsJsonAsync(Url(empId), firstUpdate, JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync(Url(empId));
		var saved = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		saved.ShouldNotBeNull();
		saved.EarningComponents.Count(x => x.IsTemp).ShouldBe(1);
		var temp = saved.EarningComponents.First(x => x.IsTemp);
		temp.ComponentId.ShouldBeNull();

		temp.ComponentId = temp.Id;
		var secondUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents = saved.EarningComponents,
			DeductionComponents = saved.DeductionComponents
		};

		var secondResponse = await Client.PutAsJsonAsync(Url(empId), secondUpdate, JsonOptions);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var verifyResponse = await Client.GetAsync(Url(empId));
		var verified = await verifyResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		verified.ShouldNotBeNull();
		verified.EarningComponents.Count(x => x.IsTemp).ShouldBe(1);
		var verifiedTemp = verified.EarningComponents.First(x => x.IsTemp);
		verifiedTemp.Name.ShouldBe("Temp earning 1");
		verifiedTemp.Amount.ShouldBe(1222m);
		verifiedTemp.ComponentId.ShouldBeNull();
		verifiedTemp.ApplicableMonth.ShouldBe(tempApplicableMonth);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Add_New_Dropdown_Components_With_Null_Id_When_ReSubmitting_Salary_Structure()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalNullIdReAdd");
		var existingEarning = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalNullIdExistingEarn");
		var existingDeduction = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalNullIdExistingDed");
		var newEarning = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalNullIdNewEarn");
		var newDeduction = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalNullIdNewDed");

		var firstUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = existingEarning.Id,
					Name = existingEarning.Name,
					Amount = 10000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = existingDeduction.Id,
					Name = existingDeduction.Name,
					Amount = 1000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			}
		};

		var firstResponse = await Client.PutAsJsonAsync(Url(empId), firstUpdate, JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var savedResponse = await Client.GetAsync(Url(empId));
		var saved = await savedResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		saved.ShouldNotBeNull();

		var secondUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				saved.EarningComponents.Single(),
				new()
				{
					Id = null,
					ComponentId = newEarning.Id,
					Name = newEarning.Name,
					Amount = 200m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				saved.DeductionComponents.Single(),
				new()
				{
					Id = null,
					ComponentId = newDeduction.Id,
					Name = newDeduction.Name,
					Amount = 200m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			}
		};

		var secondResponse = await Client.PutAsJsonAsync(Url(empId), secondUpdate, JsonOptions);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var verifyResponse = await Client.GetAsync(Url(empId));
		var verified = await verifyResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		verified.ShouldNotBeNull();
		verified.EarningComponents.Count.ShouldBe(2);
		verified.DeductionComponents.Count.ShouldBe(2);
		verified.EarningComponents.ShouldContain(x => x.ComponentId == newEarning.Id && x.Id.HasValue && x.Amount == 200m);
		verified.DeductionComponents.ShouldContain(x => x.ComponentId == newDeduction.Id && x.Id.HasValue && x.Amount == 200m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Temporary_Component_With_Forged_ComponentId()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalForgedTemp");

		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = Guid.NewGuid(),
					Name = "Forged temp earning",
					Amount = 1000m,
					IsFixed = false,
					IsTemp = true,
					ApplicableMonth = new DateTime(2027, 8, 1, 0, 0, 0, DateTimeKind.Utc),
				}
			},
			DeductionComponents = new List<SalaryComponentDto>()
		};

		var response = await Client.PutAsJsonAsync(Url(empId), updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldNotContain("<html", Case.Insensitive);
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
