using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
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

public class LibraryCoreWorkflow_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
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
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("INACTIVE"))).Single();
		await DeactivateLibraryMemberAsync(context.ActiveStudentLibraryMemberId);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_bookIssueAppService.IssueAsync(new IssueBookRequest
			{
				AccessionNo = copy.AccessionNumber,
				MemberId = context.ActiveStudentId,
				IssueDate = DateTime.UtcNow.Date,
				DueDate = DateTime.UtcNow.Date.AddDays(7)
			}));

		exception.Code.ShouldBe(LibraryErrorCodes.MemberInactive);
	}

	[Fact]
	public async Task IssueAsync_Should_Reject_When_Borrowing_Limit_Is_Reached()
	{
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
			_bookIssueAppService.IssueAsync(new IssueBookRequest
			{
				AccessionNo = copies[3].AccessionNumber,
				MemberId = context.ActiveStudentId,
				IssueDate = DateTime.UtcNow.Date,
				DueDate = DateTime.UtcNow.Date.AddDays(7)
			}));

		exception.Code.ShouldBe(LibraryErrorCodes.BorrowingLimitExceeded);
	}

	[Fact]
	public async Task ReturnAsync_Should_Create_Charge_And_Set_Copy_Status()
	{
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("RETURN"))).Single();

		var issueResult = await _bookIssueAppService.IssueAsync(new IssueBookRequest
		{
			AccessionNo = copy.AccessionNumber,
			MemberId = context.ActiveStudentId,
			IssueDate = DateTime.UtcNow.Date.AddDays(-3),
			DueDate = DateTime.UtcNow.Date.AddDays(-1)
		});
		var issueId = Guid.Parse(issueResult.Id);

		var returnResult = await _bookIssueAppService.ReturnAsync(new ReturnBookRequest
		{
			AccessionNo = copy.AccessionNumber,
			MemberId = context.ActiveStudentId,
			ReturnDate = DateTime.UtcNow.Date,
			ChargeCategoryId = context.ChargeCategoryId,
			Amount = 125m,
			FineDueDate = DateTime.UtcNow.Date.AddDays(5),
			CopyStatus = BookCopyStatus.Damaged,
			Remarks = "Damaged spine"
		});

		returnResult.Id.ShouldBe(issueResult.Id);

		await WithUnitOfWorkAsync(async () =>
		{
			var reloadedCopy = await _bookCopyRepository.GetAsync(copy.Id);
			reloadedCopy.Status.ShouldBe(BookCopyStatus.Damaged);
			reloadedCopy.Remarks.ShouldBe("Damaged spine");

			var issue = await _bookIssueRepository.GetAsync(issueId);
			issue.Status.ShouldBe(BookIssueStatus.Returned);
			issue.ReturnDate.ShouldBe(DateTime.UtcNow.Date);

			var charge = await _libraryChargeRepository.FirstOrDefaultAsync(x => x.BookIssueId == issueId);
			charge.ShouldNotBeNull();
			charge.MemberId.ShouldBe(context.ActiveStudentId);
			charge.Amount.ShouldBe(125m);
			charge.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			charge.Remarks.ShouldBe("Damaged spine");
		});
	}

	[Fact]
	public async Task DeleteReservationAsync_Should_Remove_Book_Reservation()
	{
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("RES-CANCEL"))).Single();
		var result = await _bookAppService.ReserveAsync(copy.BookId, new CreateBookReservationRequest
		{
			MemberId = context.ActiveStudentId,
			RequestedDate = DateTime.UtcNow.Date,
			ReserveDueDate = DateTime.UtcNow.Date.AddDays(3)
		});
		var reservationId = Guid.Parse(result.Id);

		await _bookAppService.DeleteReservationAsync(copy.BookId, reservationId);

		await WithUnitOfWorkAsync(async () =>
		{
			var reservation = await _bookReservationRepository.FindAsync(reservationId);
			reservation.ShouldBeNull();
		});
	}

	[Fact]
	public async Task DeleteReservationAsync_Should_Reject_Wrong_Book_Route()
	{
		var context = await GetLibraryContextAsync();
		var reservedCopy = (await CreateCopiesAsync(context, UniqueAccession("RES-WRONG-A"))).Single();
		var otherCopy = (await CreateCopiesAsync(context, UniqueAccession("RES-WRONG-B"))).Single();
		var result = await _bookAppService.ReserveAsync(reservedCopy.BookId, new CreateBookReservationRequest
		{
			MemberId = context.ActiveStudentId,
			RequestedDate = DateTime.UtcNow.Date,
			ReserveDueDate = DateTime.UtcNow.Date.AddDays(3)
		});
		var reservationId = Guid.Parse(result.Id);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_bookAppService.DeleteReservationAsync(otherCopy.BookId, reservationId));

		exception.Code.ShouldBe(LibraryErrorCodes.ReservationNotFound);
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
					$"Library Test {Guid.NewGuid():N}"[..30],
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
				tenantId: null,
				studentId,
				$"LIB-{Guid.NewGuid():N}"[..20],
				isActive: true);
			await _libraryMemberRepository.InsertAsync(member, autoSave: true);

			var category = new BookCategory(
				$"Regression {Guid.NewGuid():N}"[..30],
				"Regression test category",
				academicYearId,
				tenantId: null);
			await _bookCategoryRepository.InsertAsync(category, autoSave: true);

			var chargeCategory = new LibraryChargeCategory(
				Guid.NewGuid(),
				tenantId: null,
				academicYearId,
				"Damage",
				125m,
				isFixed: false,
				"Damage recovery charge");
			await _chargeCategoryRepository.InsertAsync(chargeCategory, autoSave: true);

			var section = new LibrarySection(
				Guid.NewGuid(),
				tenantId: null,
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
						DateTime.UtcNow.Date.AddDays(7)),
					autoSave: true);
			}
		});
	}

	private static CreateBookRequest BuildBookRequest(LibraryTestContext context, params string[] accessions)
	{
		return new CreateBookRequest
		{
			Title = $"Regression Book {Guid.NewGuid():N}",
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

	private sealed record LibraryTestContext(
		Guid AcademicYearId,
		Guid CategoryId,
		Guid SectionId,
		Guid ShelfId,
		Guid ChargeCategoryId,
		Guid ActiveStudentLibraryMemberId,
		Guid ActiveStudentId);
}
