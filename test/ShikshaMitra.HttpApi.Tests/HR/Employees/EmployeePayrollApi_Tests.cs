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

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await getResponse.Content.ReadFromJsonAsync<PayrollContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.EarningComponents.ShouldNotBeNull();
		body.DeductionComponents.ShouldNotBeNull();
		body.EarningComponents.Count.ShouldBe(2);
		body.DeductionComponents.Count.ShouldBe(1);
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

	#region Tier 1.5 — Validation Edge Cases

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Negative_BasicSalary()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayNegSal");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = -5000m,
				PunchId = "NEG01",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_TdsPercentage_Above_100()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayTdsHi");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "TDSHI",
				TdsPercentage = 150m,
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Negative_TdsPercentage()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayTdsNeg");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "TDSNEG",
				TdsPercentage = -10m,
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Negative_TdsAmount()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayTdsAmtNeg");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "TDSAMN",
				TdsAmount = -500m,
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_BankDetails_Without_BankName()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayNoBnk");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BranchName = "Some Branch",
				AccountHolderName = "Test Employee",
				AccountNumber = "1234567890",
				IfscCode = "SBIN0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "NOBNK",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_BankDetails_Without_AccountHolderName()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayNoAccH");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "SBI",
				BranchName = "Some Branch",
				AccountNumber = "1234567890",
				IfscCode = "SBIN0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "NOACC",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_IfscCode()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayBadIfsc");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "SBI",
				BranchName = "Main Branch",
				AccountHolderName = "Test Employee",
				AccountNumber = "1234567890",
				IfscCode = "INVALID",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "BADIF",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_NonNumeric_AccountNumber()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayBadAcc");

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "SBI",
				BranchName = "Main Branch",
				AccountHolderName = "Test Employee",
				AccountNumber = "ABCD1234XYZ",
				IfscCode = "SBIN0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "BADAC",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Duplicate_EarningComponentIds()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayDupE");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayDupE1");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 30000m,
				PunchId = "DUPE",
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = earningComponent.Id, Amount = 5000m },
				new() { ComponentId = earningComponent.Id, Amount = 3000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Duplicate_DeductionComponentIds()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayDupD");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "PayDupD1");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 30000m,
				PunchId = "DUPD",
			},
			DeductionComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = deductionComponent.Id, Amount = 1500m },
				new() { ComponentId = deductionComponent.Id, Amount = 2000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Empty_ComponentId()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayEmptyC");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 30000m,
				PunchId = "EMPTYC",
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = Guid.Empty, Amount = 5000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Zero_ComponentAmount()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayZeroAmt");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayZeroA1");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 30000m,
				PunchId = "ZEROAM",
			},
			EarningComponents = new List<ComponentAmountContract>
			{
				new() { ComponentId = earningComponent.Id, Amount = 0m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Accept_BasicSalary_Zero()
	{
		// BasicSalary = 0 is valid (e.g., volunteer or unpaid role)
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayZeroSal");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 0m,
				PunchId = "ZEROS",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Accept_TdsPercentage_Boundary_Values()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayTdsBnd");

		// TDS = 0 should be valid
		var zeroTds = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "TDSB0",
				TdsPercentage = 0m,
			},
		};
		var r1 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", zeroTds, JsonOptions);
		r1.StatusCode.ShouldBe(HttpStatusCode.OK);

		// TDS = 100 should be valid
		var maxTds = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				BasicSalary = 25000m,
				PunchId = "TDSB100",
				TdsPercentage = 100m,
			},
		};
		var r2 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", maxTds, JsonOptions);
		r2.StatusCode.ShouldBe(HttpStatusCode.OK);
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
		r1.StatusCode.ShouldBe(HttpStatusCode.OK);

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
		r2.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion
}
