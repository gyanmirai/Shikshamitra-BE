using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalReceivableApi_Tests
{
	[Fact]
	public async Task Refund_Payout_Http_Should_Require_Process_Even_When_View_And_Approve_Are_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var denied = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve));

		using var response = await denied.PostAsJsonAsync($"/api/fee-refunds/{Guid.NewGuid():D}/payout", PayoutActionBoundaryInput(), JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Refund_Payout_Http_With_Process_Should_Reach_The_Missing_Request_Boundary()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var authorized = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process));

		using var response = await authorized.PostAsJsonAsync($"/api/fee-refunds/{Guid.NewGuid():D}/payout", PayoutActionBoundaryInput(), JsonOptions);

		// This proves the action gate is distinct from View/Approve, not payment success.
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
	}

	private static RecordRefundPayoutDto PayoutActionBoundaryInput() => new()
	{
		IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "UPI", Amount = 15m,
		PaymentDate = new DateTimeOffset(2026, 11, 18, 8, 30, 0, TimeSpan.Zero),
		ExpenseCategoryId = Guid.NewGuid(), BankAccountId = Guid.NewGuid(),
		ExternalReference = "PAYOUT-ACTION-BOUNDARY", EvidenceReference = "Permission boundary; no persisted payment source."
	};
}
