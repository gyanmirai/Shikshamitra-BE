using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalReceivableApi_Tests
{
	[Theory]
	[InlineData("waiver-apply-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[InlineData("waiver-approval-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	[InlineData("waiver-rejection-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[InlineData("waiver-reversal-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	public async Task Waiver_Context_Http_Should_Reject_Another_Waiver_Action(string route, string unrelatedAction)
	{
		using var denied = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View, unrelatedAction));
		var source = new FinanceExternalSourceReference
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = Guid.NewGuid(),
			SourceVersion = "exact owning-action boundary +/=%& "
		};
		using var response = await denied.GetAsync(BaseUrl + "/operator/" + route + SourceQuery(source));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("waiver-apply-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	[InlineData("waiver-approval-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[InlineData("waiver-rejection-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver)]
	[InlineData("waiver-reversal-context", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver)]
	public async Task Waiver_Context_Http_Should_Report_Source_Setup_Without_Unrelated_Collection_Or_Settings_Actions(string route, string action)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		using (var maker = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)))
		{
			using var posted = await maker.PostAsJsonAsync(BaseUrl, new PostFinanceExternalReceivableInput
			{
				Source = fixture.Source, FinanceChargeCategoryId = fixture.FinanceCategoryId,
				IdempotencyKey = Guid.NewGuid().ToString("N")
			}, JsonOptions);
			posted.StatusCode.ShouldBe(HttpStatusCode.OK, await posted.Content.ReadAsStringAsync());
		}
		using var reader = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View, action));
		using var response = await reader.GetAsync(BaseUrl + "/operator/" + route + SourceQuery(fixture.Source));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var context = await response.Content.ReadFromJsonAsync<FinanceExternalWaiverContextDto>(JsonOptions);
		context.ShouldNotBeNull();
		context.Source.SourceId.ShouldBe(fixture.Source.SourceId);
		context.Source.SourceVersion.ShouldBe(fixture.Source.SourceVersion);
		context.Receivable.Payer.StudentId.ShouldBe(fixture.StudentId);
		context.Receivable.Payer.EmployeeId.ShouldBeNull();
		context.Receivable.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		context.Receivable.OriginalAmount.ShouldBe(100.75m);
		context.Receivable.OutstandingAmount.ShouldBe(0m);
		context.CanExecute.ShouldBeFalse();
		context.Issues.ShouldNotBeEmpty("No settlement policy was bound and the posted assessment is not approved.");
		context.Requests.TotalCount.ShouldBe(0);
		context.Requests.Items.ShouldBeEmpty();
	}
}
