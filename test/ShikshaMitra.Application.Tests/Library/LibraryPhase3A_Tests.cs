using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPhase3A_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IBookAppService _bookAppService;
	private readonly ILibraryDashboardAppService _dashboardAppService;
	private readonly ILibraryReportAppService _reportAppService;
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

	public LibraryPhase3A_Tests()
	{
		_bookAppService = GetRequiredService<IBookAppService>();
		_dashboardAppService = GetRequiredService<ILibraryDashboardAppService>();
		_reportAppService = GetRequiredService<ILibraryReportAppService>();
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
	}

	[Fact]
	public async Task Dashboard_Overview_Should_Return_Library_Operational_Counts()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seeded = await SeedPhase3AContextAsync();

		var overview = await _dashboardAppService.GetOverviewAsync(seeded.Context.AcademicYearId);

		overview.StockTotals.TotalBooks.ShouldBe(1);
		overview.StockTotals.TotalCopies.ShouldBe(3);
		overview.StockTotals.IssuedCopies.ShouldBe(1);
		overview.StockTotals.DamagedCopies.ShouldBe(1);
		overview.Circulation.ActiveIssues.ShouldBe(1);
		overview.Circulation.OverdueIssues.ShouldBe(1);
		overview.Charges.PendingCount.ShouldBe(1);
		overview.Charges.PendingAmount.ShouldBe(50m);
		overview.Reservations.ActiveReservations.ShouldBe(1);
		overview.PopularBooks.ShouldContain(x => x.BookId == seeded.BookId && x.IssueCount >= 1);
		overview.Alerts.ShouldContain(x => x.Type == "overdue");
	}

	[Fact]
	public async Task Reports_Should_Preview_Phase3A_Report_Types()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seeded = await SeedPhase3AContextAsync();

		foreach (var reportType in new[]
		         {
			         LibraryReportTypes.BookMaster,
			         LibraryReportTypes.StockSummary,
			         LibraryReportTypes.AvailabilityStatus,
			         LibraryReportTypes.IssueReturn,
			         LibraryReportTypes.OverdueBooks,
			         LibraryReportTypes.FineCollection,
			         LibraryReportTypes.LostDamagedBooks,
			         LibraryReportTypes.MemberList,
			         LibraryReportTypes.ReservationList,
			         LibraryReportTypes.PopularBooks
		         })
		{
			var preview = await _reportAppService.PreviewAsync(new LibraryReportRequestDto
			{
				ReportType = reportType,
				Page = 1,
				PageSize = 5,
				Filters = new LibraryReportFilterDto
				{
					AcademicYearId = seeded.Context.AcademicYearId
				}
			});

			preview.ReportType.ShouldBe(reportType);
			preview.Columns.ShouldNotBeEmpty();
			preview.Rows.Count.ShouldBeLessThanOrEqualTo(5);
		}
	}

	[Fact]
	public async Task Reports_Should_Reject_ComingSoon_Or_Unknown_Report_Types()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_reportAppService.PreviewAsync(new LibraryReportRequestDto
			{
				ReportType = "Vendor-wise Purchase Report"
			}));

		exception.Code.ShouldBe(LibraryErrorCodes.ReportTypeNotSupported);
	}

	[Theory]
	[InlineData(LibraryReportTypes.IssueReturn)]
	[InlineData(LibraryReportTypes.OverdueBooks)]
	[InlineData(LibraryReportTypes.FineCollection)]
	[InlineData(LibraryReportTypes.ReservationList)]
	public async Task Reports_Should_Exclude_Transactions_For_Out_Of_Scope_Members(string reportType)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seeded = await SeedPhase3AContextAsync();
		var actor = GetRequiredService<ICurrentUser>().GetId();
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == seeded.Context.TenantId && row.UserId == actor);
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), seeded.Context.TenantId,
				userId: actor, academicYearId: seeded.Context.AcademicYearId, divisionId: Guid.NewGuid(),
				description: "Operator can only see a different division."), autoSave: true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		var executionScope = await GetRequiredService<LibraryReportScopeService>().GetCurrentExecutionScopeAsync();
		var preview = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReportGenerator>().GenerateAsync(
			new LibraryReportRequestDto
			{
				ReportType = reportType,
				Filters = new LibraryReportFilterDto
				{
					AcademicYearId = seeded.Context.AcademicYearId,
					MemberIds = [seeded.Context.ActiveStudentId]
				}
			}, executionScope, exportAllRows: true));

		preview.TotalCount.ShouldBe(0);
		preview.Rows.ShouldBeEmpty();
	}

	[Fact]
	public async Task Reports_Should_Preserve_Former_Member_Identity_In_Loan_History()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seeded = await SeedPhase3AContextAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(seeded.Context.ActiveStudentId);
			student.AdditionalDetail!.IsLibraryMember = false;
			await _studentRepository.UpdateAsync(student, autoSave: true);
		});
		var preview = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReportGenerator>().GenerateAsync(
			new LibraryReportRequestDto
			{
				ReportType = LibraryReportTypes.IssueReturn,
				Filters = new LibraryReportFilterDto { AcademicYearId = seeded.Context.AcademicYearId }
			},
			new LibraryReportExecutionScope
			{
				TenantId = seeded.Context.TenantId,
				AllowedAcademicYearIds = [seeded.Context.AcademicYearId],
				UserScope = new UserScopeContext
				{
					UserId = GetRequiredService<ICurrentUser>().GetId(),
					TenantId = seeded.Context.TenantId,
					IsAdmin = true
				}
			}, exportAllRows: true));

		var row = preview.Rows.ShouldHaveSingleItem();
		row.GetValueOrDefault("memberName").ShouldBe("Library Reporter");
		row.GetValueOrDefault("memberDisplaySource").ShouldBe("LoanSnapshot");
		row.GetValueOrDefault("memberRecordId").ShouldBe(seeded.Context.ActiveStudentId.ToString());
	}

	[Fact]
	public async Task CopyLabels_Should_Return_Accession_Barcode_Metadata()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seeded = await SeedPhase3AContextAsync();

		var labels = await _bookAppService.GetCopyLabelsAsync(seeded.BookId);

		labels.BookId.ShouldBe(seeded.BookId);
		labels.Title.ShouldNotBeNullOrWhiteSpace();
		labels.Labels.Count.ShouldBe(3);
		labels.Labels.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.AccessionNumber));
		labels.Labels.ShouldAllBe(x => x.BarcodeValue == x.AccessionNumber);
		labels.Labels.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.SectionName));
		labels.Labels.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.ShelfName));
	}

	private async Task<SeededPhase3AContext> SeedPhase3AContextAsync()
	{
		var context = await GetLibraryContextAsync();
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "UTC" });
		var accessions = new[]
		{
			UniqueAccession("P3A-A"),
			UniqueAccession("P3A-B"),
			UniqueAccession("P3A-C")
		};
		var bookId = Guid.Parse((await _bookAppService.CreateAsync(context.AcademicYearId, BuildBookRequest(context, accessions))).Id);
		var accessionList = accessions.ToList();
		var copies = await WithUnitOfWorkAsync(async () =>
		{
			var rows = await _bookCopyRepository.GetListAsync(x => accessionList.Contains(x.AccessionNumber));
			return rows.OrderBy(x => Array.IndexOf(accessions, x.AccessionNumber)).ToList();
		});

		var issueDate = context.SchoolToday.AddDays(-5);
		var dueDate = context.SchoolToday.AddDays(-1);
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = copies[0].Id,
			MemberId = context.ActiveStudentId,
			AcademicYearId = context.AcademicYearId,
			IssueDate = issueDate,
			RequestedDueDate = dueDate
		});
		plan.CanIssue.ShouldBeTrue(string.Join(", ", plan.BlockedReasons));
		var issue = await GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(new()
		{
			CopyId = copies[0].Id,
			MemberId = context.ActiveStudentId,
			AcademicYearId = context.AcademicYearId,
			IssueDate = issueDate,
			RequestedDueDate = dueDate,
			Revision = plan.Revision,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});

		var damagedCopyState = await GetRequiredService<ILibraryCopyLifecycleAppService>().GetStateAsync(bookId, copies[1].Id);
		await _bookAppService.UpdateCopyStatusAsync(bookId, copies[1].AccessionNumber, new UpdateBookCopyStatusRequest
		{
			Status = BookCopyStatus.Damaged,
			Remarks = "Regression damaged copy",
			Reason = "Damage confirmed during inspection",
			Revision = damagedCopyState.Revision
		});

		await GetRequiredService<ILibraryReservationAppService>().CreateAsync(new LibraryReservationCreateRequest
		{
			BookId = bookId,
			AcademicYearId = context.AcademicYearId,
			MemberId = context.ActiveStudentId,
			RequestedDate = context.SchoolToday,
			RequestExpiryDate = context.SchoolToday.AddDays(3)
		});

		await GetRequiredService<ILibraryChargeAppService>().CreateAsync(new CreateChargeRequest
		{
			MemberId = context.ActiveStudentId,
			ChargeCategoryId = context.ChargeCategoryId,
			BookIssueId = issue.Loan.Id,
			Amount = 50m,
			AcademicYearId = context.AcademicYearId,
			SchoolChargeDate = context.SchoolToday,
			SchoolDueDate = context.SchoolToday.AddDays(7),
			Remarks = "Assessment for the overdue regression loan."
		});

		return new SeededPhase3AContext(context, bookId);
	}

	private async Task<LibraryTestContext> GetLibraryContextAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			await LibraryTestAuthorization.GrantTenantWideAsync(ServiceProvider);
			var tenantId = GetRequiredService<ICurrentTenant>().GetId();
			var today = DateOnly.FromDateTime(GetRequiredService<IClock>().Now.ToUniversalTime());
			var academicYearId = Guid.NewGuid();
			var educationBoardId = Guid.NewGuid();
			var educationMediumId = Guid.NewGuid();
			var educationMediumStageId = Guid.NewGuid();
			var studentId = Guid.NewGuid();

			await _academicYearRepository.InsertAsync(
				new AcademicYear(
					academicYearId,
					tenantId,
					$"Library Phase3A {Guid.NewGuid():N}"[..30],
					today.AddMonths(-4).ToDateTime(TimeOnly.MinValue),
					today.AddMonths(8).ToDateTime(TimeOnly.MinValue)),
				autoSave: true);

			await _educationBoardRepository.InsertAsync(
				new EducationBoard(educationBoardId, tenantId, EducationBoardType.CBSE, academicYearId),
				autoSave: true);

			await _educationMediumRepository.InsertAsync(
				new EducationMedium(educationMediumId, tenantId, "English", educationBoardId),
				autoSave: true);

			await _educationMediumStageRepository.InsertAsync(
				new EducationMediumStage(
					educationMediumStageId,
					tenantId,
					"Primary",
					bannerPicture: null,
					educationMediumId),
				autoSave: true);

			var student = Student.CreateDirectRegistration(
				studentId,
				tenantId,
				firstName: "Library",
				middleName: null,
				lastName: "Reporter",
				educationYearId: academicYearId,
				educationBoardId: educationBoardId,
				educationMediumId: educationMediumId,
				educationMediumStageId: educationMediumStageId,
				standardId: null,
				divisionId: null,
				dateOfBirth: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
				gender: GenderEnum.Male,
				primaryContactNo: "9999999999",
				email: $"library-phase3a-{Guid.NewGuid():N}@example.test");
			student.AdditionalDetail!.IsLibraryMember = true;
			await _studentRepository.InsertAsync(student, autoSave: true);

			var member = new LibraryMember(
				Guid.NewGuid(),
				tenantId,
				studentId,
				$"LIB-{Guid.NewGuid():N}"[..20],
				isActive: true);
			await _libraryMemberRepository.InsertAsync(member, autoSave: true);

			var category = new BookCategory(
				$"Phase3A {Guid.NewGuid():N}"[..30],
				"Phase 3A regression category",
				academicYearId,
				tenantId);
			await _bookCategoryRepository.InsertAsync(category, autoSave: true);

			var chargeCategory = new LibraryChargeCategory(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"Late Fine",
				50m,
				isFixed: false,
				"Phase 3A charge");
			await _chargeCategoryRepository.InsertAsync(chargeCategory, autoSave: true);

			var section = new LibrarySection(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				$"P3A Section {Guid.NewGuid():N}"[..30],
				numberOfShelf: 1);
			await _sectionRepository.InsertAsync(section, autoSave: true);

			var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "P3A-A1");
			await _shelfRepository.InsertAsync(shelf, autoSave: true);

			return new LibraryTestContext(
				tenantId,
				academicYearId,
				category.Id,
				shelf.SectionId,
				shelf.Id,
				chargeCategory.Id,
				member.Id,
				member.StudentId,
				today);
		});
	}

	private static CreateBookRequest BuildBookRequest(LibraryTestContext context, params string[] accessions)
	{
		return new CreateBookRequest
		{
			Title = $"Phase 3A Book {Guid.NewGuid():N}",
			Isbn = $"ISBN-{Guid.NewGuid():N}"[..17],
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

	private sealed record SeededPhase3AContext(LibraryTestContext Context, Guid BookId);

	private sealed record LibraryTestContext(
		Guid TenantId,
		Guid AcademicYearId,
		Guid CategoryId,
		Guid SectionId,
		Guid ShelfId,
		Guid ChargeCategoryId,
		Guid ActiveStudentLibraryMemberId,
		Guid ActiveStudentId,
		DateOnly SchoolToday);
}
