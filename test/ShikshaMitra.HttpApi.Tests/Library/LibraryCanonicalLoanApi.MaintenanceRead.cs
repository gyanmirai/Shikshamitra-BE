using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Fact]
	public async Task Loan_Maintenance_Read_Http_Should_Add_Owning_Context_Without_Changing_The_Command_Wire_Shape()
	{
		var input = await CheckoutRequestAsync();
		var issuedResponse = await Client.PostAsJsonAsync(LoansRoute, input, JsonOptions);
		issuedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await issuedResponse.Content.ReadAsStringAsync());
		using var issued = JsonDocument.Parse(await issuedResponse.Content.ReadAsStringAsync());
		var accepted = issued.RootElement.GetProperty("loan");
		accepted.TryGetProperty("schoolContext", out _).ShouldBeFalse();
		accepted.TryGetProperty("canReviewDates", out _).ShouldBeFalse();
		var id = accepted.GetProperty("id").GetGuid();
		using var reader = LimitedClient($"{ShikshaMitraSubmodulePermissions.Library.IssueReturn.View},{ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew}");
		var response = await reader.GetAsync($"{LoansRoute}/{id}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		body.RootElement.GetProperty("id").GetGuid().ShouldBe(id);
		body.RootElement.GetProperty("revision").GetString().ShouldBe(accepted.GetProperty("revision").GetString());
		body.RootElement.GetProperty("canReturn").GetBoolean().ShouldBeFalse();
		body.RootElement.GetProperty("canReviewDates").GetBoolean().ShouldBeFalse();
		var context = body.RootElement.GetProperty("schoolContext");
		context.GetProperty("academicYears").EnumerateArray().Select(year => year.GetProperty("id").GetGuid())
			.ShouldContain(Factory.SeededAcademicYearId);
		var confirmed = context.GetProperty("isConfirmed").GetBoolean();
		context.GetProperty("requiresExplicitDate").GetBoolean().ShouldBe(!confirmed);
		if (confirmed)
		{
			context.GetProperty("schoolToday").ValueKind.ShouldBe(JsonValueKind.String);
			context.GetProperty("confirmedTimeZoneId").GetString().ShouldNotBeNullOrWhiteSpace();
		}
		else
		{
			(!context.TryGetProperty("schoolToday", out var day) || day.ValueKind == JsonValueKind.Null).ShouldBeTrue();
			(!context.TryGetProperty("confirmedTimeZoneId", out var zone) || zone.ValueKind == JsonValueKind.Null).ShouldBeTrue();
			context.GetProperty("unavailableReason").GetString().ShouldNotBeNullOrWhiteSpace();
		}
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.ReviewDates)]
	public async Task Loan_Maintenance_Read_Http_Should_Deny_Action_Only_Before_Returning_Context(string action)
	{
		using var reader = LimitedClient(action);
		var response = await reader.GetAsync($"{LoansRoute}/{System.Guid.NewGuid()}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		body.RootElement.TryGetProperty("schoolContext", out _).ShouldBeFalse();
		body.RootElement.TryGetProperty("canReviewDates", out _).ShouldBeFalse();
	}
}
