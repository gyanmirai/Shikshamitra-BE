using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IBookAppService _bookAppService;
	private readonly IBookIssueAppService _bookIssueAppService;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<BookCategory, Guid> _bookCategoryRepository;
	private readonly IRepository<LibrarySection, Guid> _sectionRepository;
	private readonly IRepository<LibraryShelf, Guid> _shelfRepository;
	private readonly IRepository<LibraryChargeCategory, Guid> _chargeCategoryRepository;
	private readonly IRepository<LibraryMember, Guid> _libraryMemberRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<BookCopy, Guid> _bookCopyRepository;
	private readonly IRepository<BookIssue, Guid> _bookIssueRepository;
	private readonly IRepository<BookReservation, Guid> _bookReservationRepository;
	private readonly IRepository<LibraryCharge, Guid> _libraryChargeRepository;

	public LibraryCoreWorkflow_Tests()
	{
		_bookAppService = GetRequiredService<IBookAppService>();
		_bookIssueAppService = GetRequiredService<IBookIssueAppService>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_educationBoardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_educationMediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_educationMediumStageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_bookCategoryRepository = GetRequiredService<IRepository<BookCategory, Guid>>();
		_sectionRepository = GetRequiredService<IRepository<LibrarySection, Guid>>();
		_shelfRepository = GetRequiredService<IRepository<LibraryShelf, Guid>>();
		_chargeCategoryRepository = GetRequiredService<IRepository<LibraryChargeCategory, Guid>>();
		_libraryMemberRepository = GetRequiredService<IRepository<LibraryMember, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_bookCopyRepository = GetRequiredService<IRepository<BookCopy, Guid>>();
		_bookIssueRepository = GetRequiredService<IRepository<BookIssue, Guid>>();
		_bookReservationRepository = GetRequiredService<IRepository<BookReservation, Guid>>();
		_libraryChargeRepository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Duplicate_Accession_Numbers()
	{
		var context = await GetLibraryContextAsync();
		var accession = UniqueAccession("DUP");

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_bookAppService.CreateAsync(
				context.AcademicYearId,
				BuildBookRequest(context, accession, accession)));

		exception.Code.ShouldBe(LibraryErrorCodes.DuplicateAccessionNumber);
	}

	[Fact]
	public async Task IssueAsync_Should_Reject_Inactive_Student_Member()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("INACTIVE"))).Single();
		await DeactivateLibraryMemberAsync(context.ActiveStudentLibraryMemberId);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24)));

		exception.Code.ShouldBe(LibraryErrorCodes.MemberInactive);
	}

	[Fact]
	public async Task IssueAsync_Should_Reject_When_Borrowing_Limit_Is_Reached()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var accessions = new[]
		{
			UniqueAccession("LIMIT-A"),
			UniqueAccession("LIMIT-B"),
			UniqueAccession("LIMIT-C"),
			UniqueAccession("LIMIT-D")
		};
		var copies = await CreateCopiesAsync(context, accessions);
		await SeedActiveIssuesAsync(context.ActiveStudentId, copies.Take(3));

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			IssueCanonicalAsync(copies[3].Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24)));

		exception.Code.ShouldBe(LibraryErrorCodes.BorrowingLimitExceeded);
	}

	[Fact]
	public async Task ReturnAsync_Should_Create_Charge_And_Set_Copy_Status()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("RETURN"))).Single();

		var issueResult = await IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 14), new(2026, 11, 16));
		var issueId = Guid.Parse(issueResult.Id);

		var returnResult = await ReturnCanonicalAsync(issueId, copy.Id, context, (int)BookCopyStatus.Damaged, 125m, "Damaged spine");

		returnResult.Id.ShouldBe(issueResult.Id);

		await WithUnitOfWorkAsync(async () =>
		{
			var reloadedCopy = await _bookCopyRepository.GetAsync(copy.Id);
			reloadedCopy.Status.ShouldBe(BookCopyStatus.Damaged);
			reloadedCopy.Remarks.ShouldBe("Damaged spine");

			var issue = await _bookIssueRepository.GetAsync(issueId);
			issue.Status.ShouldBe(BookIssueStatus.Returned);
			issue.ReturnDate.ShouldBeNull();
			issue.SchoolReturnDate.ShouldBe(new DateOnly(2026, 11, 17));

			var charge = await _libraryChargeRepository.FirstOrDefaultAsync(x => x.BookIssueId == issueId);
			charge.ShouldNotBeNull();
			charge.MemberId.ShouldBe(context.ActiveStudentId);
			charge.Amount.ShouldBe(125m);
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charge.Remarks.ShouldBe("Damaged spine");
		});
	}

	[Fact]
	public async Task DeleteReservationAsync_Should_Retain_Cancelled_Request_And_History()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("RES-CANCEL"))).Single();
		var result = await GetRequiredService<ILibraryReservationAppService>().CreateAsync(new()
		{
			BookId = copy.BookId, AcademicYearId = context.AcademicYearId,
			MemberId = context.ActiveStudentId,
			RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 11, 20)
		});
		var reservationId = result.Id;

		await _bookAppService.DeleteReservationAsync(copy.BookId, reservationId);

		await WithUnitOfWorkAsync(async () =>
		{
			var reservation = await _bookReservationRepository.FindAsync(reservationId);
			reservation.ShouldNotBeNull();
			reservation.State.ShouldBe(LibraryReservationState.Cancelled);
			reservation.MemberId.ShouldBe(context.ActiveStudentId);
			(await GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().AnyAsync(row =>
				row.ReservationId == reservationId && row.EventType == "Cancelled")).ShouldBeTrue();
			(await GetRequiredService<IRepository<LibraryHold, Guid>>().AnyAsync(row =>
				row.ReservationId == reservationId && row.State == LibraryHoldState.Allocated)).ShouldBeFalse();
		});
	}

	[Fact]
	public async Task DeleteReservationAsync_Should_Reject_Wrong_Book_Route()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var reservedCopy = (await CreateCopiesAsync(context, UniqueAccession("RES-WRONG-A"))).Single();
		var otherCopy = (await CreateCopiesAsync(context, UniqueAccession("RES-WRONG-B"))).Single();
		var result = await GetRequiredService<ILibraryReservationAppService>().CreateAsync(new()
		{
			BookId = reservedCopy.BookId, AcademicYearId = context.AcademicYearId,
			MemberId = context.ActiveStudentId,
			RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 11, 20)
		});
		var reservationId = result.Id;

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_bookAppService.DeleteReservationAsync(otherCopy.BookId, reservationId));

		exception.Code.ShouldBe(LibraryErrorCodes.ReservationNotFound);
	}

	[Fact]
	public async Task Issue_Should_Count_Legacy_Member_Alias_Loans_Against_The_Same_Person_Quota()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copies = await CreateCopiesAsync(context, UniqueAccession("ALIAS-A"), UniqueAccession("ALIAS-B"),
			UniqueAccession("ALIAS-C"), UniqueAccession("ALIAS-D"));
		await SeedActiveIssuesAsync(context.ActiveStudentLibraryMemberId, copies.Take(3));

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			IssueCanonicalAsync(copies[3].Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24)));

		exception.Code.ShouldBe(LibraryErrorCodes.BorrowingLimitExceeded);
	}

	[Fact]
	public async Task Return_Search_Should_Resolve_The_Actual_Person_For_Legacy_Member_Alias_Loans()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("ALIAS-RETURN"))).Single();
		await SeedActiveIssuesAsync(context.ActiveStudentLibraryMemberId, [copy]);
		var member = await _bookIssueAppService.GetMemberAsync(context.ActiveStudentId);

		var result = await _bookIssueAppService.SearchReturnBookAsync(copy.AccessionNumber);

		result.MemberDetails.MemberName.ShouldBe(member.MemberName);
		result.MemberDetails.Id.ShouldBe(context.ActiveStudentId);
	}

	[Fact]
	public async Task Member_Tabs_Should_Include_Legacy_Alias_Reservations_And_Charges()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("ALIAS-TABS"))).Single();
		await WithUnitOfWorkAsync(async () =>
		{
			await _bookReservationRepository.InsertAsync(new BookReservation(Guid.NewGuid(), copy.BookId,
				context.ActiveStudentLibraryMemberId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(3),
				GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id), true);
			await _libraryChargeRepository.InsertAsync(new LibraryCharge(Guid.NewGuid(), context.ActiveStudentLibraryMemberId,
				0, context.ChargeCategoryId, null, 25m, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7),
				GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id), true);
		});
		var members = GetRequiredService<ILibraryMemberAppService>();

		(await members.GetReservedBooksAsync(context.ActiveStudentId)).TotalCount.ShouldBe(1);
		(await members.GetMemberChargesAsync(context.ActiveStudentId)).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Member_History_Should_Link_To_The_Catalog_Book_Instead_Of_The_Physical_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("HISTORY-BOOK"))).Single();
		await SeedActiveIssuesAsync(context.ActiveStudentLibraryMemberId, [copy]);

		var history = await GetRequiredService<ILibraryMemberAppService>().GetBorrowHistoryAsync(context.ActiveStudentId);

		history.Items.Single().BookId.ShouldBe(copy.BookId);
	}

	[Fact]
	public async Task Unicode_Accession_Should_Keep_Its_Printed_Text_And_Have_A_Scannable_Round_Trip()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var accession = $"પુસ્તક-{Guid.NewGuid():N}";
		var copy = (await CreateCopiesAsync(context, accession)).Single();
		var label = (await _bookAppService.GetCopyLabelsAsync(copy.BookId)).Labels.Single();

		label.AccessionNumber.ShouldBe(accession);
		label.BarcodeValue.ShouldNotBeNullOrWhiteSpace();
		label.BarcodeValue.All(character => character >= ' ' && character <= '~').ShouldBeTrue();
		(await _bookIssueAppService.GetByAccessionAsync(label.BarcodeValue)).AccessionNo.ShouldBe(accession);
		var scanned = await GetRequiredService<ILibraryCirculationLookupAppService>().GetCopyAsync(label.BarcodeValue);
		scanned.CopyId.ShouldBe(copy.Id);
		var issued = await IssueCanonicalAsync(scanned.CopyId, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24));
		(await _bookIssueAppService.SearchReturnBookAsync(label.BarcodeValue)).BookDetails.AccessionNo.ShouldBe(accession);
		await ReturnCanonicalAsync(Guid.Parse(issued.Id), scanned.CopyId, context);
		(await _bookIssueAppService.GetMemberAsync(context.ActiveStudentId)).Statistics.CurrentIssue.ShouldBe(0);
	}

	[Theory]
	[InlineData(-10, 0)]
	[InlineData(0, 999)]
	[InlineData(0, 3)]
	[InlineData(0, 6)]
	public async Task Return_Should_Reject_Invalid_Assessment_Or_Copy_State_Without_Closing_The_Loan(int amount, int status)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("BAD-RETURN"))).Single();
		await SeedActiveIssuesAsync(context.ActiveStudentId, [copy]);

		var loanId = await WithUnitOfWorkAsync(async () => (await _bookIssueRepository.GetAsync(row => row.BookCopyId == copy.Id)).Id);
		var error = await Should.ThrowAsync<BusinessException>(() => ReturnCanonicalAsync(loanId, copy.Id, context, status, amount));
		error.Code.ShouldBe(amount < 0 ? LibraryErrorCodes.InvalidCharge : LibraryErrorCodes.InvalidCopyStatusTransition);

		await WithUnitOfWorkAsync(async () =>
		{
			(await _bookIssueRepository.GetAsync(x => x.BookCopyId == copy.Id)).Status.ShouldBe(BookIssueStatus.Issued);
			(await _bookCopyRepository.GetAsync(copy.Id)).Status.ShouldBe(BookCopyStatus.Issued);
		});
	}

	[Fact]
	public async Task Member_List_Statistics_Should_Count_Loans_Recorded_Under_Legacy_Member_Aliases()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("ALIAS-STATS"))).Single();
		await SeedActiveIssuesAsync(context.ActiveStudentLibraryMemberId, [copy]);

		var members = await GetRequiredService<ILibraryMemberAppService>().GetListAsync(new GetLibraryMembersInput
		{
			Role = "Student", Search = "Library", MaxResultCount = 100
		});

		members.Items.Single(x => x.Id == context.ActiveStudentId).Statistics.CurrentIssue.ShouldBe(1);
	}

	[Fact]
	public async Task Scan_Should_Reject_A_Code_That_Also_Identifies_A_Different_Printed_Accession()
	{
		var context = await GetLibraryContextAsync();
		var unicode = (await CreateCopiesAsync(context, $"પુસ્તક-{Guid.NewGuid():N}")).Single();
		var scan = $"SM-LIB-COPY:{unicode.Id:N}";
		await CreateCopiesAsync(context, scan);

		var error = await Should.ThrowAsync<BusinessException>(() => _bookIssueAppService.GetByAccessionAsync(scan));

		error.Code.ShouldBe(LibraryErrorCodes.DuplicateAccessionNumber);
	}

	[Fact]
	public async Task Copy_History_Should_Page_All_Visible_Loans_And_Resolve_Their_Canonical_Borrower()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		var copy = await WithUnitOfWorkAsync(() => _bookCopyRepository.GetAsync(context.Catalog.CopyId));
		await WithUnitOfWorkAsync(async () =>
		{
			var alias = new LibraryMember(Guid.NewGuid(), copy.TenantId, context.StudentId, "HISTORY-ALIAS");
			await _libraryMemberRepository.InsertAsync(alias, true);
			var current = await _bookIssueRepository.GetAsync(context.LoanId!.Value);
			current.MemberId = alias.Id;
			await _bookIssueRepository.UpdateAsync(current, true);
			var older = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, context.StudentId, 0,
				new DateTime(2026, 10, 1), new DateTime(2026, 10, 8), copy.TenantId)
			{ Status = BookIssueStatus.Returned, ReturnDate = new DateTime(2026, 10, 5) };
			await _bookIssueRepository.InsertAsync(older, true);
		});

		var first = await CopyHistory().GetAsync(copy.BookId, copy.Id, pageSize: 1);
		var second = await CopyHistory().GetAsync(copy.BookId, copy.Id, page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(2);
		first.TotalPages.ShouldBe(2);
		first.Items.Single().MemberId.ShouldBe(context.StudentId);
		first.Items.Single().MemberName.ShouldBe("Library Borrower");
		second.Items.Single().Status.ShouldBe((int)BookIssueStatus.Returned);
		second.Items.Single().TransactionId.ShouldNotBe(first.Items.Single().TransactionId);
	}

	[Fact]
	public async Task Copy_History_Should_Filter_Foreign_Records_Before_Counting_And_Retain_Unknown_Own_Tenant_History()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		var copy = await WithUnitOfWorkAsync(() => _bookCopyRepository.GetAsync(context.Catalog.CopyId));
		var unknownLoanId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await _bookIssueRepository.InsertAsync(new BookIssue(unknownLoanId, copy.Id, copy.AccessionNumber,
				Guid.NewGuid(), 0, new DateTime(2026, 10, 1), new DateTime(2026, 10, 8), copy.TenantId)
			{ Status = BookIssueStatus.Returned }, true);
		});
		var foreignTenantId = Guid.NewGuid();
		using (GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(foreignTenantId))
			await WithUnitOfWorkAsync(() => _bookIssueRepository.InsertAsync(new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber,
				context.StudentId, 0, new DateTime(2026, 9, 1), new DateTime(2026, 9, 8), foreignTenantId)
			{ Status = BookIssueStatus.Returned }, true));

		var result = await CopyHistory().GetAsync(copy.BookId, copy.Id);

		result.TotalCount.ShouldBe(2);
		result.Items.ShouldContain(row => row.MemberId == context.StudentId);
		var unknown = result.Items.Single(row => row.TransactionId == unknownLoanId);
		unknown.MemberName.ShouldBeNull();
		unknown.MemberDisplaySource.ShouldBe("Unavailable");
		unknown.ContactNo.ShouldBeNull();
	}

	[Fact]
	public async Task Copy_History_Should_Reject_A_Copy_From_Another_Catalog_Route()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var copy = await WithUnitOfWorkAsync(() => _bookCopyRepository.GetAsync(context.Catalog.CopyId));
		var history = CopyHistory();

		var error = await Should.ThrowAsync<BusinessException>(() => history.GetAsync(Guid.NewGuid(), copy.Id));

		error.Code.ShouldBe(LibraryErrorCodes.CopyNotFound);
	}

	private ILibraryCopyHistoryAppService CopyHistory()
	{
		var service = ServiceProvider.GetService<ILibraryCopyHistoryAppService>();
		service.ShouldNotBeNull("Copy history must return scoped historical loans, rather than a single current borrower.");
		return service!;
	}

	private async Task<LibraryTestContext> GetLibraryContextAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			await LibraryTestAuthorization.GrantTenantWideAsync(ServiceProvider);
			var fixtureTenantId = GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id;
			var academicYearId = Guid.NewGuid();
			var educationBoardId = Guid.NewGuid();
			var educationMediumId = Guid.NewGuid();
			var educationMediumStageId = Guid.NewGuid();
			var studentId = Guid.NewGuid();

			await _academicYearRepository.InsertAsync(
				new AcademicYear(
					academicYearId,
					tenantId: fixtureTenantId,
					$"Library Test {Guid.NewGuid():N}"[..30],
					new DateTime(2026, 4, 1),
					new DateTime(2027, 3, 31)),
				autoSave: true);

			await _educationBoardRepository.InsertAsync(
				new EducationBoard(educationBoardId, tenantId: fixtureTenantId, EducationBoardType.CBSE, academicYearId),
				autoSave: true);

			await _educationMediumRepository.InsertAsync(
				new EducationMedium(educationMediumId, tenantId: fixtureTenantId, "English", educationBoardId),
				autoSave: true);

			await _educationMediumStageRepository.InsertAsync(
				new EducationMediumStage(
					educationMediumStageId,
					fixtureTenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
					"Primary",
					bannerPicture: null,
					educationMediumId),
				autoSave: true);

			var student = Student.CreateDirectRegistration(
				studentId,
				tenantId: fixtureTenantId,
				firstName: "Library",
				middleName: null,
				lastName: "Student",
				educationYearId: academicYearId,
				educationBoardId: educationBoardId,
				educationMediumId: educationMediumId,
				educationMediumStageId: educationMediumStageId,
				standardId: null,
				divisionId: null,
				dateOfBirth: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
				gender: GenderEnum.Male,
				primaryContactNo: "9999999999",
				email: $"library-{Guid.NewGuid():N}@example.test");
			student.AdditionalDetail!.IsLibraryMember = true;
			await _studentRepository.InsertAsync(student, autoSave: true);

			var member = new LibraryMember(
				Guid.NewGuid(),
				tenantId: fixtureTenantId,
				studentId,
				$"LIB-{Guid.NewGuid():N}"[..20],
				isActive: true);
			await _libraryMemberRepository.InsertAsync(member, autoSave: true);

			var category = new BookCategory(
				$"Regression {Guid.NewGuid():N}"[..30],
				"Regression test category",
				academicYearId,
				tenantId: fixtureTenantId);
			await _bookCategoryRepository.InsertAsync(category, autoSave: true);

			var chargeCategory = new LibraryChargeCategory(
				Guid.NewGuid(),
				tenantId: fixtureTenantId,
				academicYearId,
				"Damage",
				125m,
				isFixed: false,
				"Damage recovery charge");
			await _chargeCategoryRepository.InsertAsync(chargeCategory, autoSave: true);

			var section = new LibrarySection(
				Guid.NewGuid(),
				tenantId: fixtureTenantId,
				academicYearId,
				$"Section {Guid.NewGuid():N}"[..30],
				numberOfShelf: 1);
			await _sectionRepository.InsertAsync(section, autoSave: true);

			var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "A-1");
			await _shelfRepository.InsertAsync(shelf, autoSave: true);

			return new LibraryTestContext(
				academicYearId,
				category.Id,
				shelf.SectionId,
				shelf.Id,
				chargeCategory.Id,
				member.Id,
				member.StudentId);
		});
	}

	private async Task<List<BookCopy>> CreateCopiesAsync(LibraryTestContext context, params string[] accessions)
	{
		await _bookAppService.CreateAsync(context.AcademicYearId, BuildBookRequest(context, accessions));
		var accessionList = accessions.ToList();

		return await WithUnitOfWorkAsync(async () =>
		{
			var copies = await _bookCopyRepository.GetListAsync(x => accessionList.Contains(x.AccessionNumber));
			return copies
				.OrderBy(x => Array.IndexOf(accessions, x.AccessionNumber))
				.ToList();
		});
	}

	private async Task DeactivateLibraryMemberAsync(Guid memberId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var member = await _libraryMemberRepository.GetAsync(memberId);
			member.ChangeMemberStatus(false);
			await _libraryMemberRepository.UpdateAsync(member, autoSave: true);
		});
	}

	private async Task SeedActiveIssuesAsync(Guid memberId, IEnumerable<BookCopy> copies)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var sourceCopy in copies)
			{
				var copy = await _bookCopyRepository.GetAsync(sourceCopy.Id);
				copy.Status = BookCopyStatus.Issued;
				await _bookCopyRepository.UpdateAsync(copy, autoSave: true);
				await _bookIssueRepository.InsertAsync(
					new BookIssue(
						Guid.NewGuid(),
						copy.Id,
						copy.AccessionNumber,
						memberId,
						memberRole: 0,
						DateTime.UtcNow.Date.AddDays(-1),
						DateTime.UtcNow.Date.AddDays(7), GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id),
					autoSave: true);
			}
		});
	}

	private static CreateBookRequest BuildBookRequest(LibraryTestContext context, params string[] accessions)
	{
		return new CreateBookRequest
		{
			Title = $"Regression Book {Guid.NewGuid():N}",
			Author = "Library Test Author",
			Publisher = "Library Test Publisher",
			Edition = Guid.NewGuid().ToString("N")[..8],
			Subject = "Regression",
			CategoryId = context.CategoryId,
			Language = Language.English,
			ApplicableAge = "10+",
			Price = 100m,
			Pages = 120,
			NumberOfCopies = accessions.Length,
			Copies = accessions.Select((accession, index) => new BookCopyItem
			{
				CopyNumber = index + 1,
				AccessionNumber = accession,
				SectionId = context.SectionId,
				ShelfId = context.ShelfId
			}).ToList()
		};
	}

	private static string UniqueAccession(string prefix)
	{
		return $"TST-{prefix}-{Guid.NewGuid():N}";
	}

	private sealed record LibraryTestContext(
		Guid AcademicYearId,
		Guid CategoryId,
		Guid SectionId,
		Guid ShelfId,
		Guid ChargeCategoryId,
		Guid ActiveStudentLibraryMemberId,
		Guid ActiveStudentId);
}
