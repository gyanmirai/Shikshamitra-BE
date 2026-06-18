using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.IssueReturn;

[RemoteService(false)]
public class BookIssueAppService(
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<LibraryCharge, Guid> libraryChargeRepository,
	IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
	IRepository<BorrowingRule, Guid> borrowingRuleRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Members.LibraryMember, Guid> libraryMemberRepository,
	LibraryNotificationReminderService notificationReminderService)
	: ApplicationService, IBookIssueAppService
{
	private const int DefaultStudentBorrowingLimit = 3;
	private const int DefaultStaffBorrowingLimit = 5;
	private const int DefaultStudentReservationLimit = 2;
	private const int DefaultStaffReservationLimit = 3;

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	public async Task<GenericCreatedResponse> IssueAsync(IssueBookRequest input)
	{
		EnsureIssueDates(input.IssueDate, input.DueDate);

		var copy = await FindCopyByAccessionAsync(input.AccessionNo);
		if (copy == null)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotFound, $"Book copy with accession number '{input.AccessionNo}' was not found.");
		}

		if (copy.Status != BookCopyStatus.Available && copy.Status != BookCopyStatus.Reserved)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotAvailable, $"Book copy '{input.AccessionNo}' is not available for issue. Current status: {copy.Status}.");
		}

		var book = await bookRepository.GetAsync(copy.BookId);
		var memberRole = await ResolveActiveMemberRoleAsync(input.MemberId);
		var rule = await GetBorrowingRuleAsync(book.AcademicYearId, memberRole);

		var issueQuery = await issueRepository.GetQueryableAsync();
		var duplicateIssue = await AsyncExecuter.AnyAsync(issueQuery.Where(i =>
			i.AccessionNumber == copy.AccessionNumber &&
			i.Status == BookIssueStatus.Issued));

		if (duplicateIssue)
		{
			throw LibraryException(LibraryErrorCodes.DuplicateActiveIssue, "This copy already has an active issue.");
		}

		var activeIssuesForMember = await AsyncExecuter.CountAsync(
			issueQuery.Where(i => i.MemberId == input.MemberId && i.Status == BookIssueStatus.Issued));

		if (activeIssuesForMember >= rule.MaxBooksAtTime)
		{
			throw LibraryException(LibraryErrorCodes.BorrowingLimitExceeded, "Member has reached the maximum active issue limit.");
		}

		var reservationToConsume = await ResolveReservationForIssueAsync(book.Id, input.MemberId, copy.Status);

		var issue = new BookIssue(
			GuidGenerator.Create(),
			copy.Id,
			copy.AccessionNumber,
			input.MemberId,
			memberRole,
			input.IssueDate,
			input.DueDate,
			CurrentTenant.Id);

		await issueRepository.InsertAsync(issue, autoSave: true);

		copy.Status = BookCopyStatus.Issued;
		await bookCopyRepository.UpdateAsync(copy, autoSave: true);

		if (reservationToConsume != null)
		{
			await reservationRepository.DeleteAsync(reservationToConsume, autoSave: true);
		}

		await notificationReminderService.SendIssueConfirmationAsync(issue, book, copy);

		return new GenericCreatedResponse
		{
			Id = issue.Id.ToString(),
			Message = "Book issued successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	public async Task<GenericCreatedResponse> ReturnAsync(ReturnBookRequest input)
	{
		var normalizedAccession = NormalizeAccession(input.AccessionNo);
		var issueQuery = await issueRepository.GetQueryableAsync();
		var issue = await AsyncExecuter.FirstOrDefaultAsync(issueQuery.Where(i =>
			i.AccessionNumber == normalizedAccession &&
			i.MemberId == input.MemberId &&
			i.Status == BookIssueStatus.Issued));

		if (issue == null)
		{
			throw LibraryException(LibraryErrorCodes.ActiveIssueNotFound, $"No active issue found for accession '{input.AccessionNo}' and the specified member.");
		}

		if (input.ReturnDate.Date < issue.IssueDate.Date)
		{
			throw LibraryException(LibraryErrorCodes.InvalidReturnDate, "Return date cannot be before issue date.");
		}

		var finalCopyStatus = input.CopyStatus ?? BookCopyStatus.Available;
		if (finalCopyStatus == BookCopyStatus.Issued || finalCopyStatus == BookCopyStatus.Reserved)
		{
			throw LibraryException(LibraryErrorCodes.InvalidCopyStatusTransition, "Return workflow can only set a copy to available, damaged, lost, under repair, or discarded.");
		}

		issue.ReturnDate = input.ReturnDate;
		issue.Status = BookIssueStatus.Returned;
		issue.ChargeCategoryId = input.ChargeCategoryId;
		issue.Amount = input.Amount;
		issue.FineDueDate = input.FineDueDate;
		issue.Remarks = input.Remarks;

		await issueRepository.UpdateAsync(issue, autoSave: true);

		var copy = await FindCopyByAccessionAsync(input.AccessionNo);
		if (copy != null)
		{
			copy.Status = finalCopyStatus;
			copy.Remarks = input.Remarks;
			await bookCopyRepository.UpdateAsync(copy, autoSave: true);
		}

		if (input.Amount.HasValue && input.Amount.Value > 0)
		{
			await CreateReturnChargeAsync(issue, input);
		}

		Book? returnedBook = null;
		if (copy != null)
		{
			returnedBook = await bookRepository.FindAsync(copy.BookId);
		}
		await notificationReminderService.SendReturnNotificationsAsync(issue, returnedBook, copy);

		return new GenericCreatedResponse
		{
			Id = issue.Id.ToString(),
			Message = "Book returned successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	public async Task<AccessionBookDto> GetByAccessionAsync(string accessionNo)
	{
		var copy = await FindCopyByAccessionAsync(accessionNo);
		if (copy == null)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotFound, $"Book copy with accession number '{accessionNo}' was not found.");
		}

		var book = await bookRepository.GetAsync(copy.BookId);
		var category = await categoryRepository.FindAsync(book.CategoryId);

		return new AccessionBookDto
		{
			BookId = book.Id,
			Title = book.Title,
			AccessionNo = copy.AccessionNumber,
			Author = book.Author,
			Isbn = book.Isbn,
			Publisher = book.Publisher,
			Edition = book.Edition,
			AddedOn = book.CreationTime,
			Language = book.Language.ToString(),
			Category = category?.Name ?? string.Empty,
			Pages = book.Pages
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)]
	public async Task<LibraryMemberDto> GetMemberAsync(Guid memberId)
	{
		var student = await studentRepository.FindAsync(memberId);
		if (student != null)
		{
			return await BuildStudentMemberDtoAsync(student);
		}

		var employee = await employeeRepository.FindAsync(memberId);
		if (employee != null)
		{
			return await BuildEmployeeMemberDtoAsync(employee);
		}

		throw LibraryException(LibraryErrorCodes.MemberNotFound, $"Member with id '{memberId}' was not found.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	public async Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(int page = 1, int pageSize = 20)
	{
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await issueRepository.GetQueryableAsync();
		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

		var issues = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		var memberIds = issues.Select(i => i.MemberId).Distinct().ToList();
		var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();

		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var copies = await AsyncExecuter.ToListAsync(copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
		var bookIds = copies.Select(c => c.BookId).Distinct().ToList();

		var bookQuery = await bookRepository.GetQueryableAsync();
		var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
		var bookLookup = books.ToDictionary(b => b.Id);
		var copyBookLookup = copies.ToDictionary(c => c.AccessionNumber, c => c.BookId);

		var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
		var catQuery = await categoryRepository.GetQueryableAsync();
		var categories = await AsyncExecuter.ToListAsync(catQuery.Where(c => catIds.Contains(c.Id)));
		var catLookup = categories.ToDictionary(c => c.Id, c => c.Name);

		var studentQuery = await studentRepository.GetQueryableAsync();
		var students = await AsyncExecuter.ToListAsync(studentQuery.Where(s => memberIds.Contains(s.Id)));
		var studentLookup = students.ToDictionary(s => s.Id);

		var empQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(empQuery.Where(e => memberIds.Contains(e.Id)));
		var empLookup = employees.ToDictionary(e => e.Id);

		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
		var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

		var stdQuery = await standardRepository.GetQueryableAsync();
		var standards = await AsyncExecuter.ToListAsync(stdQuery.Where(s => standardIds.Contains(s.Id)));
		var stdLookup = standards.ToDictionary(s => s.Id, s => s.Name);

		var divQuery = await divisionRepository.GetQueryableAsync();
		var divisions = await AsyncExecuter.ToListAsync(divQuery.Where(d => divisionIds.Contains(d.Id)));
		var divLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
		var today = DateTime.UtcNow.Date;

		var items = issues.Select(issue =>
		{
			var dto = new IssueReturnItemDto
			{
				TransactionId = issue.Id,
				MemberId = issue.MemberId,
				Role = issue.MemberRole,
				AccessionNo = issue.AccessionNumber,
				IssueDate = issue.IssueDate,
				DueDate = issue.DueDate,
				ReturnDate = issue.ReturnDate,
				Status = issue.Status == BookIssueStatus.Issued && issue.DueDate.Date < today
					? (int)BookIssueStatus.Overdue
					: (int)issue.Status
			};

			if (copyBookLookup.TryGetValue(issue.AccessionNumber, out var bookId) &&
				bookLookup.TryGetValue(bookId, out var book))
			{
				dto.BookTitle = book.Title;
				dto.BookCategory = catLookup.GetValueOrDefault(book.CategoryId);
			}

			if (studentLookup.TryGetValue(issue.MemberId, out var student))
			{
				dto.MemberName = $"{student.FirstName} {student.LastName}".Trim();
				dto.RollNo = student.RollNo?.ToString();
				dto.GrNo = student.GrNo?.ToString();
				dto.StandardName = student.StandardId.HasValue ? stdLookup.GetValueOrDefault(student.StandardId.Value) : null;
				dto.DivisionName = student.DivisionId.HasValue ? divLookup.GetValueOrDefault(student.DivisionId.Value) : null;
			}
			else if (empLookup.TryGetValue(issue.MemberId, out var emp))
			{
				dto.MemberName = $"{emp.FirstName} {emp.LastName}".Trim();
				dto.ContactNo = emp.MobileNo;
			}

			return dto;
		}).ToList();

		return new PaginatedResponse<IssueReturnItemDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	public async Task<ReturnBookSearchDto> SearchReturnBookAsync(string accessionNo)
	{
		var copy = await FindCopyByAccessionAsync(accessionNo);
		if (copy == null)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotFound, $"Book copy with accession number '{accessionNo}' was not found.");
		}

		var issueQuery = await issueRepository.GetQueryableAsync();
		var issue = await AsyncExecuter.FirstOrDefaultAsync(issueQuery.Where(i =>
			i.AccessionNumber == copy.AccessionNumber &&
			i.Status == BookIssueStatus.Issued));

		if (issue == null)
		{
			throw LibraryException(LibraryErrorCodes.ActiveIssueNotFound, $"No active issue found for accession number '{accessionNo}'.");
		}

		var book = await bookRepository.GetAsync(copy.BookId);
		var category = await categoryRepository.FindAsync(book.CategoryId);

		var memberDetails = await BuildReturnMemberDetailsAsync(issue);

		return new ReturnBookSearchDto
		{
			BookDetails = new ReturnBookSearchBookDto
			{
				Id = book.Id,
				AccessionNo = copy.AccessionNumber,
				BookTitle = book.Title,
				Author = book.Author,
				Isbn = book.Isbn,
				Publisher = book.Publisher,
				AddedOn = book.CreationTime,
				Language = (int)book.Language,
				Edition = book.Edition,
				Category = category?.Name ?? string.Empty,
				Pages = book.Pages,
				CoverImage = book.CoverImage
			},
			MemberDetails = memberDetails
		};
	}

	private async Task<BookReservation?> ResolveReservationForIssueAsync(Guid bookId, Guid memberId, BookCopyStatus copyStatus)
	{
		var reservationQuery = await reservationRepository.GetQueryableAsync();
		var today = DateTime.UtcNow.Date;
		var activeReservations = await AsyncExecuter.ToListAsync(
			reservationQuery
				.Where(r => r.BookId == bookId && r.ReserveDueDate.Date >= today)
				.OrderBy(r => r.RequestedDate));

		var reservationForMember = activeReservations.FirstOrDefault(r => r.MemberId == memberId);

		if (copyStatus == BookCopyStatus.Reserved && reservationForMember == null)
		{
			throw LibraryException(LibraryErrorCodes.ReservationNotFound, "Reserved copies can only be issued to the member with an active reservation.");
		}

		if (copyStatus == BookCopyStatus.Available && activeReservations.Count > 0 && reservationForMember == null)
		{
			throw LibraryException(LibraryErrorCodes.ReservationNotFound, "This book has an active reservation for another member.");
		}

		return reservationForMember;
	}

	private async Task CreateReturnChargeAsync(BookIssue issue, ReturnBookRequest input)
	{
		if (!input.ChargeCategoryId.HasValue || input.ChargeCategoryId.Value == Guid.Empty)
		{
			throw LibraryException(LibraryErrorCodes.InvalidCharge, "Charge category is required when a return charge amount is provided.");
		}

		var category = await chargeCategoryRepository.FindAsync(input.ChargeCategoryId.Value);
		if (category == null)
		{
			throw LibraryException(LibraryErrorCodes.ChargeCategoryNotFound, "Charge category was not found.");
		}

		var charge = new LibraryCharge(
			GuidGenerator.Create(),
			issue.MemberId,
			issue.MemberRole,
			input.ChargeCategoryId.Value,
			issue.Id,
			input.Amount!.Value,
			input.ReturnDate,
			input.FineDueDate ?? input.ReturnDate.AddDays(7),
			CurrentTenant.Id)
		{
			Remarks = input.Remarks
		};

		await libraryChargeRepository.InsertAsync(charge, autoSave: true);
	}

	private async Task<BookCopy?> FindCopyByAccessionAsync(string accessionNo)
	{
		var normalizedAccession = NormalizeAccession(accessionNo);
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(copyQuery.Where(c => c.AccessionNumber == normalizedAccession));
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

		throw LibraryException(LibraryErrorCodes.MemberNotFound, $"Member with id '{memberId}' was not found as student or employee.");
	}

	private async Task<(int MaxBooksAtTime, int ReservationLimit)> GetBorrowingRuleAsync(Guid? academicYearId, int memberRole)
	{
		if (!academicYearId.HasValue)
		{
			return memberRole == 0
				? (DefaultStudentBorrowingLimit, DefaultStudentReservationLimit)
				: (DefaultStaffBorrowingLimit, DefaultStaffReservationLimit);
		}

		var query = await borrowingRuleRepository.GetQueryableAsync();
		var rule = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.AcademicYearId == academicYearId.Value && x.Role == memberRole));

		if (rule == null)
		{
			return memberRole == 0
				? (DefaultStudentBorrowingLimit, DefaultStudentReservationLimit)
				: (DefaultStaffBorrowingLimit, DefaultStaffReservationLimit);
		}

		return (rule.MaxBooksAtTime, rule.ReservationLimit);
	}

	private async Task<LibraryMemberDto> BuildStudentMemberDtoAsync(Student student)
	{
		var classDivision = await ResolveStudentClassDivisionAsync(student);

		var issueQuery = await issueRepository.GetQueryableAsync();
		var currentIssue = await AsyncExecuter.CountAsync(issueQuery.Where(i => i.MemberId == student.Id && i.Status == BookIssueStatus.Issued));
		var overdue = await AsyncExecuter.CountAsync(issueQuery.Where(i => i.MemberId == student.Id && i.Status == BookIssueStatus.Issued && i.DueDate < DateTime.UtcNow));

		var resQuery = await reservationRepository.GetQueryableAsync();
		var reserved = await AsyncExecuter.CountAsync(resQuery.Where(r => r.MemberId == student.Id));

		var libMember = await libraryMemberRepository.FindAsync(x => x.StudentId == student.Id);

		return new LibraryMemberDto
		{
			Id = student.Id,
			MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			MemberIdentifier = libMember?.MemberIdentifier,
			Avatar = student.AdditionalDetail?.ProfilePictureUrl,
			Email = student.ContactDetail?.Email,
			Role = "Student",
			ClassDivision = classDivision,
			RollNo = student.RollNo?.ToString(),
			GrNo = student.GrNo?.ToString(),
			ContactNo = student.ContactDetail?.PrimaryContactNo,
			Phone = student.ContactDetail?.PrimaryContactNo,
			JoinDate = student.CreationTime,
			Statistics = new LibraryMemberStatisticsDto
			{
				CurrentIssue = currentIssue,
				ReservedBooks = reserved,
				OverdueReturnBooks = overdue
			}
		};
	}

	private async Task<LibraryMemberDto> BuildEmployeeMemberDtoAsync(Employee employee)
	{
		var issueQuery = await issueRepository.GetQueryableAsync();
		var currentIssue = await AsyncExecuter.CountAsync(issueQuery.Where(i => i.MemberId == employee.Id && i.Status == BookIssueStatus.Issued));
		var overdue = await AsyncExecuter.CountAsync(issueQuery.Where(i => i.MemberId == employee.Id && i.Status == BookIssueStatus.Issued && i.DueDate < DateTime.UtcNow));

		var resQuery = await reservationRepository.GetQueryableAsync();
		var reserved = await AsyncExecuter.CountAsync(resQuery.Where(r => r.MemberId == employee.Id));

		return new LibraryMemberDto
		{
			Id = employee.Id,
			MemberName = $"{employee.FirstName} {employee.LastName}".Trim(),
			MemberIdentifier = employee.EmployeeCode,
			Avatar = employee.ProfilePictureBlobName,
			Email = employee.Email,
			Role = employee.EmployeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Staff",
			ClassDivision = null,
			RollNo = null,
			GrNo = null,
			ContactNo = employee.MobileNo,
			Phone = employee.MobileNo,
			JoinDate = employee.CreationTime,
			Statistics = new LibraryMemberStatisticsDto
			{
				CurrentIssue = currentIssue,
				ReservedBooks = reserved,
				OverdueReturnBooks = overdue
			}
		};
	}

	private async Task<ReturnBookSearchMemberDto> BuildReturnMemberDetailsAsync(BookIssue issue)
	{
		var memberDetails = new ReturnBookSearchMemberDto { Id = issue.MemberId };

		if (issue.MemberRole == 0)
		{
			var student = await studentRepository.FindAsync(issue.MemberId);
			if (student != null)
			{
				memberDetails.MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim();
				memberDetails.Avatar = student.AdditionalDetail?.ProfilePictureUrl;
				memberDetails.Email = student.ContactDetail?.Email;
				memberDetails.Role = "Student";
				memberDetails.RollNo = student.RollNo?.ToString();
				memberDetails.GrNo = student.GrNo?.ToString();
				memberDetails.Phone = student.ContactDetail?.PrimaryContactNo;
				memberDetails.JoinDate = student.CreationTime;
				memberDetails.ClassDivision = await ResolveStudentClassDivisionAsync(student);
			}
		}
		else
		{
			var employee = await employeeRepository.FindAsync(issue.MemberId);
			if (employee != null)
			{
				memberDetails.MemberName = $"{employee.FirstName} {employee.LastName}".Trim();
				memberDetails.Avatar = employee.ProfilePictureBlobName;
				memberDetails.Email = employee.Email;
				memberDetails.Role = employee.EmployeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Staff";
				memberDetails.Phone = employee.MobileNo;
				memberDetails.JoinDate = employee.CreationTime;
			}
		}

		return memberDetails;
	}

	private async Task<string?> ResolveStudentClassDivisionAsync(Student student)
	{
		string? standardName = null;
		string? divisionName = null;

		if (student.StandardId.HasValue)
		{
			var std = await standardRepository.FindAsync(student.StandardId.Value);
			standardName = std?.Name;
		}

		if (student.DivisionId.HasValue)
		{
			var div = await divisionRepository.FindAsync(student.DivisionId.Value);
			divisionName = div?.Name;
		}

		return standardName != null && divisionName != null
			? $"{standardName}-{divisionName}"
			: standardName ?? divisionName;
	}

	private static void EnsureIssueDates(DateTime issueDate, DateTime dueDate)
	{
		if (issueDate == default || dueDate == default || dueDate.Date < issueDate.Date)
		{
			throw LibraryException(LibraryErrorCodes.InvalidIssueDateRange, "Due date must be on or after issue date.");
		}
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
