using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationFifo_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly DateOnly SchoolDate = new(2026, 11, 19);

	[Fact]
	public async Task A_Pickup_Reservation_Should_Wait_At_Its_Site_Without_Blocking_An_Unrelated_Campus()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(withSpareCopy: false);
		var catalog = context.School.Catalog;
		var location = await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var campus = await GetRequiredService<IRepository<AdmissionCampus, Guid>>().InsertAsync(
				new AdmissionCampus(Guid.NewGuid(), tenant, "BRANCH", "Branch Campus"), true);
			var section = await GetRequiredService<IRepository<LibrarySection, Guid>>().InsertAsync(
				new LibrarySection(Guid.NewGuid(), tenant, catalog.YearId, "Pickup Desk", 1), true);
			await GetRequiredService<IRepository<LibraryShelf, Guid>>().InsertAsync(new LibraryShelf(Guid.NewGuid(), section.Id, "A1"), true);
			return (campus.Id, SectionId: section.Id);
		});
		await GetRequiredService<ILibrarySectionAppService>().BindCampusAsync(location.SectionId, catalog.YearId, location.Id);
		var request = new LibraryReservationCreateRequest
		{
			BookId = catalog.BookId, AcademicYearId = catalog.YearId, MemberId = context.School.StudentId,
			RequestedDate = SchoolDate, RequestExpiryDate = SchoolDate.AddDays(14), PickupSectionId = location.SectionId
		};
		var reservations = GetRequiredService<ILibraryReservationAppService>();
		(await reservations.GetPickupLocationsAsync(catalog.YearId)).ShouldContain(choice => choice.SectionId == location.SectionId);
		var invalid = await reservations.PreviewAsync(new()
		{
			BookId = catalog.BookId, AcademicYearId = catalog.YearId, MemberId = context.School.StudentId,
			RequestedDate = SchoolDate, RequestExpiryDate = request.RequestExpiryDate, PickupSectionId = Guid.NewGuid()
		});
		invalid.CanCreate.ShouldBeFalse();
		(await reservations.PreviewAsync(request)).CanCreate.ShouldBeTrue();
		var waiting = await reservations.CreateAsync(request);
		waiting.State.ShouldBe((int)LibraryReservationState.Waiting);
		waiting.Hold.ShouldBeNull();
		waiting.PickupSectionId.ShouldBe(location.SectionId);
		waiting.PickupLabel.ShouldBe("Branch Campus / Pickup Desk");
		var campusWithoutHold = await ReserveAsync(context, context.OtherStudentId, SchoolDate);
		campusWithoutHold.State.ShouldBe((int)LibraryReservationState.Ready);
		campusWithoutHold.Hold!.CopyId.ShouldBe(catalog.CopyId);
	}

	[Fact]
	public async Task Later_Requester_Should_Not_Check_Out_The_Only_Copy_Ahead_Of_The_Queue_Head()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(withSpareCopy: false);
		var head = await ReserveAsync(context, context.School.StudentId, SchoolDate.AddDays(-1));
		var later = await ReserveAsync(context, context.OtherStudentId, SchoolDate);
		head.State.ShouldBe((int)LibraryReservationState.Ready);
		head.Hold!.CopyId.ShouldBe(context.School.Catalog.CopyId);
		later.State.ShouldBe((int)LibraryReservationState.Waiting);
		var checkout = await CheckoutRequestAsync(context, context.School.Catalog.CopyId);

		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(checkout));
		error.Code.ShouldBe(LibraryErrorCodes.CopyNotAvailable);

		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookIssue, Guid>>().AnyAsync(row => row.MemberId == context.OtherStudentId)).ShouldBeFalse();
			var reservations = GetRequiredService<IRepository<BookReservation, Guid>>();
			(await reservations.AnyAsync(row => row.Id == head.Id)).ShouldBeTrue();
			(await reservations.AnyAsync(row => row.Id == later.Id)).ShouldBeTrue();
		});
	}

	[Fact]
	public async Task A_Walk_In_Should_Be_Able_To_Check_Out_A_Spare_Copy_After_The_Head_Has_An_Allocated_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(withSpareCopy: true);
		var head = await ReserveAsync(context, context.School.StudentId, SchoolDate);
		head.State.ShouldBe((int)LibraryReservationState.Ready);
		head.Hold!.CopyId.ShouldBe(context.School.Catalog.CopyId);
		var checkout = await CheckoutRequestAsync(context, context.SpareCopyId!.Value);

		var issued = await GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(checkout);

		await WithUnitOfWorkAsync(async () =>
		{
			var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(issued.Loan.Id);
			loan.BookCopyId.ShouldBe(context.SpareCopyId!.Value);
			loan.MemberId.ShouldBe(context.OtherStudentId);
			(await GetRequiredService<IRepository<BookReservation, Guid>>().AnyAsync(row => row.Id == head.Id)).ShouldBeTrue();
		});
	}

	private Task<LibraryReservationDto> ReserveAsync(FifoContext context, Guid memberId, DateOnly requestedDate) =>
		GetRequiredService<ILibraryReservationAppService>().CreateAsync(new()
		{
			BookId = context.School.Catalog.BookId, AcademicYearId = context.School.Catalog.YearId,
			MemberId = memberId, RequestedDate = requestedDate, RequestExpiryDate = SchoolDate.AddDays(14)
		});

	private async Task<LibraryCheckoutRequest> CheckoutRequestAsync(FifoContext context, Guid copyId)
	{
		var request = new LibraryCheckoutRequest
		{
			CopyId = copyId, MemberId = context.OtherStudentId, AcademicYearId = context.School.Catalog.YearId,
			IssueDate = SchoolDate, RequestedDueDate = SchoolDate.AddDays(7), IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = request.CopyId, MemberId = request.MemberId, AcademicYearId = request.AcademicYearId,
			IssueDate = request.IssueDate, RequestedDueDate = request.RequestedDueDate
		});
		plan.CanIssue.ShouldBe(copyId == context.SpareCopyId);
		if (!plan.CanIssue) plan.BlockedReasons.ShouldContain(reason => reason.Contains("allocated", StringComparison.OrdinalIgnoreCase));
		request.Revision = plan.Revision;
		return request;
	}

	private Task<FifoContext> SeedAsync(bool withSpareCopy) => WithUnitOfWorkAsync(async () =>
	{
		var school = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Second", null, "Borrower",
			school.Catalog.YearId, school.BoardId, school.MediumId, school.StageId, school.BorrowerClassId, null,
			new DateTimeOffset(2014, 1, 2, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "8888888888");
		student.AdditionalDetail!.IsLibraryMember = true;
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var first = await copies.GetAsync(school.Catalog.CopyId);
		BookCopy? spare = null;
		if (withSpareCopy)
		{
			spare = new BookCopy(Guid.NewGuid(), school.Catalog.BookId, 2, $"FIFO-{Guid.NewGuid():N}",
				school.Catalog.SectionId, school.Catalog.ShelfId, tenantId);
			await copies.InsertAsync(spare, true);
		}
		return new FifoContext(school, student.Id, first.AccessionNumber, spare?.Id, spare?.AccessionNumber);
	});

	private sealed record FifoContext(LibraryCopyLifecycleTestContext School, Guid OtherStudentId,
		string FirstAccession, Guid? SpareCopyId, string? SpareAccession);
}
