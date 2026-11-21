using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Theory]
	[InlineData(true, LibraryErrorCodes.ReceivingCommandConflict)]
	[InlineData(false, LibraryErrorCodes.ReceivingPlanChanged)]
	public async Task Receiving_Http_Should_Return_Conflict_For_A_Changed_Command_Or_Stale_Stock_Preview(bool reuseKey, string code)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await ReceivingInputAsync();
		var preview = await PostAndReadAsync<LibraryReceivingPreviewDto>(ReceivingRoute + "/preview", input);
		preview.CanReceive.ShouldBeTrue();
		var command = new LibraryReceiveCommand { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var accepted = await PostAndReadAsync<LibraryStockReceiptDto>(ReceivingRoute, command);
		accepted.CopyCount.ShouldBe(2);
		if (reuseKey) command.Receipt.Lines[0].Quantity = 3;
		else command.IdempotencyKey = Guid.NewGuid().ToString("N");

		using var response = await Client.PostAsJsonAsync(ReceivingRoute, command, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe(code);
	}
}
