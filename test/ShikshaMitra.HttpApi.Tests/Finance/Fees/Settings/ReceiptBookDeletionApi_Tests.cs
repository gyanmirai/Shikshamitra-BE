using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

[Collection(FinanceReceiptApiTestCollection.Name)]
public class ReceiptBookDeletionApi_Tests(FinanceReceiptApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Unused_book_can_be_deleted_and_missing_book_is_reported()
	{
		var id = await SeedBookAsync(false);
		var route = $"/api/receipt-books/{id}";

		(await Client.DeleteAsync(route)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await Client.DeleteAsync(route)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.DeleteAsync($"/api/receipt-books/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Referenced_book_remains_available_and_reports_the_dependency()
	{
		var id = await SeedBookAsync(true);
		var route = $"/api/receipt-books/{id}";

		var response = await Client.DeleteAsync(route);
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		var problem = await response.Content.ReadAsStringAsync();
		problem.ShouldContain(ReceiptBookErrorCodes.CannotDeleteInUse);
		problem.ShouldContain("fee structure");
		(await Client.DeleteAsync(route)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	private async Task<Guid> SeedBookAsync(bool referenced)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var book = new ReceiptBook(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
			Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
			"Book deletion test", "BOOK", string.Empty, 1, 100, string.Empty);
		await scope.ServiceProvider.GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(book, autoSave: true);
		if (referenced)
			await scope.ServiceProvider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(
				new FeeStructureMetadata(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
					Factory.SeededBoardId, Factory.SeededMediumId, book.Id, "Receipt book dependency"));
		await uow.CompleteAsync();
		return book.Id;
	}
}
