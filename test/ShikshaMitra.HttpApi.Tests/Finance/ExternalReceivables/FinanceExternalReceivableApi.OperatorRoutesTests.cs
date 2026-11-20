using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalReceivableApi_Tests
{
	[Theory]
	[InlineData("/posting-options", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	[InlineData("/binding-options", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	[InlineData("/binding-context", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	[InlineData("/approval-context", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	[InlineData("/history", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task Operator_Http_Routes_Should_Require_Their_Owning_Action_Instead_Of_An_Other_Finance_Action(
		string suffix, string otherAction)
	{
		using var denied = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View, otherAction));
		var source = new FinanceExternalSourceReference
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = Guid.NewGuid(),
			SourceVersion = "operator-permission-boundary-v1"
		};

		using var response = await denied.GetAsync(BaseUrl + "/operator" + suffix + SourceQuery(source) +
			$"&optionKind={(int)FinanceExternalOperatorOptionKind.ChargeCategory}&page=1&pageSize=20");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Operator_Posting_Options_Should_Return_The_Actual_Source_Category_Usable_By_The_Canonical_Post_Over_Http()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		using var cashier = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add));
		var search = Uri.EscapeDataString($"HTTP Library charge {fixture.StudentId:N}");
		using var optionsResponse = await cashier.GetAsync(BaseUrl + "/operator/posting-options" + SourceQuery(fixture.Source) +
			$"&optionKind={(int)FinanceExternalOperatorOptionKind.ChargeCategory}&page=1&pageSize=20&search={search}");
		optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());
		var choices = await optionsResponse.Content.ReadFromJsonAsync<PagedResultDto<FinanceExternalOperatorOptionDto>>(JsonOptions);
		choices.ShouldNotBeNull();
		choices.TotalCount.ShouldBe(1);
		choices.Items.Count.ShouldBe(1);
		var category = choices.Items[0];
		category.Id.ShouldBe(fixture.FinanceCategoryId);
		category.IsSelectable.ShouldBeTrue();
		category.PolicyId.ShouldBeNull();
		category.VersionNumber.ShouldBeNull();

		using var postResponse = await cashier.PostAsJsonAsync(BaseUrl, new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = category.Id, IdempotencyKey = Guid.NewGuid().ToString("N")
		}, JsonOptions);
		postResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await postResponse.Content.ReadAsStringAsync());
		var posted = await postResponse.Content.ReadFromJsonAsync<FinanceExternalReceivableDto>(JsonOptions);
		posted.ShouldNotBeNull();
		posted.Source.SourceId.ShouldBe(fixture.Source.SourceId);
		posted.Source.SourceVersion.ShouldBe(fixture.Source.SourceVersion);
		posted.Payer.StudentId.ShouldBe(fixture.StudentId);
		posted.Payer.EmployeeId.ShouldBeNull();
		posted.OriginalAmount.ShouldBe(100.75m);
		posted.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		posted.OutstandingAmount.ShouldBe(0m);
	}
}
