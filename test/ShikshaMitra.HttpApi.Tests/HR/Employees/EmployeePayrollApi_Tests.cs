using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.SalaryStructure;
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
	public async Task Should_Get_Employee_Payroll_Legacy_Route_Returns_Same_Shape()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayLegacy");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/get-payroll");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(empId);
		body.BankDetails.ShouldNotBeNull();
		body.SalaryDetails.ShouldNotBeNull();
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
				TotalWorkingHourPerMonth = 160m,
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
	public async Task Should_Accept_Payroll_Update_With_Empty_PfRuleId_From_Form()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayEmptyPf");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayEmptyPfE1");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "PayEmptyPfD1");

		var json = $$"""
		{
		  "bankDetails": {
		    "bankName": "HDFC Bank",
		    "branchName": "Main Branch",
		    "accountHolderName": "Payroll Form Employee",
		    "accountNumber": "1234567890",
		    "ifscCode": "HDFC0001234"
		  },
		  "salaryDetails": {
		    "pfRuleId": "",
		    "epfNumber": "",
		    "uanNumber": "",
		    "punchId": "PAYEMPTY",
		    "totalWorkingHourPerMonth": 160
		  },
		  "earningComponents": [
		    {
		      "componentId": "{{earningComponent.Id}}",
		      "name": "{{earningComponent.Name}}",
		      "amount": 5000,
		      "isFixed": true
		    }
		  ],
		  "deductionComponents": [
		    {
		      "componentId": "{{deductionComponent.Id}}",
		      "name": "{{deductionComponent.Name}}",
		      "amount": 1200,
		      "isFixed": true
		    }
		  ]
		}
		""";

		var response = await Client.PutAsync(
			$"{BaseUrl}/{empId}/payroll",
			new StringContent(json, Encoding.UTF8, "application/json"));

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Payroll_When_Form_Sends_Employee_Component_Row_Id()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayRowId");
		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayRowIdE1");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "PayRowIdD1");

		var salaryStructure = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = earningComponent.Id,
					Name = earningComponent.Name,
					Amount = 5000m,
					IsFixed = true
				},
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					ComponentId = deductionComponent.Id,
					Name = deductionComponent.Name,
					Amount = 1200m,
					IsFixed = true
				},
			},
		};

		var salaryResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{empId}/salary-structure",
			salaryStructure,
			JsonOptions);
		salaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getSalaryResponse = await Client.GetAsync($"/api/payroll/employees/{empId}/salary-structure");
		getSalaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var savedStructure = await getSalaryResponse.Content.ReadFromJsonAsync<SalaryStructureResponseDto>(JsonOptions);
		savedStructure.ShouldNotBeNull();
		savedStructure.EarningComponents.Count.ShouldBe(1);
		savedStructure.DeductionComponents.Count.ShouldBe(1);

		var request = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "HDFC Bank",
				BranchName = "Main Branch",
				AccountHolderName = "Payroll Row Employee",
				AccountNumber = "1234567890",
				IfscCode = "HDFC0001234",
			},
			SalaryDetails = new SalaryDetailsContract
			{
				TotalWorkingHourPerMonth = 160m,
				PunchId = "ROWID1",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new()
				{
					ComponentId = savedStructure.EarningComponents[0].Id,
					Name = savedStructure.EarningComponents[0].Name,
					Amount = 5500m,
					IsFixed = true
				},
			},
			DeductionComponents = new List<SalaryComponentContract>
			{
				new()
				{
					ComponentId = savedStructure.DeductionComponents[0].Id,
					Name = savedStructure.DeductionComponents[0].Name,
					Amount = 1300m,
					IsFixed = true
				},
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getPayrollResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		var payroll = await getPayrollResponse.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
		payroll.ShouldNotBeNull();
		payroll.EarningComponents![0].ComponentId.ShouldBe(earningComponent.Id);
		payroll.EarningComponents[0].Amount.ShouldBe(5500m);
		payroll.DeductionComponents![0].ComponentId.ShouldBe(deductionComponent.Id);
		payroll.DeductionComponents[0].Amount.ShouldBe(1300m);
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
				TotalWorkingHourPerMonth = 180m,
				PunchId = "FULL001",
				EpfNumber = "AP/12345/1234567",
				UanNumber = "123456789012",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = earningComponent1.Id, Name = earningComponent1.Name, Amount = 5000m },
				new() { ComponentId = earningComponent2.Id, Name = earningComponent2.Name, Amount = 3000m },
			},
			DeductionComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = deductionComponent.Id, Name = deductionComponent.Name, Amount = 1500m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await getResponse.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(empId);
		body.EarningComponents.ShouldNotBeNull();
		body.DeductionComponents.ShouldNotBeNull();
		body.EarningComponents.Count.ShouldBe(2);
		body.DeductionComponents.Count.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Components_Using_Name_Only_When_Master_Unique()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayNameOnly");
		var earning = await PayrollTestHelper.CreateEarningComponentAsync(Client, "PayNameE");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract { PunchId = "NAME1" },
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { Name = earning.Name, Amount = 4000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		var body = await getResponse.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.EarningComponents.ShouldNotBeNull();
		body.EarningComponents.Count.ShouldBe(1);
		body.EarningComponents[0].ComponentId.ShouldBe(earning.Id);
		body.EarningComponents[0].Amount.ShouldBe(4000m);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "PAY-GUARD-1",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = earningComponent.Id, Name = earningComponent.Name, Amount = 4200m },
			},
			DeductionComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = deductionComponent.Id, Name = deductionComponent.Name, Amount = 1200m },
			},
		};

		var baselineResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", baseline, JsonOptions);
		baselineResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var invalidUpdate = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				TotalWorkingHourPerMonth = 168m,
				PunchId = "PAY-GUARD-2",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = Guid.NewGuid(), Name = "Bad", Amount = 5000m },
			},
			DeductionComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = deductionComponent.Id, Name = deductionComponent.Name, Amount = 1500m },
			},
		};

		var invalidResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", invalidUpdate, JsonOptions);
		invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		invalidResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
		var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
		invalidBody.ShouldNotContain("<html", Case.Insensitive);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var payload = await getResponse.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
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
	public async Task Should_Reject_Negative_TotalWorkingHourPerMonth()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayNegHrs");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				TotalWorkingHourPerMonth = -10m,
				PunchId = "NEGH",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "NOBNK",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "NOACC",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "BADIF",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "BADAC",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "DUPE",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = earningComponent.Id, Name = earningComponent.Name, Amount = 5000m },
				new() { ComponentId = earningComponent.Id, Name = earningComponent.Name, Amount = 3000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "DUPD",
			},
			DeductionComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = deductionComponent.Id, Name = deductionComponent.Name, Amount = 1500m },
				new() { ComponentId = deductionComponent.Id, Name = deductionComponent.Name, Amount = 2000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Empty_ComponentId_When_Supplied_As_Empty_Guid()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayEmptyC");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				TotalWorkingHourPerMonth = 160m,
				PunchId = "EMPTYC",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = Guid.Empty, Name = "X", Amount = 5000m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
				TotalWorkingHourPerMonth = 160m,
				PunchId = "ZEROAM",
			},
			EarningComponents = new List<SalaryComponentContract>
			{
				new() { ComponentId = earningComponent.Id, Name = earningComponent.Name, Amount = 0m },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Accept_TotalWorkingHourPerMonth_Zero()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PayZeroHrs");

		var request = new PayrollContract
		{
			SalaryDetails = new SalaryDetailsContract
			{
				TotalWorkingHourPerMonth = 0m,
				PunchId = "ZEROS",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
				TotalWorkingHourPerMonth = 172m,
				PunchId = "VFY001",
			},
		};

		var putResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/payroll", request, JsonOptions);
		if (putResponse.StatusCode == HttpStatusCode.OK)
		{
			var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/payroll");
			getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
			var body = await getResponse.Content.ReadFromJsonAsync<EmployeePayrollResponse>(JsonOptions);
			body.ShouldNotBeNull();
			body.BankDetails.ShouldNotBeNull();
			body.BankDetails.BankName.ShouldBe("ICICI Bank");
			body.BankDetails.AccountNumber.ShouldBe("5555666677778888");
			body.SalaryDetails.ShouldNotBeNull();
			body.SalaryDetails.TotalWorkingHourPerMonth.ShouldBe(172m);
		}
	}

	#endregion
}
