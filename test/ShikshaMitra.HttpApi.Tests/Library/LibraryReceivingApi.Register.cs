using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	public async Task Receiving_Http_Register_Should_Require_Both_Read_Actions(string permissions)
	{
		using var limited = LimitedClient(permissions);
		using var response = await limited.GetAsync(ReceivingRoute);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Receiving_Http_Register_Should_Find_An_Actual_Copy_Receipt_Without_The_Add_Action()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await ReceivingInputAsync();
		var preview = await PostAndReadAsync<LibraryReceivingPreviewDto>(ReceivingRoute + "/preview", input);
		preview.CanReceive.ShouldBeTrue();
		var receipt = await PostAndReadAsync<LibraryStockReceiptDto>(ReceivingRoute, new LibraryReceiveCommand
			{ Receipt = input, Revision = preview.Revision!, IdempotencyKey = System.Guid.NewGuid().ToString("N") });
		using var reader = LimitedClient(string.Join(',', ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var copyId = receipt.Lines.Single().Copies.First().CopyId;

		using var response = await reader.GetAsync($"{ReceivingRoute}?copyId={copyId}&pageSize=1");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<LibraryStockReceiptSummaryDto>>(JsonOptions);
		result.ShouldNotBeNull(); result.TotalCount.ShouldBe(1); result.Items.Single().Id.ShouldBe(receipt.Id);
		result.Items.Single().CopyCount.ShouldBe(2); result.Items.Single().RecordedAt.ShouldBe(receipt.RecordedAt);
	}
}
