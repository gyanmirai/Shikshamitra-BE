using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.CampusTransfers;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public class LibrarySectionCampus_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Campus_Binding_Should_Preserve_Shelves_And_Require_Transfer_Between_Campuses()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = (await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider))).Catalog;
		var north = await CreateCampusAsync("NORTH");
		var south = await CreateCampusAsync("SOUTH");
		var annex = await CreateSectionAsync(catalog.YearId, "Annex");
		var branch = await CreateSectionAsync(catalog.YearId, "Branch");
		var sections = GetRequiredService<ILibrarySectionAppService>();
		(await sections.GetAsync(catalog.SectionId, catalog.YearId)).CampusId.ShouldBeNull();
		await sections.BindCampusAsync(catalog.SectionId, catalog.YearId, north);
		await sections.BindCampusAsync(catalog.SectionId, catalog.YearId, north);
		await sections.BindCampusAsync(annex.Section, catalog.YearId, north);
		await sections.BindCampusAsync(branch.Section, catalog.YearId, south);
		var bound = await sections.GetAsync(catalog.SectionId, catalog.YearId);
		bound.CampusId.ShouldBe(north);
		bound.ShelfDetails.ShouldContain(row => row.Id == catalog.ShelfId);
		await Should.ThrowAsync<BusinessException>(() => sections.BindCampusAsync(catalog.SectionId, catalog.YearId, south));

		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var options = await lifecycle.GetMoveOptionsAsync(catalog.BookId, catalog.CopyId, catalog.YearId);
		options.Sections.Select(row => row.Id).ShouldContain(annex.Section);
		options.Sections.Select(row => row.Id).ShouldNotContain(branch.Section);
		await Should.ThrowAsync<BusinessException>(() => lifecycle.MoveAsync(catalog.BookId, catalog.CopyId,
			new() { SectionId = branch.Section, ShelfId = branch.Shelf, Revision = options.State.Revision, Reason = "Cross-campus request" }));
		var moved = await lifecycle.MoveAsync(catalog.BookId, catalog.CopyId,
			new() { SectionId = annex.Section, ShelfId = annex.Shelf, Revision = options.State.Revision, Reason = "Shelve in same campus annex" });
		moved.SectionId.ShouldBe(annex.Section);
		(await lifecycle.GetEventsAsync(catalog.BookId, catalog.CopyId)).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Campus_Transfer_Should_Keep_Each_Copy_At_The_Source_Until_Its_Receipt()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = (await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider))).Catalog;
		var section = await CreateSectionAsync(catalog.YearId, "Transfer destination");
		var campusSettings = GetRequiredService<ILibrarySectionAppService>();
		await campusSettings.BindCampusAsync(catalog.SectionId, catalog.YearId, await CreateCampusAsync("SEND"));
		await campusSettings.BindCampusAsync(section.Section, catalog.YearId, await CreateCampusAsync("RECEIVE"));
		var spare = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(
			new BookCopy(Guid.NewGuid(), catalog.BookId, 2, "TRANSFER-002", catalog.SectionId, catalog.ShelfId,
				GetRequiredService<ICurrentTenant>().Id), true));
		var transfers = GetRequiredService<ILibraryCampusTransferAppService>();
		var day = new DateOnly(2026, 11, 17);
		var input = new LibraryCampusDispatchInput(catalog.SectionId, section.Section, section.Shelf,
			day, "Branch reading set", [catalog.CopyId, spare.Id]);
		(await Should.ThrowAsync<BusinessException>(() => transfers.PreviewDispatchAsync(input with { Reason = "branch\0set" })))
			.Code.ShouldBe(LibraryErrorCodes.CampusTransferInvalid);
		var preview = await transfers.PreviewDispatchAsync(input);
		preview.Errors.ShouldBeEmpty();
		var command = new LibraryCampusDispatchCommand(input, preview.Revision!, "campus-dispatch");
		var transfer = await transfers.DispatchAsync(command);
		(await transfers.DispatchAsync(command)).Id.ShouldBe(transfer.Id);
		(await transfers.GetAsync(transfer.Id)).PendingCount.ShouldBe(2);
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var enRoute = await lifecycle.GetStateAsync(catalog.BookId, catalog.CopyId);
		enRoute.Status.ShouldBe((int)BookCopyStatus.InTransit);
		enRoute.SectionId.ShouldBe(catalog.SectionId);
		enRoute.CanMove.ShouldBeFalse();
		enRoute.CanWriteOff.ShouldBeFalse();
		var counts = GetRequiredService<ILibraryStockVerificationAppService>();
		var countInput = new LibraryStockCountInput("Dispatch check", catalog.SectionId, catalog.ShelfId, day);
		var planned = await counts.PreviewAsync(countInput);
		var count = await counts.OpenAsync(new(countInput, planned.Revision, "campus-transfer-count"));
		(await counts.GetReconciliationAsync(count.Id)).Items.Select(row => row.Finding).Distinct()
			.ShouldHaveSingleItem().ShouldBe(LibraryStockFinding.InTransit);

		var receive = new LibraryCampusReceiveInput(day.AddDays(1), "One received",
			[new(catalog.CopyId, BookCopyStatus.Damaged)]);
		(await Should.ThrowAsync<BusinessException>(() => transfers.PreviewReceiveAsync(transfer.Id, receive with { Notes = "one\0received" })))
			.Code.ShouldBe(LibraryErrorCodes.CampusTransferInvalid);
		var receiptPlan = await transfers.PreviewReceiveAsync(transfer.Id, receive);
		receiptPlan.Errors.ShouldBeEmpty();
		var receiveCommand = new LibraryCampusReceiveCommand(receive, receiptPlan.Revision!, "campus-receipt");
		var receipt = await transfers.ReceiveAsync(transfer.Id, receiveCommand);
		(await transfers.ReceiveAsync(transfer.Id, receiveCommand)).Id.ShouldBe(receipt.Id);
		var arrived = await lifecycle.GetStateAsync(catalog.BookId, catalog.CopyId);
		arrived.SectionId.ShouldBe(section.Section);
		arrived.ShelfId.ShouldBe(section.Shelf);
		arrived.Status.ShouldBe((int)BookCopyStatus.Damaged);
		var pending = await transfers.GetAsync(transfer.Id);
		pending.PendingCount.ShouldBe(1);
		pending.Copies.Single(row => row.ReceiptId == null).CopyId.ShouldBe(spare.Id);
	}

	[Fact]
	public async Task Campus_Receipt_Should_Save_While_A_Closed_Calendar_Defers_Automatic_Pickup()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var catalog = context.Catalog;
		var destination = await CreateSectionAsync(catalog.YearId, "Pickup desk");
		var sections = GetRequiredService<ILibrarySectionAppService>();
		await sections.BindCampusAsync(catalog.SectionId, catalog.YearId, await CreateCampusAsync("ORIGIN"));
		await sections.BindCampusAsync(destination.Section, catalog.YearId, await CreateCampusAsync("BRANCH"));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "UTC" });
		var day = new DateOnly(2026, 11, 17);
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var policy = await policies.CreateAsync(catalog.YearId, new()
		{
			Name = "Automatic pickup", Role = 0, EffectiveFrom = new(2026, 11, 1),
			Terms = new()
			{
				MaxBooksAtTime = 4, ReservationLimit = 2, LoanDays = 7, MaxRenewals = 1, RenewalDays = 7,
				PickupWindowDays = 2, MoveDueDateToNextOpenDay = true
			}
		});
		await policies.PublishAsync(policy.Id, new() { ConcurrencyStamp = policy.ConcurrencyStamp! });
		var reservations = GetRequiredService<ILibraryReservationAppService>();
		var waiting = await reservations.CreateAsync(new()
		{
			BookId = catalog.BookId, AcademicYearId = catalog.YearId, MemberId = context.StudentId,
			RequestedDate = day, PickupSectionId = destination.Section
		});
		waiting.State.ShouldBe((int)LibraryReservationState.Waiting);

		var transfers = GetRequiredService<ILibraryCampusTransferAppService>();
		var dispatch = new LibraryCampusDispatchInput(catalog.SectionId, destination.Section, destination.Shelf,
			day, "Send a reserved title to the branch", [catalog.CopyId]);
		var dispatchPlan = await transfers.PreviewDispatchAsync(dispatch);
		var transfer = await transfers.DispatchAsync(new(dispatch, dispatchPlan.Revision!, "deferred-pickup-dispatch"));
		await SetOpenWeekdaysAsync(catalog.YearId, false);

		var receive = new LibraryCampusReceiveInput(day.AddDays(1), null, [new(catalog.CopyId, BookCopyStatus.Available)]);
		var receiptPlan = await transfers.PreviewReceiveAsync(transfer.Id, receive);
		receiptPlan.Errors.ShouldBeEmpty();
		var receipt = await transfers.ReceiveAsync(transfer.Id, new(receive, receiptPlan.Revision!, "deferred-pickup-receipt"));
		receipt.Copies.ShouldHaveSingleItem().CopyId.ShouldBe(catalog.CopyId);
		(await transfers.GetAsync(transfer.Id)).PendingCount.ShouldBe(0);
		var contents = GetRequiredService<IRepository<BookReservation, Guid>>();
		var deferred = await WithUnitOfWorkAsync(() => contents.GetAsync(waiting.Id));
		deferred.State.ShouldBe(LibraryReservationState.Waiting);

		await SetOpenWeekdaysAsync(catalog.YearId, true);
		await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>().RebalanceAsync(catalog.BookId, day.AddDays(1)));
		var allocated = await WithUnitOfWorkAsync(() => contents.GetAsync(waiting.Id));
		allocated.State.ShouldBe(LibraryReservationState.Ready);
		allocated.QueueSequence.ShouldBe(deferred.QueueSequence);
	}

	private Task SetOpenWeekdaysAsync(Guid year, bool open) => WithUnitOfWorkAsync(async () =>
	{
		var timings = GetRequiredService<IRepository<LibraryTiming, Guid>>();
		foreach (var timing in await timings.GetListAsync(row => row.AcademicYearId == year))
		{
			timing.Status = open && timing.Day == nameof(DayOfWeek.Monday) ? 0 : 1;
			await timings.UpdateAsync(timing);
		}
	});

	[Fact]
	public async Task Campus_Binding_Should_Exclude_Foreign_And_Inactive_Campuses()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = (await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider))).Catalog;
		var inactive = await CreateCampusAsync("RETIRED", active: false);
		Guid foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) foreign = await CreateCampusAsync("FOREIGN");
		var sections = GetRequiredService<ILibrarySectionAppService>();
		var options = await sections.GetCampusOptionsAsync(catalog.YearId);
		options.ShouldNotContain(row => row.Id == inactive || row.Id == foreign);
		await Should.ThrowAsync<BusinessException>(() => sections.BindCampusAsync(catalog.SectionId, catalog.YearId, foreign));
		await Should.ThrowAsync<BusinessException>(() => sections.BindCampusAsync(catalog.SectionId, catalog.YearId, inactive));
		(await sections.GetAsync(catalog.SectionId, catalog.YearId)).CampusId.ShouldBeNull();
	}

	private Task<Guid> CreateCampusAsync(string code, bool active = true) => WithUnitOfWorkAsync(async () =>
	{
		var campus = new AdmissionCampus(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, code, code);
		if (!active) campus.Deactivate();
		await GetRequiredService<IRepository<AdmissionCampus, Guid>>().InsertAsync(campus, true);
		return campus.Id;
	});

	private Task<(Guid Section, Guid Shelf)> CreateSectionAsync(Guid year, string name) => WithUnitOfWorkAsync(async () =>
	{
		var section = new LibrarySection(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, year, name, 1);
		await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(section, true);
		var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A1");
		await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(shelf, true);
		return (section.Id, shelf.Id);
	});
}
