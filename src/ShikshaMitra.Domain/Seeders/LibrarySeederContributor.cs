using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class LibrarySeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<BookCategory, Guid> _bookCategoryRepository;
	private readonly IRepository<LibraryChargeCategory, Guid> _chargeCategoryRepository;
	private readonly IRepository<LibraryTiming, Guid> _libraryTimingRepository;
	private readonly IRepository<BorrowingRule, Guid> _borrowingRuleRepository;
	private readonly IRepository<LibrarySection, Guid> _sectionRepository;
	private readonly IRepository<LibraryShelf, Guid> _shelfRepository;
	private readonly IRepository<Book, Guid> _bookRepository;
	private readonly IRepository<BookCopy, Guid> _bookCopyRepository;
	private readonly IRepository<BookIssue, Guid> _bookIssueRepository;
	private readonly IRepository<BookReservation, Guid> _bookReservationRepository;
	private readonly IRepository<LibraryCharge, Guid> _libraryChargeRepository;
	private readonly IRepository<LibraryMember, Guid> _libraryMemberRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly ILogger<LibrarySeederContributor> _logger;

	public LibrarySeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IAcademicYearRepository academicYearRepository,
		IRepository<BookCategory, Guid> bookCategoryRepository,
		IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
		IRepository<LibraryTiming, Guid> libraryTimingRepository,
		IRepository<BorrowingRule, Guid> borrowingRuleRepository,
		IRepository<LibrarySection, Guid> sectionRepository,
		IRepository<LibraryShelf, Guid> shelfRepository,
		IRepository<Book, Guid> bookRepository,
		IRepository<BookCopy, Guid> bookCopyRepository,
		IRepository<BookIssue, Guid> bookIssueRepository,
		IRepository<BookReservation, Guid> bookReservationRepository,
		IRepository<LibraryCharge, Guid> libraryChargeRepository,
		IRepository<LibraryMember, Guid> libraryMemberRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<Employee, Guid> employeeRepository,
		ILogger<LibrarySeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_academicYearRepository = academicYearRepository;
		_bookCategoryRepository = bookCategoryRepository;
		_chargeCategoryRepository = chargeCategoryRepository;
		_libraryTimingRepository = libraryTimingRepository;
		_borrowingRuleRepository = borrowingRuleRepository;
		_sectionRepository = sectionRepository;
		_shelfRepository = shelfRepository;
		_bookRepository = bookRepository;
		_bookCopyRepository = bookCopyRepository;
		_bookIssueRepository = bookIssueRepository;
		_bookReservationRepository = bookReservationRepository;
		_libraryChargeRepository = libraryChargeRepository;
		_libraryMemberRepository = libraryMemberRepository;
		_studentRepository = studentRepository;
		_employeeRepository = employeeRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (await _bookRepository.GetCountAsync() > 0)
		{
			_logger.LogInformation("[LibrarySeeder] Library books already exist, skipping. TenantId={TenantId}", CurrentTenant.Id);
			return;
		}

		var academicYear = await _academicYearRepository.FirstOrDefaultAsync();
		if (academicYear == null)
		{
			_logger.LogWarning("[LibrarySeeder] No academic year found. Skipping Library seeding.");
			return;
		}

		var categories = await SeedBookCategoriesAsync(academicYear.Id);
		var chargeCategories = await SeedChargeCategoriesAsync(academicYear.Id);
		await SeedLibraryHoursAsync(academicYear.Id);
		await SeedBorrowingRulesAsync(academicYear.Id);
		var shelves = await SeedSectionsAsync(academicYear.Id);
		var members = await SeedMembersAsync();
		var copies = await SeedBooksAndCopiesAsync(academicYear.Id, categories, shelves);
		await SeedIssueReservationAndChargeAsync(members, copies, chargeCategories);

		await CurrentUnitOfWork.SaveChangesAsync();
		_logger.LogInformation("[LibrarySeeder] Completed Library demo data. TenantId={TenantId}", CurrentTenant.Id);
	}

	private async Task<List<BookCategory>> SeedBookCategoriesAsync(Guid academicYearId)
	{
		var categories = new[]
		{
			new BookCategory("Reference", "Encyclopedias, dictionaries, and desk references.", academicYearId, CurrentTenant.Id),
			new BookCategory("Textbook", "Curriculum-aligned textbooks and guides.", academicYearId, CurrentTenant.Id),
			new BookCategory("Fiction", "Stories, novels, and general reading.", academicYearId, CurrentTenant.Id)
		};

		await _bookCategoryRepository.InsertManyAsync(categories, autoSave: true);
		return categories.ToList();
	}

	private async Task<List<LibraryChargeCategory>> SeedChargeCategoriesAsync(Guid academicYearId)
	{
		var categories = new[]
		{
			new LibraryChargeCategory(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Late Return", 10m, true, "Per-day late return fine."),
			new LibraryChargeCategory(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Damaged Book", 250m, false, "Damage recovery charge."),
			new LibraryChargeCategory(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Lost Book", 500m, false, "Lost book replacement charge.")
		};

		await _chargeCategoryRepository.InsertManyAsync(categories, autoSave: true);
		return categories.ToList();
	}

	private async Task SeedLibraryHoursAsync(Guid academicYearId)
	{
		var days = new[]
		{
			("Monday", "08:00", "15:00", 0),
			("Tuesday", "08:00", "15:00", 0),
			("Wednesday", "08:00", "15:00", 0),
			("Thursday", "08:00", "15:00", 0),
			("Friday", "08:00", "15:00", 0),
			("Saturday", "09:00", "13:00", 0),
			("Sunday", "00:00", "00:00", 1)
		};

		var timings = days
			.Select(x => new LibraryTiming(GuidGenerator.Create(), x.Item1, x.Item2, x.Item3, x.Item4, academicYearId, CurrentTenant.Id))
			.ToList();

		await _libraryTimingRepository.InsertManyAsync(timings, autoSave: true);
	}

	private async Task SeedBorrowingRulesAsync(Guid academicYearId)
	{
		var rules = new[]
		{
			new BorrowingRule(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, role: 0, maxBooksAtTime: 3, reservationLimit: 2),
			new BorrowingRule(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, role: 1, maxBooksAtTime: 5, reservationLimit: 3)
		};

		await _borrowingRuleRepository.InsertManyAsync(rules, autoSave: true);
	}

	private async Task<List<LibraryShelf>> SeedSectionsAsync(Guid academicYearId)
	{
		var general = new LibrarySection(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "General Reading", 2);
		var reference = new LibrarySection(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Reference Wing", 2);
		await _sectionRepository.InsertManyAsync(new[] { general, reference }, autoSave: true);

		var shelves = new[]
		{
			new LibraryShelf(GuidGenerator.Create(), general.Id, "G-A"),
			new LibraryShelf(GuidGenerator.Create(), general.Id, "G-B"),
			new LibraryShelf(GuidGenerator.Create(), reference.Id, "R-A"),
			new LibraryShelf(GuidGenerator.Create(), reference.Id, "R-B")
		};

		await _shelfRepository.InsertManyAsync(shelves, autoSave: true);
		return shelves.ToList();
	}

	private async Task<List<(Guid MemberId, int Role)>> SeedMembersAsync()
	{
		var members = new List<(Guid MemberId, int Role)>();
		var students = (await _studentRepository.GetListAsync())
			.Where(x => x.AdditionalDetail != null)
			.Take(3)
			.ToList();

		foreach (var student in students)
		{
			student.AdditionalDetail!.IsLibraryMember = true;
			await _studentRepository.UpdateAsync(student, autoSave: true);

			if (!await _libraryMemberRepository.AnyAsync(x => x.StudentId == student.Id))
			{
				await _libraryMemberRepository.InsertAsync(
					new LibraryMember(GuidGenerator.Create(), CurrentTenant.Id, student.Id, $"LIB-{members.Count + 1001}", true),
					autoSave: true);
			}

			members.Add((student.Id, 0));
		}

		var employees = (await _employeeRepository.GetListAsync())
			.Where(x => x.AdditionalDetails != null)
			.Take(2)
			.ToList();

		foreach (var employee in employees)
		{
			employee.AdditionalDetails!.Patch(
				bplCardNo: null,
				personWithDisability: null,
				secondLanguage: null,
				libraryMember: true,
				transportMember: null);
			await _employeeRepository.UpdateAsync(employee, autoSave: true);
			members.Add((employee.Id, 1));
		}

		return members;
	}

	private async Task<List<BookCopy>> SeedBooksAndCopiesAsync(Guid academicYearId, IReadOnlyList<BookCategory> categories, IReadOnlyList<LibraryShelf> shelves)
	{
		var referenceShelf = shelves.First(x => x.ShelfName.StartsWith("R-", StringComparison.OrdinalIgnoreCase));
		var generalShelf = shelves.First(x => x.ShelfName.StartsWith("G-", StringComparison.OrdinalIgnoreCase));

		var books = new[]
		{
			new Book("Student Atlas of India", "9780000000011", "Academic Press", "Shiksha Publications", "2026", "Geography", categories[0].Id, Language.English, "10+", 350m, 128, 2, academicYearId, CurrentTenant.Id),
			new Book("Mathematics Practice Companion", "9780000000028", "R. Mehta", "Learning House", "2026", "Mathematics", categories[1].Id, Language.English, "12+", 450m, 240, 2, academicYearId, CurrentTenant.Id),
			new Book("Stories from Gujarat", "9780000000035", "K. Patel", "Open Shelf", "2026", "Literature", categories[2].Id, Language.Gujarati, "8+", 220m, 156, 2, academicYearId, CurrentTenant.Id)
		};

		await _bookRepository.InsertManyAsync(books, autoSave: true);

		var copies = new[]
		{
			new BookCopy(GuidGenerator.Create(), books[0].Id, 1, "LIB-0001", referenceShelf.SectionId, referenceShelf.Id),
			new BookCopy(GuidGenerator.Create(), books[0].Id, 2, "LIB-0002", referenceShelf.SectionId, referenceShelf.Id),
			new BookCopy(GuidGenerator.Create(), books[1].Id, 1, "LIB-0003", generalShelf.SectionId, generalShelf.Id),
			new BookCopy(GuidGenerator.Create(), books[1].Id, 2, "LIB-0004", generalShelf.SectionId, generalShelf.Id),
			new BookCopy(GuidGenerator.Create(), books[2].Id, 1, "LIB-0005", generalShelf.SectionId, generalShelf.Id),
			new BookCopy(GuidGenerator.Create(), books[2].Id, 2, "LIB-0006", generalShelf.SectionId, generalShelf.Id)
		};

		await _bookCopyRepository.InsertManyAsync(copies, autoSave: true);
		return copies.ToList();
	}

	private async Task SeedIssueReservationAndChargeAsync(
		IReadOnlyList<(Guid MemberId, int Role)> members,
		IReadOnlyList<BookCopy> copies,
		IReadOnlyList<LibraryChargeCategory> chargeCategories)
	{
		if (members.Count == 0 || copies.Count < 3)
		{
			return;
		}

		var issue = new BookIssue(
			GuidGenerator.Create(),
			copies[0].Id,
			copies[0].AccessionNumber,
			members[0].MemberId,
			members[0].Role,
			DateTime.UtcNow.Date.AddDays(-7),
			DateTime.UtcNow.Date.AddDays(7),
			CurrentTenant.Id);

		copies[0].Status = BookCopyStatus.Issued;
		await _bookIssueRepository.InsertAsync(issue, autoSave: true);
		await _bookCopyRepository.UpdateAsync(copies[0], autoSave: true);

		if (members.Count > 1)
		{
			await _bookReservationRepository.InsertAsync(
				new BookReservation(GuidGenerator.Create(), copies[2].BookId, members[1].MemberId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(5), CurrentTenant.Id),
				autoSave: true);
		}

		await _libraryChargeRepository.InsertAsync(
			new LibraryCharge(
				GuidGenerator.Create(),
				members[0].MemberId,
				members[0].Role,
				chargeCategories[0].Id,
				issue.Id,
				20m,
				DateTime.UtcNow.Date,
				DateTime.UtcNow.Date.AddDays(7),
				CurrentTenant.Id)
			{
				Remarks = "Demo late-return fine"
			},
			autoSave: true);
	}
}
