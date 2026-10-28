using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Mobile;

public class MobileFinancePermissionContract_Tests
{
	[Theory]
	[InlineData("ContextAsync", "Finance.Fees.FeeCollections.View")]
	[InlineData("OverviewAsync", "Finance.Fees.FeeCollections.View")]
	[InlineData("InstallmentsAsync", "Finance.Fees.FeeCollections.View")]
	[InlineData("PaymentsAsync", "Finance.Fees.FeeCollections.View")]
	[InlineData("StatementPdfAsync", "Finance.Fees.FeeCollections.View")]
	[InlineData("ChargesAsync", "Finance.Fees.Charges.View")]
	[InlineData("ChargesPageAsync", "Finance.Fees.Charges.View")]
	[InlineData("ChargeAsync", "Finance.Fees.Charges.View")]
	[InlineData("ChargePaymentsAsync", "Finance.Fees.Charges.View")]
	[InlineData("ReceiptsAsync", "Finance.Fees.InvoiceReceipt.View")]
	[InlineData("ReceiptPdfAsync", "Finance.Fees.InvoiceReceipt.View")]
	public void Read_operation_uses_the_existing_web_action(string method, string permission) =>
		typeof(MobileFinanceReadService).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
			.Select(x => x.Policy).ShouldContain(permission);

	[Fact]
	public void Mobile_mapping_contains_only_existing_finance_action_names()
	{
		MobileFinancePermissions.All.ShouldAllBe(x => x.StartsWith("Finance.Fees.", StringComparison.Ordinal));
		MobileFinancePermissions.IsSupported("Finance.SelfService.View").ShouldBeFalse();
		MobileFinancePermissions.RequiresGuardian(MobileFinancePermissions.RequestRefund).ShouldBeTrue();
		MobileFinancePermissions.RequiresGuardian(MobileFinancePermissions.View).ShouldBeFalse();
	}
}
