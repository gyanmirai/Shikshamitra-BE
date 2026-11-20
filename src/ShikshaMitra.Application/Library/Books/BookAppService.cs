using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
[RemoteService(false)]
public partial class BookAppService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	LibraryCopyRetentionReader copyRetention,
	LibraryCatalogueRevisionReader catalogueRevisions,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository,
	IRepository<LibraryShelf, Guid> shelfRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<BorrowingRule, Guid> borrowingRuleRepository,
	IRepository<Members.LibraryMember, Guid> libraryMemberRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	ILibraryInventoryLock inventoryLock,
	LibraryCatalogueConfigurationReader catalogueConfiguration,
	LibraryCatalogueLabelReader catalogueLabels,
	ILibraryAccessService libraryAccess,
	LibraryCopyLifecycleAppService copyLifecycle,
	IPermissionChecker permissions,
	LibraryReservationAppService reservationLifecycle,
	LibraryReservationCoordinator reservationCoordinator)
	: ApplicationService, IBookAppService
{
	private const int DefaultStudentReservationLimit = 2;
	private const int DefaultStaffReservationLimit = 3;

	public async Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? alphabet = null)
	{
		await libraryAccess.GetContextAsync(academicYearId);
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await bookRepository.GetQueryableAsync();

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
		var bookIds = items.Select(x => x.Id).ToList();
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var copyCounts = await AsyncExecuter.ToListAsync(copyQuery
			.Where(x => bookIds.Contains(x.BookId))
			.GroupBy(x => x.BookId)
			.Select(group => new { BookId = group.Key, Count = group.Count() }));
		var countsByBook = copyCounts.ToDictionary(x => x.BookId, x => x.Count);
		var bookDtos = ObjectMapper.Map<List<Book>, List<BookDto>>(items);
		var labels = await catalogueLabels.ReadAsync();
		var booksById = items.ToDictionary(book => book.Id);
		foreach (var bookDto in bookDtos)
		{
			bookDto.NumberOfCopies = countsByBook.GetValueOrDefault(bookDto.Id);
			var source = booksById[bookDto.Id];
			bookDto.LanguageCode = labels.LanguageCode(source);
			bookDto.LanguageLabel = labels.LanguageLabel(source);
			bookDto.MaterialCode = source.MaterialCode;
			bookDto.MaterialLabel = labels.MaterialLabel(source);
			bookDto.PriceText = LibraryCataloguePriceRules.Format(source.Price);
		}

		return new PaginatedResponse<BookDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = bookDtos
		};
	}

	public async Task<BookDetailDto> GetAsync(Guid bookId)
	{
		await libraryAccess.GetContextAsync();
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
		var retention = await copyRetention.ReadAsync(copies);
		var canUpdateCopies = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		var canDelete = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete);
		var deletionBlockedReason = canDelete ? await TitleRemovalBlockedReasonAsync(book.Id, retention)
			: "You do not have permission to delete catalogue titles.";
		var labels = await catalogueLabels.ReadAsync();

		return new BookDetailDto
		{
			MetadataRevision = LibraryCatalogueRevisionReader.Metadata(book),
			CopiesRevision = LibraryCatalogueRevisionReader.Copies(book, copies, retention),
			CanDelete = canDelete && deletionBlockedReason == null,
			DeletionBlockedReason = deletionBlockedReason,
			Id = book.Id,
			Title = book.Title,
			Author = book.Author,
			Publisher = book.Publisher,
			Subject = book.Subject,
			Isbn = book.Isbn,
			Language = (int?)book.Language,
			LanguageCode = labels.LanguageCode(book),
			LanguageLabel = labels.LanguageLabel(book),
			MaterialCode = book.MaterialCode,
			MaterialLabel = labels.MaterialLabel(book),
			Edition = book.Edition,
			Category = category != null
				? new NamedIdDto { Id = category.Id, Name = category.Name }
				: new NamedIdDto { Id = book.CategoryId, Name = string.Empty },
			ApplicableAge = book.ApplicableAge,
			Pages = book.Pages,
			Price = book.Price,
			PriceText = LibraryCataloguePriceRules.Format(book.Price),
			CoverImage = book.CoverImage,
			AddedOn = book.CreationTime,
			Copies = copies.Select(c => new BookDetailCopyDto
			{
				CopyId = c.Id,
				ReceivingReceiptId = retention[c.Id].ReceiptId,
				CanRemove = canUpdateCopies && retention[c.Id].BlockedReason == null,
				RemovalBlockedReason = retention[c.Id].BlockedReason ?? (canUpdateCopies ? null : "You do not have permission to remove catalogue copies."),
				CopyNumber = c.CopyNumber,
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
	public async Task<LibraryBookCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var resolvedAcademicYearId = EnsureAcademicYear(academicYearId);
		await libraryAccess.GetContextAsync(resolvedAcademicYearId);
		var catalogue = await catalogueConfiguration.ForWriteAsync(input.ExpectedCatalogueConfigurationRevision);
		await EnsureCategoryAsync(input.CategoryId);
		await EnsureBookIsUniqueAsync(input.Isbn, input.Edition, ignoredBookId: null);

		var copies = NormalizeCopyItems(input.Copies);
		if (copies.Any(x => x.CopyId.HasValue))
		{
			throw LibraryException(LibraryErrorCodes.InvalidCopyIdentity, "A new book cannot contain an existing copy identity.");
		}
		await AllocateAccessionsAsync(copies);
		if (copies.Count > 0)
		{
			await EnsureCopyPayloadIsValidAsync(Guid.Empty, copies, allowSameBookAccession: false);
		}

		var entity = new Book(
			input.Title.Trim(),
			NormalizeIsbn(input.Isbn),
			input.Author?.Trim() ?? string.Empty,
			input.Publisher?.Trim() ?? string.Empty,
			input.Edition?.Trim() ?? string.Empty,
			input.Subject?.Trim() ?? string.Empty,
			input.CategoryId,
			input.Language,
			input.ApplicableAge?.Trim() ?? string.Empty,
			LibraryCataloguePriceRules.Resolve(input.Price, input.PriceText),
			input.Pages,
			copies.Count,
			resolvedAcademicYearId,
			CurrentTenant.Id)
		{
			CoverImage = input.CoverImage
		};
		entity.InitializeCatalogueMetadata(catalogue, new(input.Author, NormalizeIsbn(input.Isbn), input.Publisher,
			input.Edition, input.Subject, input.ApplicableAge, input.Language, input.LanguageCode, input.MaterialCode));

		await bookRepository.InsertAsync(entity, autoSave: true);

		if (copies.Count > 0)
		{
			var bookCopies = copies.Select(c => new BookCopy(
				GuidGenerator.Create(),
				entity.Id,
				c.CopyNumber,
				c.AccessionNumber!,
				c.SectionId,
				c.ShelfId,
				CurrentTenant.Id)).ToList();

			await bookCopyRepository.InsertManyAsync(bookCopies, autoSave: true);
		}

		return new LibraryBookCreatedResponse
		{
			Id = entity.Id.ToString(),
			MetadataRevision = LibraryCatalogueRevisionReader.Metadata(entity),
			CopiesRevision = await catalogueRevisions.CopiesAsync(entity),
			Message = "Book created successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryBookUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input)
	{
		await libraryAccess.GetContextAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var entity = await bookRepository.GetAsync(bookId);
		var catalogue = await catalogueConfiguration.ForWriteAsync(input.ExpectedCatalogueConfigurationRevision);
		LibraryCatalogueRevisionReader.RequireMetadata(entity, input.ExpectedMetadataRevision);
		if (input.Copies is { Count: > 0 }) await catalogueRevisions.RequireCopiesAsync(entity, input.ExpectedCopiesRevision);
		await EnsureCategoryAsync(input.CategoryId);
		if (NormalizeIsbn(entity.Isbn) != NormalizeIsbn(input.Isbn) ||
			!string.Equals(entity.Edition?.Trim(), input.Edition?.Trim(), StringComparison.OrdinalIgnoreCase))
			await EnsureBookIsUniqueAsync(input.Isbn, input.Edition, bookId);

		entity.ReviseCatalogueMetadata(catalogue, new(input.Author, NormalizeIsbn(input.Isbn), input.Publisher,
			input.Edition, input.Subject, input.ApplicableAge, input.Language, input.LanguageCode, input.MaterialCode));
		entity.Title = input.Title.Trim();
		entity.CategoryId = input.CategoryId;
		entity.Price = LibraryCataloguePriceRules.ResolveUpdate(entity.Price, input.Price, input.PriceText);
		entity.Pages = input.Pages;
		entity.NumberOfCopies = await bookCopyRepository.CountAsync(x => x.BookId == bookId);
		entity.CoverImage = input.CoverImage;

		await bookRepository.UpdateAsync(entity, autoSave: true);

		var copies = NormalizeCopyItems(input.Copies);
		if (copies.Count > 0)
		{
			await PutCopiesAsync(bookId, new PutBookCopiesRequest { Copies = copies, ExpectedCopiesRevision = input.ExpectedCopiesRevision });
		}

		return await CatalogueUpdatedResponseAsync(entity, "Book updated successfully.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)]
	public async Task DeleteAsync(Guid bookId)
	{
		await libraryAccess.GetContextAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await bookRepository.GetAsync(bookId);
		var copies = await bookCopyRepository.GetListAsync(x => x.BookId == bookId);
		var blockedReason = await TitleRemovalBlockedReasonAsync(bookId, await copyRetention.ReadAsync(copies));
		if (blockedReason != null) throw LibraryException(LibraryErrorCodes.CopyHasHistory, blockedReason);
		await bookRepository.DeleteAsync(bookId, autoSave: true);
	}

	public async Task<PaginatedResponse<BookCopyDto>> GetCopiesAsync(Guid bookId, int page = 1, int pageSize = 20)
	{
		await libraryAccess.GetContextAsync();
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
		var retention = await copyRetention.ReadAsync(copies);
		var canUpdateCopies = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);

		return new PaginatedResponse<BookCopyDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = copies.Select(x => new BookCopyDto
			{
				Id = x.Id,
				ReceivingReceiptId = retention[x.Id].ReceiptId,
				CanRemove = canUpdateCopies && retention[x.Id].BlockedReason == null,
				RemovalBlockedReason = retention[x.Id].BlockedReason ?? (canUpdateCopies ? null : "You do not have permission to remove catalogue copies."),
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
		await libraryAccess.GetContextAsync();
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
				BarcodeValue = LibraryCopyScanCode.For(copy)
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryBookUpdatedResponse> PutCopiesAsync(Guid bookId, PutBookCopiesRequest input)
	{
		await libraryAccess.GetContextAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var book = await bookRepository.GetAsync(bookId);
		await catalogueRevisions.RequireCopiesAsync(book, input.ExpectedCopiesRevision);
		var copies = NormalizeCopyItems(input.Copies);
		await ReconcileCopiesAsync(bookId, copies);
		book.NumberOfCopies = copies.Count;
		await bookRepository.UpdateAsync(book, autoSave: true);
		await reservationCoordinator.RebalanceAsync(bookId);

		return await CatalogueUpdatedResponseAsync(book, "Book copies updated successfully.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<GenericUpdatedResponse> UpdateCopyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input)
	{
		var copy = await copyLifecycle.UpdateLegacyStatusAsync(bookId, accessionNumber, input);

		return new GenericUpdatedResponse
		{
			Id = copy.CopyId.ToString(),
			Message = "Book copy status updated successfully."
		};
	}

	public Task<PaginatedResponse<BookReservationDto>> GetReservationsAsync(Guid bookId, int page = 1, int pageSize = 20) =>
		reservationLifecycle.GetLegacyListAsync(bookId, page, pageSize);

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)]
	public Task<GenericCreatedResponse> ReserveAsync(Guid bookId, CreateBookReservationRequest input) =>
		reservationLifecycle.CreateLegacyAsync(bookId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)]
	public Task DeleteReservationAsync(Guid bookId, Guid reservationId) =>
		reservationLifecycle.CancelLegacyAsync(bookId, reservationId);

	private Guid EnsureAcademicYear(Guid? academicYearId)
	{
		if (!academicYearId.HasValue || academicYearId.Value == Guid.Empty)
		{
			throw LibraryException(LibraryErrorCodes.AcademicYearRequired, "Academic year is required for Library records.");
		}

		return academicYearId.Value;
	}

	private async Task EnsureCategoryAsync(Guid categoryId)
	{
		var category = await categoryRepository.FindAsync(categoryId);
		if (category == null)
		{
			throw LibraryException(LibraryErrorCodes.BookCategoryNotFound, "Book category was not found for the current school.");
		}
	}

	private async Task EnsureBookIsUniqueAsync(string? isbn, string? edition, Guid? ignoredBookId)
	{
		var normalizedIsbn = NormalizeIsbn(isbn);
		if (normalizedIsbn.Length == 0) return;
		var normalizedEdition = (edition ?? string.Empty).Trim().ToLower();
		var query = await bookRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			(!ignoredBookId.HasValue || x.Id != ignoredBookId.Value) &&
			x.Isbn.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpper() == normalizedIsbn &&
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

		var accessionNumbers = copies.Select(x => x.AccessionNumber!.ToUpperInvariant()).ToList();
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var existingCopies = await AsyncExecuter.ToListAsync(copyQuery.Where(x => accessionNumbers.Contains(x.NormalizedAccessionNumber)));

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

	private async Task<BookCopy?> FindCopyAsync(Guid bookId, string accessionNumber)
	{
		await bookRepository.GetAsync(bookId);
		var normalizedAccession = NormalizeAccession(accessionNumber).ToUpperInvariant();
		var query = await bookCopyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.BookId == bookId && x.NormalizedAccessionNumber == normalizedAccession));
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
				CopyId = x.CopyId,
				CopyNumber = x.CopyNumber,
				AccessionNumber = NormalizeAccession(x.AccessionNumber),
				SectionId = x.SectionId,
				ShelfId = x.ShelfId
			})
			.ToList() ?? new List<BookCopyItem>();
	}

	private static string NormalizeAccession(string? accessionNumber)
	{
		return (accessionNumber ?? string.Empty).Trim();
	}

	internal static string NormalizeIsbn(string? isbn)
	{
		return new string((isbn ?? string.Empty).Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray()).ToUpperInvariant();
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
