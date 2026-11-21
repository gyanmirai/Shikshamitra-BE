using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	private const string ReceivingRoute = "/api/library/receiving";

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View + "," + ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View + "," + ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View + "," + ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public async Task Receiving_Http_Preview_Should_Require_All_Of_Its_Exact_Actions(string permissions)
	{
		using var limited = LimitedClient(permissions);
		using var response = await limited.PostAsJsonAsync(ReceivingRoute + "/preview", new LibraryReceivingInput(), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Receiving_Http_Detail_Should_Require_Authentication()
	{
		using var anonymous = CreateAnonymousClient();
		using var response = await anonymous.GetAsync($"{ReceivingRoute}/{Guid.NewGuid()}");
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Receiving_Http_Preview_Should_Preserve_Its_Full_Supported_Cost_As_Exact_Decimal_Strings()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await ReceivingInputAsync();
		input.Source = LibraryStockSource.Purchase;
		input.Lines[0].UnitCost = "9999999999999998.99";
		input.Lines[0].Currency = "INR";
		using var response = await Client.PostAsJsonAsync(ReceivingRoute + "/preview", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("canReceive").GetBoolean().ShouldBeTrue();
		var cost = json.RootElement.GetProperty("lines")[0].GetProperty("unitCost");
		cost.ValueKind.ShouldBe(JsonValueKind.String);
		cost.GetString().ShouldBe("9999999999999998.99");
		json.RootElement.GetProperty("knownTotals")[0].GetProperty("amount").GetString().ShouldBe("19999999999999997.98");
	}

	[Fact]
	public async Task Receiving_Http_Should_Save_And_Replay_Only_Its_Actual_New_Copies_Without_Inventing_A_Cost()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await ReceivingInputAsync();
		var preview = await PostAndReadAsync<LibraryReceivingPreviewDto>(ReceivingRoute + "/preview", input);
		preview.CanReceive.ShouldBeTrue();
		var command = new LibraryReceiveCommand { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var first = await PostAndReadAsync<LibraryStockReceiptDto>(ReceivingRoute, command);
		var replay = await PostAndReadAsync<LibraryStockReceiptDto>(ReceivingRoute, command);
		var detail = await GetAndReadAsync<LibraryStockReceiptDto>($"{ReceivingRoute}/{first.Id}");
		replay.Id.ShouldBe(first.Id); detail.Id.ShouldBe(first.Id); detail.CopyCount.ShouldBe(2);
		detail.Lines.Single().UnitCost.ShouldBeNull(); detail.Lines.Single().Currency.ShouldBeNull();
		detail.Lines.Single().Copies.Select(copy => copy.CopyId).ShouldBe(first.Lines.Single().Copies.Select(copy => copy.CopyId));
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		(await services.GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(copy => copy.BookId == input.Lines[0].BookId)).ShouldBe(3);
		(await services.GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync(row => row.ReceiptId == first.Id)).ShouldBe(1);
		await unit.CompleteAsync();
	}

	private async Task<LibraryReceivingInput> ReceivingInputAsync()
	{
		var (copyId, _) = await SeedLoanContextAsync();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var copy = await services.GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(copyId);
		var input = new LibraryReceivingInput
		{
			ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation, SourceName = "Community donor",
			Lines = [new() { BookId = copy.BookId, SectionId = copy.SectionId, ShelfId = copy.ShelfId, Quantity = 2 }]
		};
		await unit.CompleteAsync();
		return input;
	}
}
