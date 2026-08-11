using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Reports;
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

public class LibraryPhase3A_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IBookAppService _bookAppService;
	private readonly IBookIssueAppService _bookIssueAppService;
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
	private readonly IRepository<BookIssue, Guid> _bookIssueRepository;
	private readonly IRepository<LibraryCharge, Guid> _libraryChargeRepository;

	public LibraryPhase3A_Tests()
	{
		_bookAppService = GetRequiredService<IBookAppService>();
		_bookIssueAppService = GetRequiredService<IBookIssueAppService>();
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
		_bookIssueRepository = GetRequiredService<IRepository<BookIssue, Guid>>();
		_libraryChargeRepository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
	}

	[Fact]
	public async Task Dashboard_Overview_Should_Return_Library_Operational_Counts()
	{
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

	[Fact]
	public async Task CopyLabels_Should_Return_Accession_Barcode_Metadata()
	{
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

		var issue = await _bookIssueAppService.IssueAsync(new IssueBookRequest
		{
			AccessionNo = copies[0].AccessionNumber,
			MemberId = context.ActiveStudentId,
			IssueDate = DateTime.UtcNow.Date.AddDays(-5),
			DueDate = DateTime.UtcNow.Date.AddDays(-1)
		});
		var issueId = Guid.Parse(issue.Id);

		await _bookAppService.UpdateCopyStatusAsync(bookId, copies[1].AccessionNumber, new UpdateBookCopyStatusRequest
		{
			Status = BookCopyStatus.Damaged,
			Remarks = "Regression damaged copy"
		});

		await _bookAppService.ReserveAsync(bookId, new CreateBookReservationRequest
		{
			MemberId = context.ActiveStudentId,
			RequestedDate = DateTime.UtcNow.Date,
			ReserveDueDate = DateTime.UtcNow.Date.AddDays(3)
		});

		await WithUnitOfWorkAsync(async () =>
		{
			await _libraryChargeRepository.InsertAsync(new LibraryCharge(
				Guid.NewGuid(),
				context.ActiveStudentId,
				memberRole: 0,
				context.ChargeCategoryId,
				issueId,
				50m,
				DateTime.UtcNow.Date,
				DateTime.UtcNow.Date.AddDays(7)), autoSave: true);
		});

		return new SeededPhase3AContext(context, bookId);
	}

	private async Task<LibraryTestContext> GetLibraryContextAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var academicYearId = Guid.NewGuid();
			var educationBoardId = Guid.NewGuid();
			var educationMediumId = Guid.NewGuid();
			var educationMediumStageId = Guid.NewGuid();
			var studentId = Guid.NewGuid();

			await _academicYearRepository.InsertAsync(
				new AcademicYear(
					academicYearId,
					tenantId: null,
					$"Library Phase3A {Guid.NewGuid():N}"[..30],
					new DateTime(2026, 4, 1),
					new DateTime(2027, 3, 31)),
				autoSave: true);

			await _educationBoardRepository.InsertAsync(
				new EducationBoard(educationBoardId, tenantId: null, EducationBoardType.CBSE, academicYearId),
				autoSave: true);

			await _educationMediumRepository.InsertAsync(
				new EducationMedium(educationMediumId, tenantId: null, "English", educationBoardId),
				autoSave: true);

			await _educationMediumStageRepository.InsertAsync(
				new EducationMediumStage(
					educationMediumStageId,
					Guid.Parse("11111111-1111-1111-1111-111111111111"),
					"Primary",
					bannerPicture: null,
					educationMediumId),
				autoSave: true);

			var student = Student.CreateForAdmission(
				studentId,
				tenantId: null,
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
				tenantId: null,
				studentId,
				$"LIB-{Guid.NewGuid():N}"[..20],
				isActive: true);
			await _libraryMemberRepository.InsertAsync(member, autoSave: true);

			var category = new BookCategory(
				$"Phase3A {Guid.NewGuid():N}"[..30],
				"Phase 3A regression category",
				academicYearId,
				tenantId: null);
			await _bookCategoryRepository.InsertAsync(category, autoSave: true);

			var chargeCategory = new LibraryChargeCategory(
				Guid.NewGuid(),
				tenantId: null,
				academicYearId,
				"Late Fine",
				50m,
				isFixed: false,
				"Phase 3A charge");
			await _chargeCategoryRepository.InsertAsync(chargeCategory, autoSave: true);

			var section = new LibrarySection(
				Guid.NewGuid(),
				tenantId: null,
				academicYearId,
				$"P3A Section {Guid.NewGuid():N}"[..30],
				numberOfShelf: 1);
			await _sectionRepository.InsertAsync(section, autoSave: true);

			var shelf = new LibraryShelf(Guid.NewGuid(), section.Id, "P3A-A1");
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

	private static CreateBookRequest BuildBookRequest(LibraryTestContext context, params string[] accessions)
	{
		return new CreateBookRequest
		{
			Title = $"Phase 3A Book {Guid.NewGuid():N}",
			Isbn = $"ISBN-{Guid.NewGuid():N}",
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
		Guid AcademicYearId,
		Guid CategoryId,
		Guid SectionId,
		Guid ShelfId,
		Guid ChargeCategoryId,
		Guid ActiveStudentLibraryMemberId,
		Guid ActiveStudentId);
}
