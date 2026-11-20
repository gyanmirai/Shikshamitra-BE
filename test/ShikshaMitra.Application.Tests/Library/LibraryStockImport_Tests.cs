using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStockImport_Tests : ShikshaMitraApplicationTestBase<LibraryStockVerificationTestModule>
{
	private ILibraryStockImportAppService App => GetRequiredService<ILibraryStockImportAppService>();

	[Fact]
	public async Task Commits_An_Exact_Mixed_Batch_And_Replays_It_Without_Duplicating_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add).Returns(true);
		var input = Input(seed, "SourceRowId,BookId,TitleKey,Title,Author,Accession,CataloguePrice,UnitCost,Currency,LanguageCode\n" +
			$"00001,{seed.BookId},,,,000011,,,,\n" +
			"00002,,hindi,\"कहानियाँ, भाग १\",विद्यालय,000012,0,12.25,INR,hi\n" +
			"00003,,hindi,\"कहानियाँ, भाग १\",विद्यालय,000013,0,12.25,INR,hi\n" +
			"00004,,anonymous,Donated folktales,School,000014,,,,en\n");
		var preview = await App.PreviewAsync(input);
		preview.Errors.ShouldBeEmpty();
		preview.Rows.Count.ShouldBe(4);
		preview.NewTitleCount.ShouldBe(2);
		var command = new LibraryCommitStockImportCommand(input, preview.Revision!, "term-one-import");
		var accepted = await App.CommitAsync(command);
		accepted.Rows.Select(row => row.SourceRowId).ShouldBe(["00001", "00002", "00003", "00004"]);
		accepted.Rows.Select(row => row.AccessionNumber).ShouldBe(["000011", "000012", "000013", "000014"]);
		accepted.Rows[0].BookId.ShouldBe(seed.BookId);
		accepted.Rows[1].BookId.ShouldBe(accepted.Rows[2].BookId);
		var receipts = accepted.Rows.Select(row => row.ReceiptId).Distinct().ToArray();
		receipts.ShouldHaveSingleItem();
		var receipt = await GetRequiredService<ILibraryReceivingAppService>().GetAsync(receipts[0]);
		receipt.Lines.Single(line => line.BookId == accepted.Rows[1].BookId).UnitCost.ShouldBe("12.25");
		(await App.GetAsync(accepted.Id)).Rows.Select(row => row.CopyId).ShouldBe(accepted.Rows.Select(row => row.CopyId));
		(await App.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(accepted.Id);
		(await App.CommitAsync(command)).Id.ShouldBe(accepted.Id);
		(await App.PreviewAsync(input)).Errors.Count(error => error.Field == "SourceRowId").ShouldBe(4);
		await Should.ThrowAsync<BusinessException>(() => App.CommitAsync(command with { IdempotencyKey = "new-key" }));
		await WithUnitOfWorkAsync(async () =>
		{
			var books = GetRequiredService<IRepository<Book, Guid>>();
			(await books.GetAsync(accepted.Rows[1].BookId)).Price.ShouldBe(0);
			(await books.GetAsync(accepted.Rows[3].BookId)).Price.ShouldBeNull();
		});

		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		await WithUnitOfWorkAsync(async () =>
		{
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.TenantId == tenant && row.UserId == actor, true);
			await scopes.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: seed.YearId,
				description: "Allowed current year only"), true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		(await App.GetListAsync()).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<EntityNotFoundException>(() => App.GetAsync(accepted.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.CommitAsync(command));
	}

	[Fact]
	public async Task Reports_Duplicate_And_Invalid_Rows_And_Requires_Catalogue_Add_For_New_Titles()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var stock = Input(seed, "Legacy ID,BookId,Accession,ReceivedOn\n" +
			$"X01,{seed.BookId},x0001,\n" + $"X02,{seed.BookId},X0001,2025-02-30\n") with
		{
			ColumnMap = new() { ["SourceRowId"] = "Legacy ID" }
		};
		var preview = await App.PreviewAsync(stock);
		preview.Revision.ShouldBeNull();
		preview.Errors.ShouldContain(error => error.RowNumber == 3 && error.Field == "ReceivedOn");
		preview.Errors.ShouldContain(error => error.RowNumber == 3 && error.Field == "Accession");
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.PreviewAsync(Input(seed,
			"SourceRowId,TitleKey,Title,Author\nX01,new,New title,Author\n")));
		(await App.GetListAsync()).TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Reports_The_Physical_Csv_Line_After_Blanks_And_Multiline_Fields()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = Input(seed, "SourceRowId,BookId,Ignored,ReceivedOn\r\n\r\n" +
			$"00001,{seed.BookId},\"line one\r\nline two\",\r\n\r\n" +
			$"00002,{seed.BookId},,0001-01-01\r\n") with { Source = null };
		var preview = await App.PreviewAsync(input);
		preview.Revision.ShouldBeNull();
		preview.Rows.Select(row => row.RowNumber).ShouldBe([3, 6]);
		preview.Errors.ShouldContain(error => error.RowNumber == 6 && error.Field == "ReceivedOn");
		preview.Errors.ShouldContain(error => error.RowNumber == null && error.Field == "Receipt");
	}

	private static LibraryStockImportInput Input(LibraryPolicyTestContext seed, string csv) =>
		new(csv, seed.OriginYearId, seed.SectionId, seed.ShelfId, seed.CategoryId,
			new DateOnly(2025, 10, 2), LibraryStockSource.Donation, "School book drive");
}
