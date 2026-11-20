using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalReceivableApi_Tests
{
	[Theory]
	[InlineData("GET", "/refund-form")]
	[InlineData("POST", "/refunds/preview")]
	[InlineData("POST", "/refunds")]
	public async Task External_Refund_Http_Routes_Should_Enforce_Their_Exact_Finance_Action(string method, string suffix)
	{
		using var denied = ClientWithPermissions("__none__");
		var source = new FinanceExternalSourceReference
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = Guid.NewGuid(), SourceVersion = "permission-boundary-v1"
		};
		var input = new FinanceExternalRefundRequestInput
		{
			Source = source, Revision = new string('a', 64), OriginalReceiptId = Guid.NewGuid(),
			Method = "UPI", IdempotencyKey = Guid.NewGuid().ToString("N"), Reason = "Explicit source refund permission boundary.",
			Lines = [new() { SourceAllocationId = Guid.NewGuid(), SourceReceiptId = Guid.NewGuid(), Amount = 15.25m }]
		};

		using var response = method == "GET"
			? await denied.GetAsync(BaseUrl + suffix + SourceQuery(source))
			: await denied.PostAsJsonAsync(BaseUrl + suffix, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}
}
