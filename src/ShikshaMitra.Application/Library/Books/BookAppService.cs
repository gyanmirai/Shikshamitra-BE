using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
[RemoteService(false)]
public class BookAppService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository,
	IRepository<LibraryShelf, Guid> shelfRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<BorrowingRule, Guid> borrowingRuleRepository,
	IRepository<Members.LibraryMember, Guid> libraryMemberRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	LibraryNotificationReminderService notificationReminderService)
	: ApplicationService, IBookAppService
{
	private const int DefaultStudentReservationLimit = 2;
	private const int DefaultStaffReservationLimit = 3;

	public async Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? alphabet = null)
	{
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await bookRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length >= 2)
		{
			var s = search.Trim().ToLower();
			query = query.Where(x =>
				x.Title.ToLower().Contains(s) ||
				x.Author.ToLower().Contains(s) ||
				x.Isbn.ToLower().Contains(s));
		}

		if (!string.IsNullOrWhiteSpace(alphabet) && alphabet.Trim().Length == 1 && char.IsLetter(alphabet.Trim()[0]))
		{
			var letter = alphabet.Trim().ToUpper();
			query = query.Where(x => x.Title.ToUpper().StartsWith(letter));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new PaginatedResponse<BookDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = ObjectMapper.Map<List<Book>, List<BookDto>>(items)
		};
	}

	public async Task<BookDetailDto> GetAsync(Guid bookId)
	{
		var book = await bookRepository.GetAsync(bookId);

		var copiesQuery = await bookCopyRepository.GetQueryableAsync();
		var copies = await AsyncExecuter.ToListAsync(copiesQuery.Where(c => c.BookId == bookId));

		var category = await categoryRepository.FindAsync(book.CategoryId);

		var sectionIds = copies.Select(c => c.SectionId).Distinct().ToList();
		var shelfIds = copies.Select(c => c.ShelfId).Distinct().ToList();

		var sectionsQuery = await sectionRepository.GetQueryableAsync();
		var sections = await AsyncExecuter.ToListAsync(sectionsQuery.Where(s => sectionIds.Contains(s.Id)));
		var sectionLookup = sections.ToDictionary(s => s.Id, s => s.SectionName ?? string.Empty);

		var shelvesQuery = await shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(shelvesQuery.Where(s => shelfIds.Contains(s.Id)));
		var shelfLookup = shelves.ToDictionary(s => s.Id, s => s.ShelfName ?? string.Empty);

		return new BookDetailDto
		{
			Id = book.Id,
			Title = book.Title,
			Author = book.Author,
			Publisher = book.Publisher,
			Subject = book.Subject,
			Isbn = book.Isbn,
			Language = (int)book.Language,
			Edition = book.Edition,
			Category = category != null
				? new NamedIdDto { Id = category.Id, Name = category.Name }
				: new NamedIdDto { Id = book.CategoryId, Name = string.Empty },
			ApplicableAge = book.ApplicableAge,
			Pages = book.Pages,
			Price = book.Price,
			CoverImage = book.CoverImage,
			AddedOn = book.CreationTime,
			Copies = copies.Select(c => new BookDetailCopyDto
			{
				CopyId = c.Id,
				AccessionNo = c.AccessionNumber,
				Status = (int)c.Status,
				Section = new NamedIdDto
				{
					Id = c.SectionId,
					Name = sectionLookup.GetValueOrDefault(c.SectionId, string.Empty)
				},
				Shelf = new NamedIdDto
				{
					Id = c.ShelfId,
					Name = shelfLookup.GetValueOrDefault(c.ShelfId, string.Empty)
				},
				Remarks = c.Remarks
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input)
	{
		var resolvedAcademicYearId = EnsureAcademicYear(academicYearId);
		await EnsureCategoryAsync(input.CategoryId, resolvedAcademicYearId);
		await EnsureBookIsUniqueAsync(input.Isbn, input.Edition, resolvedAcademicYearId, ignoredBookId: null);

		var copies = NormalizeCopyItems(input.Copies);
		if (copies.Count > 0)
		{
			await EnsureCopyPayloadIsValidAsync(Guid.Empty, copies, allowSameBookAccession: false);
		}

		var entity = new Book(
			input.Title.Trim(),
			input.Isbn.Trim(),
			input.Author.Trim(),
			input.Publisher?.Trim() ?? string.Empty,
			input.Edition?.Trim() ?? string.Empty,
			input.Subject?.Trim() ?? string.Empty,
			input.CategoryId,
			input.Language,
			input.ApplicableAge?.Trim() ?? string.Empty,
			input.Price,
			input.Pages,
			copies.Count > 0 ? copies.Count : input.NumberOfCopies,
			resolvedAcademicYearId,
			CurrentTenant.Id)
		{
			CoverImage = input.CoverImage
		};

		await bookRepository.InsertAsync(entity, autoSave: true);

		if (copies.Count > 0)
		{
			var bookCopies = copies.Select(c => new BookCopy(
				GuidGenerator.Create(),
				entity.Id,
				c.CopyNumber,
				c.AccessionNumber,
				c.SectionId,
				c.ShelfId)).ToList();

			await bookCopyRepository.InsertManyAsync(bookCopies, autoSave: true);
		}

		return new GenericCreatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Book created successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input)
	{
		var entity = await bookRepository.GetAsync(bookId);
		var resolvedAcademicYearId = EnsureAcademicYear(entity.AcademicYearId);

		await EnsureCategoryAsync(input.CategoryId, resolvedAcademicYearId);
		await EnsureBookIsUniqueAsync(input.Isbn, input.Edition, resolvedAcademicYearId, bookId);

		entity.Title = input.Title.Trim();
		entity.Isbn = input.Isbn.Trim();
		entity.Author = input.Author.Trim();
		entity.Publisher = input.Publisher?.Trim() ?? string.Empty;
		entity.Edition = input.Edition?.Trim() ?? string.Empty;
		entity.Subject = input.Subject?.Trim() ?? string.Empty;
		entity.CategoryId = input.CategoryId;
		entity.Language = input.Language;
		entity.ApplicableAge = input.ApplicableAge?.Trim() ?? string.Empty;
		entity.Price = input.Price;
		entity.Pages = input.Pages;
		entity.NumberOfCopies = input.NumberOfCopies;
		entity.CoverImage = input.CoverImage;

		await bookRepository.UpdateAsync(entity, autoSave: true);

		var copies = NormalizeCopyItems(input.Copies);
		if (copies.Count > 0)
		{
			await PutCopiesAsync(bookId, new PutBookCopiesRequest { Copies = copies });
		}

		return new GenericUpdatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Book updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)]
	public async Task DeleteAsync(Guid bookId)
	{
		await EnsureNoActiveIssuesForBookAsync(bookId);
		await bookRepository.DeleteAsync(bookId, autoSave: true);
	}

	public async Task<PaginatedResponse<BookCopyDto>> GetCopiesAsync(Guid bookId, int page = 1, int pageSize = 20)
	{
		await bookRepository.GetAsync(bookId);

		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await bookCopyRepository.GetQueryableAsync();
		query = query.Where(x => x.BookId == bookId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		var copies = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.CopyNumber)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new PaginatedResponse<BookCopyDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = copies.Select(x => new BookCopyDto
			{
				Id = x.Id,
				CopyNumber = x.CopyNumber,
				AccessionNumber = x.AccessionNumber,
				SectionId = x.SectionId,
				ShelfId = x.ShelfId,
				Status = x.Status.ToString(),
				Remarks = x.Remarks
			}).ToList()
		};
	}

	public async Task<LibraryCopyLabelBatchDto> GetCopyLabelsAsync(Guid bookId)
	{
		var book = await bookRepository.GetAsync(bookId);

		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var copies = await AsyncExecuter.ToListAsync(
			copyQuery
				.Where(x => x.BookId == bookId)
				.OrderBy(x => x.CopyNumber));

		var category = await categoryRepository.FindAsync(book.CategoryId);
		var sectionIds = copies.Select(x => x.SectionId).Distinct().ToList();
		var shelfIds = copies.Select(x => x.ShelfId).Distinct().ToList();

		var sectionQuery = await sectionRepository.GetQueryableAsync();
		var sections = await AsyncExecuter.ToListAsync(sectionQuery.Where(x => sectionIds.Contains(x.Id)));
		var sectionLookup = sections.ToDictionary(x => x.Id, x => x.SectionName ?? string.Empty);

		var shelfQuery = await shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(shelfQuery.Where(x => shelfIds.Contains(x.Id)));
		var shelfLookup = shelves.ToDictionary(x => x.Id, x => x.ShelfName ?? string.Empty);

		return new LibraryCopyLabelBatchDto
		{
			BookId = book.Id,
			Title = book.Title,
			Author = book.Author,
			Isbn = book.Isbn,
			CategoryName = category?.Name ?? string.Empty,
			Labels = copies.Select(copy => new LibraryCopyLabelDto
			{
				CopyId = copy.Id,
				CopyNumber = copy.CopyNumber,
				AccessionNumber = copy.AccessionNumber,
				SectionId = copy.SectionId,
				SectionName = sectionLookup.GetValueOrDefault(copy.SectionId, string.Empty),
				ShelfId = copy.ShelfId,
				ShelfName = shelfLookup.GetValueOrDefault(copy.ShelfId, string.Empty),
				BarcodeValue = copy.AccessionNumber
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<GenericUpdatedResponse> PutCopiesAsync(Guid bookId, PutBookCopiesRequest input)
	{
		var book = await bookRepository.GetAsync(bookId);
		var copies = NormalizeCopyItems(input.Copies);

		if (copies.Count == 0)
		{
			throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, "At least one book copy is required.");
		}

		await EnsureNoActiveIssuesForBookAsync(bookId);
		await EnsureCopyPayloadIsValidAsync(bookId, copies, allowSameBookAccession: true);

		await bookCopyRepository.DeleteDirectAsync(x => x.BookId == bookId);

		var bookCopies = copies.Select(c => new BookCopy(
			GuidGenerator.Create(),
			bookId,
			c.CopyNumber,
			c.AccessionNumber,
			c.SectionId,
			c.ShelfId)).ToList();

		await bookCopyRepository.InsertManyAsync(bookCopies, autoSave: true);

		book.NumberOfCopies = bookCopies.Count;
		await bookRepository.UpdateAsync(book, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = book.Id.ToString(),
			Message = "Book copies updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<GenericUpdatedResponse> UpdateCopyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input)
	{
		var copy = await FindCopyAsync(bookId, accessionNumber);
		if (copy == null)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotFound, $"Book copy with accession number '{accessionNumber}' was not found.");
		}

		await EnsureStatusTransitionAsync(copy, input.Status);

		copy.Status = input.Status;
		copy.Remarks = input.Remarks;

		await bookCopyRepository.UpdateAsync(copy, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = copy.Id.ToString(),
			Message = "Book copy status updated successfully."
		};
	}

	public async Task<PaginatedResponse<BookReservationDto>> GetReservationsAsync(Guid bookId, int page = 1, int pageSize = 20)
	{
		await bookRepository.GetAsync(bookId);

		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await reservationRepository.GetQueryableAsync();
		query = query.Where(x => x.BookId == bookId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		var reservations = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.RequestedDate)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new PaginatedResponse<BookReservationDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = reservations.Select(x => new BookReservationDto
			{
				Id = x.Id,
				BookId = x.BookId,
				MemberId = x.MemberId,
				RequestedDate = x.RequestedDate,
				ReserveDueDate = x.ReserveDueDate
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)]
	public async Task<GenericCreatedResponse> ReserveAsync(Guid bookId, CreateBookReservationRequest input)
	{
		var book = await bookRepository.GetAsync(bookId);
		if (input.ReserveDueDate.Date < input.RequestedDate.Date)
		{
			throw LibraryException(LibraryErrorCodes.InvalidIssueDateRange, "Reserve due date must be on or after requested date.");
		}

		var memberRole = await ResolveActiveMemberRoleAsync(input.MemberId);
		var rule = await GetBorrowingRuleAsync(book.AcademicYearId, memberRole);

		var reservationQuery = await reservationRepository.GetQueryableAsync();
		var now = DateTime.UtcNow.Date;
		var currentReservations = await AsyncExecuter.CountAsync(
			reservationQuery.Where(r => r.MemberId == input.MemberId && r.ReserveDueDate.Date >= now));

		if (currentReservations >= rule.ReservationLimit)
		{
			throw LibraryException(LibraryErrorCodes.ReservationLimitExceeded, "Member has reached the active reservation limit.");
		}

		var duplicateReservation = await AsyncExecuter.AnyAsync(
			reservationQuery.Where(r =>
				r.BookId == bookId &&
				r.MemberId == input.MemberId &&
				r.ReserveDueDate.Date >= now));

		if (duplicateReservation)
		{
			throw LibraryException(LibraryErrorCodes.DuplicateReservation, "Member already has an active reservation for this book.");
		}

		var reservation = new BookReservation(
			GuidGenerator.Create(),
			bookId,
			input.MemberId,
			input.RequestedDate,
			input.ReserveDueDate,
			CurrentTenant.Id);

		await reservationRepository.InsertAsync(reservation, autoSave: true);
		await notificationReminderService.SendReservationConfirmedAsync(reservation, book);

		return new GenericCreatedResponse
		{
			Id = reservation.Id.ToString(),
			Message = "Book reservation created successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)]
	public async Task DeleteReservationAsync(Guid bookId, Guid reservationId)
	{
		var reservation = await reservationRepository.FindAsync(x => x.Id == reservationId && x.BookId == bookId);
		if (reservation == null)
		{
			throw LibraryException(LibraryErrorCodes.ReservationNotFound, "Book reservation was not found for the selected book.");
		}

		await reservationRepository.DeleteAsync(reservation, autoSave: true);
	}

	private Guid EnsureAcademicYear(Guid? academicYearId)
	{
		if (!academicYearId.HasValue || academicYearId.Value == Guid.Empty)
		{
			throw LibraryException(LibraryErrorCodes.AcademicYearRequired, "Academic year is required for Library records.");
		}

		return academicYearId.Value;
	}

	private async Task EnsureCategoryAsync(Guid categoryId, Guid academicYearId)
	{
		var category = await categoryRepository.FindAsync(categoryId);
		if (category == null || category.AcademicYearId != academicYearId)
		{
			throw LibraryException(LibraryErrorCodes.BookCategoryNotFound, "Book category was not found for the selected academic year.");
		}
	}

	private async Task EnsureBookIsUniqueAsync(string isbn, string edition, Guid academicYearId, Guid? ignoredBookId)
	{
		var normalizedIsbn = isbn.Trim().ToLower();
		var normalizedEdition = (edition ?? string.Empty).Trim().ToLower();
		var query = await bookRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			(!ignoredBookId.HasValue || x.Id != ignoredBookId.Value) &&
			x.Isbn.ToLower() == normalizedIsbn &&
			(x.Edition ?? string.Empty).ToLower() == normalizedEdition));

		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.BookAlreadyExists, "A book with the same ISBN and edition already exists.");
		}
	}

	private async Task EnsureCopyPayloadIsValidAsync(Guid bookId, IReadOnlyCollection<BookCopyItem> copies, bool allowSameBookAccession)
	{
		if (copies.Any(x =>
			x.CopyNumber <= 0 ||
			string.IsNullOrWhiteSpace(x.AccessionNumber) ||
			x.SectionId == Guid.Empty ||
			x.ShelfId == Guid.Empty))
		{
			throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, "Every copy requires a valid copy number, accession number, section, and shelf.");
		}

		var duplicateAccession = copies
			.GroupBy(x => x.AccessionNumber, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(x => x.Count() > 1);

		if (duplicateAccession != null)
		{
			throw LibraryException(LibraryErrorCodes.DuplicateAccessionNumber, $"Duplicate accession number '{duplicateAccession.Key}' in copy payload.");
		}

		var duplicateCopyNumber = copies
			.GroupBy(x => x.CopyNumber)
			.FirstOrDefault(x => x.Count() > 1);

		if (duplicateCopyNumber != null)
		{
			throw LibraryException(LibraryErrorCodes.DuplicateAccessionNumber, $"Duplicate copy number '{duplicateCopyNumber.Key}' in copy payload.");
		}

		var accessionNumbers = copies.Select(x => x.AccessionNumber).ToList();
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var existingCopies = await AsyncExecuter.ToListAsync(copyQuery.Where(x => accessionNumbers.Contains(x.AccessionNumber)));

		var conflictingCopy = existingCopies.FirstOrDefault(x => !allowSameBookAccession || x.BookId != bookId);
		if (conflictingCopy != null)
		{
			throw LibraryException(LibraryErrorCodes.AccessionNumberAlreadyExists, $"Accession number '{conflictingCopy.AccessionNumber}' already exists.");
		}

		await EnsureSectionsAndShelvesAsync(copies);
	}

	private async Task EnsureSectionsAndShelvesAsync(IReadOnlyCollection<BookCopyItem> copies)
	{
		var sectionIds = copies.Select(x => x.SectionId).Distinct().ToList();
		var shelfIds = copies.Select(x => x.ShelfId).Distinct().ToList();

		var sectionQuery = await sectionRepository.GetQueryableAsync();
		var sections = await AsyncExecuter.ToListAsync(sectionQuery.Where(x => sectionIds.Contains(x.Id)));
		if (sections.Count != sectionIds.Count)
		{
			throw LibraryException(LibraryErrorCodes.SectionNotFound, "One or more selected library sections were not found.");
		}

		var shelfQuery = await shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(shelfQuery.Where(x => shelfIds.Contains(x.Id)));
		if (shelves.Count != shelfIds.Count)
		{
			throw LibraryException(LibraryErrorCodes.ShelfNotFound, "One or more selected library shelves were not found.");
		}

		var shelfLookup = shelves.ToDictionary(x => x.Id);
		foreach (var copy in copies)
		{
			if (!shelfLookup.TryGetValue(copy.ShelfId, out var shelf) || shelf.SectionId != copy.SectionId)
			{
				throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, "Selected shelf does not belong to the selected section.");
			}
		}
	}

	private async Task EnsureNoActiveIssuesForBookAsync(Guid bookId)
	{
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var accessionNumbers = await AsyncExecuter.ToListAsync(
			copyQuery.Where(x => x.BookId == bookId).Select(x => x.AccessionNumber));

		if (accessionNumbers.Count == 0)
		{
			return;
		}

		var issueQuery = await issueRepository.GetQueryableAsync();
		var hasActiveIssue = await AsyncExecuter.AnyAsync(
			issueQuery.Where(x => accessionNumbers.Contains(x.AccessionNumber) && x.Status == BookIssueStatus.Issued));

		if (hasActiveIssue)
		{
			throw LibraryException(LibraryErrorCodes.BookHasActiveIssues, "Book copies cannot be changed while active issues exist.");
		}
	}

	private async Task<BookCopy?> FindCopyAsync(Guid bookId, string accessionNumber)
	{
		var normalizedAccession = NormalizeAccession(accessionNumber);
		var query = await bookCopyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.BookId == bookId && x.AccessionNumber == normalizedAccession));
	}

	private async Task EnsureStatusTransitionAsync(BookCopy copy, BookCopyStatus targetStatus)
	{
		var issueQuery = await issueRepository.GetQueryableAsync();
		var hasActiveIssue = await AsyncExecuter.AnyAsync(
			issueQuery.Where(x => x.AccessionNumber == copy.AccessionNumber && x.Status == BookIssueStatus.Issued));

		if (hasActiveIssue && targetStatus != BookCopyStatus.Issued)
		{
			throw LibraryException(LibraryErrorCodes.InvalidCopyStatusTransition, "Issued copies cannot move to another status until returned.");
		}

		if (!hasActiveIssue && targetStatus == BookCopyStatus.Issued)
		{
			throw LibraryException(LibraryErrorCodes.InvalidCopyStatusTransition, "A copy can be marked issued only through the issue workflow.");
		}
	}

	private async Task<(int MaxBooksAtTime, int ReservationLimit)> GetBorrowingRuleAsync(Guid? academicYearId, int memberRole)
	{
		if (!academicYearId.HasValue)
		{
			return memberRole == 0 ? (3, DefaultStudentReservationLimit) : (5, DefaultStaffReservationLimit);
		}

		var query = await borrowingRuleRepository.GetQueryableAsync();
		var rule = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.AcademicYearId == academicYearId.Value && x.Role == memberRole));

		if (rule == null)
		{
			return memberRole == 0 ? (3, DefaultStudentReservationLimit) : (5, DefaultStaffReservationLimit);
		}

		return (rule.MaxBooksAtTime, rule.ReservationLimit);
	}

	private async Task<int> ResolveActiveMemberRoleAsync(Guid memberId)
	{
		var student = await studentRepository.FindAsync(memberId);
		if (student != null)
		{
			var libraryMember = await libraryMemberRepository.FindAsync(x => x.StudentId == student.Id);
			if (student.AdditionalDetail?.IsLibraryMember == true && libraryMember?.IsActive != false)
			{
				return 0;
			}

			throw LibraryException(LibraryErrorCodes.MemberInactive, "Student is not an active library member.");
		}

		var employee = await employeeRepository.FindAsync(memberId);
		if (employee != null)
		{
			if (employee.AdditionalDetails?.LibraryMember == true)
			{
				return 1;
			}

			throw LibraryException(LibraryErrorCodes.MemberInactive, "Staff member is not an active library member.");
		}

		throw LibraryException(LibraryErrorCodes.MemberNotFound, "Library member was not found.");
	}

	private static List<BookCopyItem> NormalizeCopyItems(IEnumerable<BookCopyItem>? copies)
	{
		return copies?
			.Select(x => new BookCopyItem
			{
				CopyNumber = x.CopyNumber,
				AccessionNumber = NormalizeAccession(x.AccessionNumber),
				SectionId = x.SectionId,
				ShelfId = x.ShelfId
			})
			.ToList() ?? new List<BookCopyItem>();
	}

	private static string NormalizeAccession(string accessionNumber)
	{
		return (accessionNumber ?? string.Empty).Trim();
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
