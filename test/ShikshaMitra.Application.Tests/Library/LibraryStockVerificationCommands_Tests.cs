using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStockVerificationCommands_Tests : ShikshaMitraApplicationTestBase<LibraryStockVerificationTestModule>
{
	private ILibraryStockVerificationAppService App => GetRequiredService<ILibraryStockVerificationAppService>();

	[Fact]
	public async Task Stock_Count_Should_Freeze_And_Replay_The_Reviewed_Baseline()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		preview.ExpectedCopyCount.ShouldBe(1);
		var command = new LibraryOpenStockCountCommand(input, preview.Revision, "start-1");
		var session = await App.OpenAsync(command);
		session.AcademicYearId.ShouldBe(seed.OriginYearId);
		await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(seed.CopyId);
			copy.Status = BookCopyStatus.Issued;
			await copies.UpdateAsync(copy, true);
		});
		(await App.OpenAsync(command)).Id.ShouldBe(session.Id);
		(await App.GetBaselineAsync(session.Id, 1, 10)).Items.Single().CopyStatus.ShouldBe(BookCopyStatus.Available);
		(await Should.ThrowAsync<BusinessException>(() => App.OpenAsync(command with { IdempotencyKey = "start-2" })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		(await Should.ThrowAsync<BusinessException>(() => App.OpenAsync(command with { Count = input with { Name = "Different count" } })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationCommandConflict);
	}

	[Fact]
	public async Task Scan_Should_Replay_An_Accepted_Observation_And_Keep_Unknown_Codes()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "start-1"));
		var code = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(seed.CopyId)).AccessionNumber);
		var command = new LibraryRecordStockObservationCommand(code, seed.SectionId, seed.ShelfId, input.SchoolDate, "scan-1");
		var first = await App.RecordObservationAsync(session.Id, command);
		first.CopyId.ShouldBe(seed.CopyId);
		(await App.RecordObservationAsync(session.Id, command)).Id.ShouldBe(first.Id);
		var unknown = await App.RecordObservationAsync(session.Id, command with { Scan = "000UNKNOWN", IdempotencyKey = "scan-2" });
		unknown.CopyId.ShouldBeNull();
		unknown.AccessionNumber.ShouldBe("000UNKNOWN");
		unknown.Sequence.ShouldBe(2);
		(await App.GetAsync(session.Id)).ObservationCount.ShouldBe(2);
		(await Should.ThrowAsync<BusinessException>(() => App.RecordObservationAsync(session.Id, command with { Scan = "another" })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationCommandConflict);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Update).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.RecordObservationAsync(session.Id, command));
	}

	[Fact]
	public async Task Stock_Count_Should_Use_The_Recorded_Section_Year_For_Scoped_Reads()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "start-1"));
		await LimitToYearAsync(seed.YearId);
		(await App.GetOptionsAsync()).CanCountWholeSchool.ShouldBeFalse();
		(await App.GetListAsync()).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetBaselineAsync(session.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.PreviewAsync(input with { SectionId = null, ShelfId = null }));
		await LimitToYearAsync(seed.OriginYearId);
		(await App.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(session.Id);
	}

	private async Task LimitToYearAsync(Guid year)
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var user = GetRequiredService<ICurrentUser>().Id!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.TenantId == tenant && row.UserId == user, true);
			await scopes.InsertAsync(new(Guid.NewGuid(), tenant, userId: user, academicYearId: year, description: "Stock verification scope"), true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(user);
	}

	[Fact]
	public async Task Reconciliation_Should_Distinguish_Observed_Expected_Unexpected_And_Unknown_Copies()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var loan = await AddCopyAsync(seed, 2, "COUNT-LOAN", BookCopyStatus.Issued);
		var legacy = await AddCopyAsync(seed, 3, "COUNT-LEGACY", BookCopyStatus.Issued);
		var repair = await AddCopyAsync(seed, 4, "COUNT-REPAIR", BookCopyStatus.UnderRepair);
		var unseen = await AddCopyAsync(seed, 5, "COUNT-NOT-SEEN", BookCopyStatus.Available);
		var shelf = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryShelf, Guid>>()
			.InsertAsync(new(Guid.NewGuid(), seed.SectionId, "B2"), true));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "reconciliation"));
		var extra = await AddCopyAsync(seed, 6, "COUNT-LATE", BookCopyStatus.Available);
		await WithUnitOfWorkAsync(async () =>
		{
			var loans = GetRequiredService<IRepository<BookIssue, Guid>>();
			var day = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
			await loans.InsertAsync(new(Guid.NewGuid(), loan.Id, loan.AccessionNumber, Guid.NewGuid(), 0, day, day.AddDays(7), loan.TenantId));
			await loans.InsertAsync(new(Guid.NewGuid(), unseen.Id, legacy.AccessionNumber, Guid.NewGuid(), 0, day, day.AddDays(7), legacy.TenantId));
		});
		var code = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(seed.CopyId)).AccessionNumber);
		await App.RecordObservationAsync(session.Id, new(code, seed.SectionId, seed.ShelfId, input.SchoolDate, "intended-shelf"));
		var latest = await App.RecordObservationAsync(session.Id, new(code, seed.SectionId, shelf.Id, input.SchoolDate, "other-shelf"));
		await App.RecordObservationAsync(session.Id, new(extra.AccessionNumber, seed.SectionId, seed.ShelfId, input.SchoolDate, "after-opening"));
		await App.RecordObservationAsync(session.Id, new("000UNKNOWN", null, null, input.SchoolDate, "unknown"));

		var expected = await App.GetReconciliationAsync(session.Id);
		expected.TotalCount.ShouldBe(5);
		var findings = expected.Items.ToDictionary(row => row.Id);
		findings[loan.Id].Finding.ShouldBe(LibraryStockFinding.OnLoan);
		findings[legacy.Id].Finding.ShouldBe(LibraryStockFinding.RegisterConflict);
		findings[repair.Id].Finding.ShouldBe(LibraryStockFinding.InRepair);
		findings[unseen.Id].Finding.ShouldBe(LibraryStockFinding.RegisterConflict);
		findings[seed.CopyId].Finding.ShouldBe(LibraryStockFinding.ObservedElsewhere);
		findings[seed.CopyId].ObservationSequence.ShouldBe(latest.Sequence);
		findings[seed.CopyId].ObservedShelfId.ShouldBe(shelf.Id);
		(await App.GetReconciliationAsync(session.Id, page: 2, pageSize: 2)).Items.Count.ShouldBe(2);
		(await App.GetReconciliationAsync(session.Id, LibraryStockReconciliationSource.OutsideBaseline))
			.Items.ShouldHaveSingleItem().Finding.ShouldBe(LibraryStockFinding.OutsideBaseline);
		(await App.GetReconciliationAsync(session.Id, LibraryStockReconciliationSource.UnknownScans))
			.Items.ShouldHaveSingleItem().AccessionNumber.ShouldBe("000UNKNOWN");
		(await App.GetObservationsAsync(session.Id)).TotalCount.ShouldBe(4);
	}

	[Fact]
	public async Task Reconciliation_Should_Keep_Count_Evidence_Without_Exposing_A_Moved_Copy_Outside_Current_Scope()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "scope-review"));
		var before = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		before.Finding.ShouldBe(LibraryStockFinding.NotSeen);
		await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var section = await GetRequiredService<IRepository<LibrarySection, Guid>>()
				.InsertAsync(new(Guid.NewGuid(), tenant, seed.YearId, "Other year library", 2), true);
			var shelf = await GetRequiredService<IRepository<LibraryShelf, Guid>>()
				.InsertAsync(new(Guid.NewGuid(), section.Id, "C1"), true);
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(seed.CopyId);
			copy.SectionId = section.Id;
			copy.ShelfId = shelf.Id;
			await copies.UpdateAsync(copy, true);
		});
		(await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem().Finding.ShouldBe(LibraryStockFinding.MovedOutsideScope);
		await LimitToYearAsync(seed.OriginYearId);
		var scoped = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		scoped.Finding.ShouldBe(LibraryStockFinding.CurrentCopyUnavailable);
		scoped.CurrentStatus.ShouldBeNull();
		await LimitToYearAsync(seed.YearId);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetReconciliationAsync(session.Id));
	}

	private Task<BookCopy> AddCopyAsync(LibraryPolicyTestContext seed, int copyNumber, string accession, BookCopyStatus status) =>
		WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
			new BookCopy(Guid.NewGuid(), seed.BookId, copyNumber, accession, seed.SectionId,
				seed.ShelfId, GetRequiredService<ICurrentTenant>().Id) { Status = status }, true));

	[Fact]
	public async Task Stock_Count_Should_Check_Owning_Action_And_School_On_Replay_And_Read()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var command = new LibraryOpenStockCountCommand(input, preview.Revision, "start-1");
		var session = await App.OpenAsync(command);
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Add).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.OpenAsync(command));
		(await App.GetAsync(session.Id)).Id.ShouldBe(session.Id);
		using var otherSchool = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetAsync(session.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetReconciliationAsync(session.Id));
	}

	private static LibraryStockCountInput CreateInput(LibraryPolicyTestContext seed) =>
		new("Shelf verification", seed.SectionId, seed.ShelfId, new DateOnly(2026, 11, 19));
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryStockVerificationTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(false);
		permissions.IsGrantedAsync("Library.InventoryVerification.View").Returns(true);
		permissions.IsGrantedAsync("Library.InventoryVerification.Add").Returns(true);
		permissions.IsGrantedAsync("Library.InventoryVerification.Update").Returns(true);
		permissions.IsGrantedAsync("Library.InventoryVerification.Approve").Returns(true);
		permissions.IsGrantedAsync("Library.Receiving.View").Returns(true);
		permissions.IsGrantedAsync("Library.Receiving.Add").Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View).Returns(true);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}
