using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.Infrastructure.Auth;
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
	public async Task Should_Update_Salary_Structure_With_No_Deductions()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalNoDed");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalNoDedEarn");

		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = earningComponent.Id,
					Name = "Basic Salary",
					Amount = 25000m,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents = []
		};

		var response = await Client.PutAsJsonAsync(Url(empId), updateDto, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var getResponse = await Client.GetAsync(Url(empId));
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		payload.ShouldNotBeNull();
		payload.EarningComponents.Count.ShouldBe(1);
		payload.DeductionComponents.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Mark_Generated_Structure_Modified_When_A_Recurring_Row_Changes()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalModified");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalModifiedEarn");
		var generatedAt = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
		var templateId = Guid.NewGuid();
		var versionId = Guid.NewGuid();
		var initial = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new SalaryComponentDto
				{
					ComponentId = earningComponent.Id,
					Name = "Basic Salary",
					Amount = 18000m,
					IsFixed = true,
					UsesMasterValue = false,
					IsTemp = false
				}
			],
			DeductionComponents = [],
			QuotedSalaryBasis = "GROSS",
			QuotedMonthlyAmount = 18000m,
			SourceSalaryTemplateId = templateId,
			SourceSalaryTemplateVersionId = versionId,
			SourceSalaryTemplateVersion = 1,
			SalaryGeneratedAt = generatedAt,
			WasModifiedAfterGeneration = false
		};
		(await Client.PutAsJsonAsync(Url(empId), initial, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);

		initial.EarningComponents[0].Amount = 19000m;
		initial.WasModifiedAfterGeneration = false;
		(await Client.PutAsJsonAsync(Url(empId), initial, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await (await Client.GetAsync(Url(empId)))
			.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		payload.ShouldNotBeNull();
		payload.WasModifiedAfterGeneration.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Salary_Structure_With_Employee_Edit_And_Payroll_Permission()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalEditPerm");
		using var client = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add);

		var response = await client.PutAsJsonAsync(Url(empId), BuildSalaryStructureUpdate(), JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Salary_Structure_Update_Without_Employee_Edit()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalNoEdit");
		using var client = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add);

		var response = await client.PutAsJsonAsync(Url(empId), BuildSalaryStructureUpdate(), JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
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

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Recalculate_Percentage_Earning_When_Replacing_Component()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalReplacePctEarn");
		var basic = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalPctBasicEarn");
		var oldEarning = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalOldEarn");
		var deduction = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalPctEarnDed");
		var hra = await PayrollTestHelper.CreatePercentageEarningComponentAsync(
			Client,
			basic.Id,
			10m,
			"SalHraPct");

		var firstUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = basic.Id,
					Name = "Basic Salary",
					Amount = 30000m,
					IsFixed = true,
					IsTemp = false
				},
				new()
				{
					ComponentId = oldEarning.Id,
					Name = oldEarning.Name,
					Amount = 5000m,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents =
			[
				new()
				{
					ComponentId = deduction.Id,
					Name = deduction.Name,
					Amount = 500m,
					IsFixed = true,
					IsTemp = false
				}
			]
		};

		var firstResponse = await Client.PutAsJsonAsync(Url(empId), firstUpdate, JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var replaceUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = basic.Id,
					Name = "Basic Salary",
					Amount = 30000m,
					IsFixed = true,
					IsTemp = false
				},
				new()
				{
					ComponentId = hra.Id,
					Name = hra.Name,
					Amount = null,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents =
			[
				new()
				{
					ComponentId = deduction.Id,
					Name = deduction.Name,
					Amount = 500m,
					IsFixed = true,
					IsTemp = false
				}
			]
		};

		var replaceResponse = await Client.PutAsJsonAsync(Url(empId), replaceUpdate, JsonOptions);
		replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync(Url(empId));
		var saved = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		saved.ShouldNotBeNull();
		var savedHra = saved.EarningComponents.Single(x => x.ComponentId == hra.Id);
		savedHra.Amount.ShouldBe(3000m);
		savedHra.IsFixed.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Recalculate_Percentage_Deduction_When_Replacing_Component()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalReplacePctDed");
		var basic = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalPctBasicDed");
		var oldDeduction = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalOldDed");
		var pf = await PayrollTestHelper.CreatePercentageDeductionComponentAsync(
			Client,
			basic.Id,
			12m,
			"SalPfPct");

		var firstUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = basic.Id,
					Name = "Basic Salary",
					Amount = 30000m,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents =
			[
				new()
				{
					ComponentId = oldDeduction.Id,
					Name = oldDeduction.Name,
					Amount = 500m,
					IsFixed = true,
					IsTemp = false
				}
			]
		};

		var firstResponse = await Client.PutAsJsonAsync(Url(empId), firstUpdate, JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var replaceUpdate = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = basic.Id,
					Name = "Basic Salary",
					Amount = 30000m,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents =
			[
				new()
				{
					ComponentId = pf.Id,
					Name = pf.Name,
					Amount = null,
					IsFixed = true,
					IsTemp = false
				}
			]
		};

		var replaceResponse = await Client.PutAsJsonAsync(Url(empId), replaceUpdate, JsonOptions);
		replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync(Url(empId));
		var saved = await getResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		saved.ShouldNotBeNull();
		var savedPf = saved.DeductionComponents.Single(x => x.ComponentId == pf.Id);
		savedPf.Amount.ShouldBe(3600m);
		savedPf.IsFixed.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Percentage_Deduction_When_Base_Component_Is_Missing()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalMissingPctBase");
		var basic = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalMissingBaseBasic");
		var allowance = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalMissingBaseAllowance");
		var pf = await PayrollTestHelper.CreatePercentageDeductionComponentAsync(
			Client,
			basic.Id,
			12m,
			"SalMissingBasePf");

		var update = new UpdateSalaryStructureDto
		{
			EarningComponents =
			[
				new()
				{
					ComponentId = allowance.Id,
					Name = allowance.Name,
					Amount = 5000m,
					IsFixed = true,
					IsTemp = false
				}
			],
			DeductionComponents =
			[
				new()
				{
					ComponentId = pf.Id,
					Name = pf.Name,
					Amount = null,
					IsFixed = true,
					IsTemp = false
				}
			]
		};

		var response = await Client.PutAsJsonAsync(Url(empId), update, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	#endregion

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	private static UpdateSalaryStructureDto BuildSalaryStructureUpdate() => new()
	{
		EarningComponents =
		[
			new()
			{
				Name = "Basic Salary",
				Amount = 10000m,
				IsFixed = true,
				IsTemp = false
			}
		],
		DeductionComponents =
		[
			new()
			{
				Name = "Professional Tax",
				Amount = 200m,
				IsFixed = true,
				IsTemp = false
			}
		]
	};
}
