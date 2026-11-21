using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryApiTestCollection.Name)]
public partial class LibraryCanonicalLoanApi_Tests(LibraryApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string LoansRoute = "/api/library/circulation/loans";

	[Fact]
	public async Task Canonical_Http_Checkout_And_Return_Should_Preserve_Civil_Dates_And_Replay_The_Exact_Loan()
	{
		var input = await CheckoutRequestAsync();
		var issued = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		issued.Loan.RawIssueDate.ShouldBeNull();
		issued.Loan.SchoolIssueDate.ShouldBe(input.IssueDate);
		issued.Loan.SchoolDueDate.ShouldBe(input.RequestedDueDate);
		var replay = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		replay.WasReplay.ShouldBeTrue();
		replay.Loan.Id.ShouldBe(issued.Loan.Id);
		var current = await GetAndReadAsync<LibraryLoanDto>($"{LoansRoute}/{issued.Loan.Id}");
		var receipt = new LibraryLoanReturnRequest
		{
			CopyId = current.CopyId, Revision = current.Revision, SchoolReturnDate = new(2026, 11, 18),
			IdempotencyKey = Guid.NewGuid().ToString("N"), CopyStatus = 0
		};
		var returned = await PostAndReadAsync<LibraryLoanCommandResultDto>($"{LoansRoute}/{current.Id}/return", receipt);
		returned.Loan.SchoolReturnDate.ShouldBe(receipt.SchoolReturnDate);
		returned.Loan.RawReturnDate.ShouldBeNull();
		returned.Loan.CanReturn.ShouldBeFalse();
		var returnReplay = await PostAndReadAsync<LibraryLoanCommandResultDto>($"{LoansRoute}/{current.Id}/return", receipt);
		returnReplay.WasReplay.ShouldBeTrue();
		returnReplay.Loan.Id.ShouldBe(current.Id);
	}

	[Fact]
	public async Task A_Changed_Http_Checkout_Payload_Should_Not_Reuse_An_Accepted_Key()
	{
		var input = await CheckoutRequestAsync();
		var issued = await PostAndReadAsync<LibraryLoanCommandResultDto>(LoansRoute, input);
		input.RequestedDueDate = input.RequestedDueDate!.Value.AddDays(1);
		var response = await Client.PostAsJsonAsync(LoansRoute, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryCirculationErrorCodes.CommandKeyReused);
		(await GetAndReadAsync<LibraryLoanDto>($"{LoansRoute}/{issued.Loan.Id}")).SchoolDueDate.ShouldBe(issued.Loan.SchoolDueDate);
	}

	[Theory]
	[InlineData("")]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	public async Task Checkout_Should_Require_View_And_The_Exact_Issue_Action(string permissions)
	{
		var input = await CheckoutRequestAsync();
		using var limited = LimitedClient(permissions);
		var response = await limited.PostAsJsonAsync(LoansRoute, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("return")]
	[InlineData("renewal-preview")]
	[InlineData("renew")]
	[InlineData("dates/review")]
	public async Task Loan_View_Should_Not_Grant_Command_Authority(string action)
	{
		using var limited = LimitedClient(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View);
		var response = await limited.PostAsJsonAsync($"{LoansRoute}/{Guid.NewGuid()}/{action}", new
		{
			IdempotencyKey = "denied-action", Revision = "denied", Reason = "Denied access control test"
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Loan_Endpoints_Should_Require_Authentication()
	{
		using var anonymous = CreateAnonymousClient();
		var response = await anonymous.GetAsync($"{LoansRoute}/{Guid.NewGuid()}");
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
	}

	private HttpClient LimitedClient(string permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.IsNullOrEmpty(permissions) ? "__none__" : permissions);
		return client;
	}
}
