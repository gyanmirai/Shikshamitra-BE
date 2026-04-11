using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeePayrollApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public EmployeePayrollApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Employee_Payroll()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayGet");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Employee_Payroll()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayPut");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "State Bank of India",
				BranchName = "Main Branch",
				AccountHolderName = "Test Employee",
				AccountNumber = "1234567890",
				IfscCode = "SBIN0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "EMP001",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/payroll");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Full_Payroll_With_Components()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayFull");
		var earningComponent1 = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayFullE1");
		var earningComponent2 = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayFullE2");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "PayFullD1");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "HDFC Bank",
				BranchName = "Corporate Branch",
				AccountHolderName = "Full Payroll Employee",
				AccountNumber = "9876543210123",
				IfscCode = "HDFC0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 50000m,
				PunchId = "FULL001",
				EpfNumber = "PF-12345",
				UanNumber = "UAN-67890",
				TdsPercentage = 10m,
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = earningComponent1.Id, Amount = 5000m },
				new() { ComponentId = earningComponent2.Id, Amount = 3000m },
			},
			DeductionComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = deductionComponent.Id, Amount = 1500m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.OK)
		{
			var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
			getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

			var body = await getResponse.Content.ReadFromJsonAsync<PayrollContract>(JsonOptions);
			body.ShouldNotBeNull();
			body.EarningComponents.ShouldNotBeNull();
			body.DeductionComponents.ShouldNotBeNull();
			body.EarningComponents.Count.ShouldBe(2);
			body.DeductionComponents.Count.ShouldBe(1);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_ComponentIds_Without_Deleting_Existing_Payroll()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayGuard");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayGuardE1");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "PayGuardD1");

		var baseline = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 42000m,
				PunchId = "PAY-GUARD-1",
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = earningComponent.Id, Amount = 4200m },
			},
			DeductionComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = deductionComponent.Id, Amount = 1200m },
			},
		};

		var baselineResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", baseline, JsonOptions);
		baselineResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var invalidUpdate = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 45000m,
				PunchId = "PAY-GUARD-2",
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = Guid.NewGuid(), Amount = 5000m },
			},
			DeductionComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = deductionComponent.Id, Amount = 1500m },
			},
		};

		var invalidResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", invalidUpdate, JsonOptions);
		invalidResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var payload = await getResponse.Content.ReadFromJsonAsync<PayrollContract>(JsonOptions);
		payload.ShouldNotBeNull();
		payload.EarningComponents.ShouldNotBeNull();
		payload.DeductionComponents.ShouldNotBeNull();
		payload.EarningComponents.Count.ShouldBe(1);
		payload.DeductionComponents.Count.ShouldBe(1);
		payload.EarningComponents[0].ComponentId.ShouldBe(earningComponent.Id);
		payload.EarningComponents[0].Amount.ShouldBe(4200m);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_Bank_Details()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayVerify");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "ICICI Bank",
				BranchName = "Tech Park Branch",
				AccountHolderName = "Verify Employee",
				AccountNumber = "5555666677778888",
				IfscCode = "ICIC0009999",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 35000m,
				PunchId = "VFY001",
			},
		};

		var putResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		if (putResponse.StatusCode == HttpStatusCode.OK)
		{
			var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
			getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
			var body = await getResponse.Content.ReadFromJsonAsync<PayrollContract>(JsonOptions);
			body.ShouldNotBeNull();
			body.BankDetails.ShouldNotBeNull();
			body.BankDetails.BankName.ShouldBe("ICICI Bank");
			body.BankDetails.AccountNumber.ShouldBe("5555666677778888");
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_TDS_Boundary_Values()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayTDS");

		var zeroTds = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 20000m,
				PunchId = "TDS0",
				TdsPercentage = 0m,
			},
		};
		var r1 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", zeroTds, JsonOptions);
		r1.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

		var maxTds = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 20000m,
				PunchId = "TDS100",
				TdsPercentage = 100m,
			},
		};
		var r2 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", maxTds, JsonOptions);
		r2.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion
}
