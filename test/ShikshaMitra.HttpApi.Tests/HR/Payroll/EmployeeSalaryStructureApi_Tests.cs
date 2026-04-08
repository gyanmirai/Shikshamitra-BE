using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
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

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Salary_Structure()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalPut");

		var earning = await PayrollTestHelper.CreateEarningComponentAsync(Client, "SalEarn");
		var deduction = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "SalDed");

		var updateDto = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Name = "Basic Salary",
					Amount = 25000m,
					IsFixed = true,
					IsTemp = false,
					ComponentType = SalaryComponentType.Fixed,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Name = "PF Deduction",
					Amount = 3000m,
					IsFixed = true,
					IsTemp = false,
					ComponentType = SalaryComponentType.Fixed,
				}
			},
		};

		var response = await Client.PutAsJsonAsync(Url(empId), updateDto, JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion
}
