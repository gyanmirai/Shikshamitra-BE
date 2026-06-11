using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Members;

[Authorize]
[RemoteService(false)]
public class LibraryMemberAppService(
IRepository<LibraryMember, Guid> libraryMemberRepository,
IStudentRepository studentRepository,
IRepository<Employee, Guid> employeeRepository,
IRepository<BookIssue, Guid> bookIssueRepository,
IRepository<BookReservation, Guid> bookReservationRepository,
IRepository<Standard, Guid> standardRepository,
IRepository<Division, Guid> divisionRepository,
IRepository<BookCopy, Guid> bookCopyRepository,
IRepository<Book, Guid> bookRepository,
IRepository<BookCategory, Guid> bookCategoryRepository,
IRepository<LibraryCharge, Guid> libraryChargeRepository,
IRepository<LibraryChargeCategory, Guid> libraryChargeCategoryRepository,
LibraryMemberManager libraryMemberManager,
IGuidGenerator guidGenerator
) : ApplicationService, ILibraryMemberAppService
{
  public async Task CreateLibraryMember(Guid studentId, bool isActive = true)
  {
    var student = await GetStudentForLibraryMemberAsync(studentId);

    var memberIdentifier = libraryMemberManager.GetMemberIdentifier(student);

    var libraryMember = new LibraryMember(
    guidGenerator.Create(),
    CurrentTenant.Id,
    student.Id,
    memberIdentifier,
    isActive
    );

    await libraryMemberRepository.InsertAsync(libraryMember);
  }

  public async Task HandleLibraryMemberAsync(Guid studentId, bool isActive = true)
  {
    await GetStudentForLibraryMemberAsync(studentId);

    var libraryMember = await libraryMemberRepository.FindAsync(x => x.StudentId == studentId);
    if (libraryMember != null)
    {
      libraryMember.ChangeMemberStatus(isActive);
      await libraryMemberRepository.UpdateAsync(libraryMember);
    }
    else
    {
      await CreateLibraryMember(studentId, isActive);
    }
  }

  private async Task<Student> GetStudentForLibraryMemberAsync(Guid studentId)
  {
    var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
    var student = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(student => student.Id == studentId));

    return student ?? throw new EntityNotFoundException($"Student with id: {studentId} not found");
  }

  public async Task<PagedResultDto<LibraryMemberDto>> GetListAsync(GetLibraryMembersInput input)
  {
    var studentQueryable = await studentRepository.GetQueryableAsync();
    var employeeQueryable = await employeeRepository.GetQueryableAsync();

    // Base filters for search
    if (!string.IsNullOrWhiteSpace(input.Search))
    {
      var search = input.Search.Trim().ToLower();
      studentQueryable = studentQueryable.Where(x =>
      (x.FirstName != null && x.FirstName.ToLower().Contains(search)) ||
      (x.LastName != null && x.LastName.ToLower().Contains(search)) ||
      (x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(search))
      );

      employeeQueryable = employeeQueryable.Where(x =>
      (x.FirstName != null && x.FirstName.ToLower().Contains(search)) ||
      (x.LastName != null && x.LastName.ToLower().Contains(search)) ||
      (x.MobileNo != null && x.MobileNo.Contains(search))
      );
    }

    // Only active library members
    studentQueryable = studentQueryable.Where(s => s.AdditionalDetail != null && s.AdditionalDetail.IsLibraryMember == true);
    employeeQueryable = employeeQueryable.Where(e => e.AdditionalDetails != null && e.AdditionalDetails.LibraryMember == true);

    // Apply role filters to base queries
    var includeStudents = string.IsNullOrEmpty(input.Role) || input.Role.Equals("Student", StringComparison.OrdinalIgnoreCase);
    var includeTeachers = string.IsNullOrEmpty(input.Role) || input.Role.Equals("Teacher", StringComparison.OrdinalIgnoreCase);
    var includeStaff = string.IsNullOrEmpty(input.Role) || input.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase);

    if (!includeStudents)
    {
      studentQueryable = studentQueryable.Where(x => false);
    }

    if (!includeTeachers && !includeStaff)
    {
      employeeQueryable = employeeQueryable.Where(x => false);
    }
    else if (includeTeachers && !includeStaff)
    {
      employeeQueryable = employeeQueryable.Where(x => x.EmployeeGroup == EmployeeGroupType.Teaching);
    }
    else if (!includeTeachers && includeStaff)
    {
      employeeQueryable = employeeQueryable.Where(x => x.EmployeeGroup == EmployeeGroupType.NonTeaching);
    }

    // Calculate total count
    var studentCount = await AsyncExecuter.CountAsync(studentQueryable);
    var employeeCount = await AsyncExecuter.CountAsync(employeeQueryable);
    var totalCount = studentCount + employeeCount;

    // Since EF Core Concat with completely different entities requires anonymous projection or similar,
    // and we need specific order (like by creation time/name), we use a rough heuristic: 
    // If Skip < studentCount, we fetch from students, and potentially the remainder from employees.

    var items = new List<LibraryMemberDto>();

    var skip = input.SkipCount;
    var take = input.MaxResultCount;

    List<Student> students = new List<Student>();
    List<Employee> employees = new List<Employee>();

    if (skip < studentCount)
    {
      var studentTake = Math.Min(take, studentCount - skip);
      students = await AsyncExecuter.ToListAsync(
      studentQueryable.OrderByDescending(x => x.CreationTime)
      .Skip(skip)
      .Take(studentTake)
      );

      if (studentTake < take)
      {
        employees = await AsyncExecuter.ToListAsync(
        employeeQueryable.OrderByDescending(x => x.CreationTime)
         .Take(take - studentTake)
        );
      }
    }
    else
    {
      var employeeSkip = skip - studentCount;
      employees = await AsyncExecuter.ToListAsync(
      employeeQueryable.OrderByDescending(x => x.CreationTime)
       .Skip(employeeSkip)
       .Take(take)
      );
    }

    // Resolve names/statistics for current page items dynamically
    if (students.Any())
    {
      var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
      var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

      var stdQuery = await standardRepository.GetQueryableAsync();
      var standardsLookup = (await AsyncExecuter.ToListAsync(stdQuery.Where(s => standardIds.Contains(s.Id))))
        .ToDictionary(s => s.Id, s => s.Name);

      var divQuery = await divisionRepository.GetQueryableAsync();
      var divisionsLookup = (await AsyncExecuter.ToListAsync(divQuery.Where(d => divisionIds.Contains(d.Id))))
        .ToDictionary(d => d.Id, d => d.Name);

      var studentIds = students.Select(s => s.Id).ToList();
      var libMembers = await AsyncExecuter.ToListAsync(
          (await libraryMemberRepository.GetQueryableAsync()).Where(lm => studentIds.Contains(lm.StudentId))
      );
      var libMemberLookup = libMembers.ToDictionary(lm => lm.StudentId, lm => lm.MemberIdentifier);

      foreach (var student in students)
      {
        string? standardName = student.StandardId.HasValue ? standardsLookup.GetValueOrDefault(student.StandardId.Value) : null;
        string? divisionName = student.DivisionId.HasValue ? divisionsLookup.GetValueOrDefault(student.DivisionId.Value) : null;
        var classDiv = standardName != null && divisionName != null ? $"{standardName}-{divisionName}" : standardName ?? divisionName;

        items.Add(new LibraryMemberDto
        {
          Id = student.Id,
          MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
          MemberIdentifier = libMemberLookup.GetValueOrDefault(student.Id),
          Avatar = student.AdditionalDetail?.ProfilePictureUrl,
          Email = student.ContactDetail?.Email,
          Role = "Student",
          ClassDivision = classDiv,
          RollNo = student.RollNo?.ToString(),
          GrNo = student.GrNo?.ToString(),
          ContactNo = student.ContactDetail?.PrimaryContactNo,
          Phone = student.ContactDetail?.SecondaryContactNo ?? student.ContactDetail?.PrimaryContactNo,
          JoinDate = student.CreationTime,
          Statistics = await GetStatisticsAsync(student.Id)
        });
      }
    }

    if (employees.Any())
    {
      foreach (var emp in employees)
      {
        items.Add(new LibraryMemberDto
        {
          Id = emp.Id,
          MemberName = $"{emp.FirstName} {emp.MiddleName} {emp.LastName}".Trim(),
          MemberIdentifier = emp.EmployeeCode,
          Avatar = emp.ProfilePictureBlobName,
          Email = emp.Email,
          Role = emp.EmployeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Staff",
          ContactNo = emp.MobileNo,
          Phone = emp.SecondaryMobileNo ?? emp.MobileNo,
          JoinDate = emp.CreationTime,
          Statistics = await GetStatisticsAsync(emp.Id)
        });
      }
    }

    return new PagedResultDto<LibraryMemberDto>(totalCount, items);
  }

  private async Task<LibraryMemberStatisticsDto> GetStatisticsAsync(Guid memberId)
  {
    var issueQuery = await bookIssueRepository.GetQueryableAsync();
    var resQuery = await bookReservationRepository.GetQueryableAsync();
    var now = DateTime.UtcNow;

    var currentIssue = await AsyncExecuter.CountAsync(
    issueQuery.Where(i => i.MemberId == memberId && i.Status == BookIssueStatus.Issued)
    );

    var overdue = await AsyncExecuter.CountAsync(
    issueQuery.Where(i => i.MemberId == memberId && i.Status == BookIssueStatus.Issued && i.DueDate < now)
    );

    var reserved = await AsyncExecuter.CountAsync(
    resQuery.Where(r => r.MemberId == memberId)
    );

    return new LibraryMemberStatisticsDto
    {
      CurrentIssue = currentIssue,
      ReservedBooks = reserved,
      OverdueReturnBooks = overdue
    };
  }

  public async Task<PaginatedResponse<MemberBorrowedBookDto>> GetBorrowedBooksAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    var issueQuery = await bookIssueRepository.GetQueryableAsync();
    var filteredQuery = issueQuery.Where(i => i.MemberId == memberId && i.Status == BookIssueStatus.Issued);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var issues = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(i => i.IssueDate).Skip((page - 1) * pageSize).Take(pageSize));

    var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();
    var copyQuery = await bookCopyRepository.GetQueryableAsync();
    var copies = await AsyncExecuter.ToListAsync(copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
    var copyBookIdLookup = copies.ToDictionary(c => c.AccessionNumber, c => c.BookId);

    var bookIds = copies.Select(c => c.BookId).Distinct().ToList();
    var bookQuery = await bookRepository.GetQueryableAsync();
    var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
    var bookLookup = books.ToDictionary(b => b.Id);

    var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
    var catQuery = await bookCategoryRepository.GetQueryableAsync();
    var categories = await AsyncExecuter.ToListAsync(catQuery.Where(c => catIds.Contains(c.Id)));
    var catLookup = categories.ToDictionary(c => c.Id, c => c.Name);

    var items = issues.Select(issue =>
    {
      var dto = new MemberBorrowedBookDto
      {
        Id = issue.Id,
        AccessionNo = issue.AccessionNumber,
        IssuedOn = issue.IssueDate,
        DueOn = issue.DueDate
      };

      if (copyBookIdLookup.TryGetValue(issue.AccessionNumber, out var bookId) &&
          bookLookup.TryGetValue(bookId, out var book))
      {
        dto.BookTitle = book.Title;
        dto.BookCategory = catLookup.GetValueOrDefault(book.CategoryId);
      }

      return dto;
    }).ToList();

    return new PaginatedResponse<MemberBorrowedBookDto>
    {
      TotalCount = totalCount,
      CurrentPage = page,
      PageSize = pageSize,
      TotalPages = totalPages,
      Items = items
    };
  }

  public async Task<PaginatedResponse<MemberReservedBookDto>> GetReservedBooksAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    var resQuery = await bookReservationRepository.GetQueryableAsync();
    var filteredQuery = resQuery.Where(r => r.MemberId == memberId);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var reservations = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(r => r.RequestedDate).Skip((page - 1) * pageSize).Take(pageSize));

    var bookIds = reservations.Select(r => r.BookId).Distinct().ToList();
    var bookQuery = await bookRepository.GetQueryableAsync();
    var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
    var bookLookup = books.ToDictionary(b => b.Id);

    var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
    var catQuery = await bookCategoryRepository.GetQueryableAsync();
    var categories = await AsyncExecuter.ToListAsync(catQuery.Where(c => catIds.Contains(c.Id)));
    var catLookup = categories.ToDictionary(c => c.Id, c => c.Name);

    var items = reservations.Select(reservation =>
    {
      var dto = new MemberReservedBookDto
      {
        Id = reservation.Id,
        ReservedOn = reservation.RequestedDate
      };

      if (bookLookup.TryGetValue(reservation.BookId, out var book))
      {
        dto.BookTitle = book.Title;
        dto.Author = book.Author;
        dto.BookCategory = catLookup.GetValueOrDefault(book.CategoryId);
      }

      return dto;
    }).ToList();

    return new PaginatedResponse<MemberReservedBookDto>
    {
      TotalCount = totalCount,
      CurrentPage = page,
      PageSize = pageSize,
      TotalPages = totalPages,
      Items = items
    };
  }

  public async Task<PaginatedResponse<MemberChargeItemDto>> GetMemberChargesAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    var chargeQuery = await libraryChargeRepository.GetQueryableAsync();
    var filteredQuery = chargeQuery.Where(c => c.MemberId == memberId);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var charges = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(c => c.ChargeDate).Skip((page - 1) * pageSize).Take(pageSize));

    var categoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
    var chargeCatQuery = await libraryChargeCategoryRepository.GetQueryableAsync();
    var chargeCategories = await AsyncExecuter.ToListAsync(chargeCatQuery.Where(c => categoryIds.Contains(c.Id)));
    var chargeCatLookup = chargeCategories.ToDictionary(c => c.Id, c => c.ChargeType);

    var issueIds = charges.Where(c => c.BookIssueId.HasValue).Select(c => c.BookIssueId!.Value).Distinct().ToList();
    Dictionary<Guid, BookIssue> issueLookup = new();
    Dictionary<string, (string title, string? category)> accessionBookLookup = new();

    if (issueIds.Any())
    {
      var issueQuery = await bookIssueRepository.GetQueryableAsync();
      var issues = await AsyncExecuter.ToListAsync(issueQuery.Where(i => issueIds.Contains(i.Id)));
      issueLookup = issues.ToDictionary(i => i.Id);

      var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();
      var copyQuery = await bookCopyRepository.GetQueryableAsync();
      var copies = await AsyncExecuter.ToListAsync(copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
      var bookIds = copies.Select(c => c.BookId).Distinct().ToList();

      var bookQuery = await bookRepository.GetQueryableAsync();
      var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
      var bookLookup = books.ToDictionary(b => b.Id);

      var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
      var bookCatQuery = await bookCategoryRepository.GetQueryableAsync();
      var bookCats = await AsyncExecuter.ToListAsync(bookCatQuery.Where(c => catIds.Contains(c.Id)));
      var bookCatLookup = bookCats.ToDictionary(c => c.Id, c => c.Name);

      foreach (var copy in copies)
      {
        if (bookLookup.TryGetValue(copy.BookId, out var book))
          accessionBookLookup[copy.AccessionNumber] = (book.Title, bookCatLookup.GetValueOrDefault(book.CategoryId));
      }
    }

    var items = charges.Select(charge =>
    {
      var dto = new MemberChargeItemDto
      {
        Id = charge.Id,
        Amount = charge.Amount,
        Date = charge.ChargeDate,
        ChargeType = chargeCatLookup.GetValueOrDefault(charge.ChargeCategoryId, "Unknown"),
        Reason = charge.Remarks,
        PaymentStatus = (int) charge.PaymentStatus
      };

      if (charge.BookIssueId.HasValue && issueLookup.TryGetValue(charge.BookIssueId.Value, out var issue))
      {
        dto.AccessionNo = issue.AccessionNumber;
        if (accessionBookLookup.TryGetValue(issue.AccessionNumber, out var bookInfo))
        {
          dto.BookTitle = bookInfo.title;
          dto.BookCategory = bookInfo.category;
        }
      }

      return dto;
    }).ToList();

    return new PaginatedResponse<MemberChargeItemDto>
    {
      TotalCount = totalCount,
      CurrentPage = page,
      PageSize = pageSize,
      TotalPages = totalPages,
      Items = items
    };
  }

  public async Task<PaginatedResponse<MemberBorrowHistoryDto>> GetBorrowHistoryAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    var issueQuery = await bookIssueRepository.GetQueryableAsync();
    var filteredQuery = issueQuery.Where(i => i.MemberId == memberId);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var issues = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(i => i.IssueDate).Skip((page - 1) * pageSize).Take(pageSize));

    var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();
    var copyQuery = await bookCopyRepository.GetQueryableAsync();
    var copies = await AsyncExecuter.ToListAsync(copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
    var copyBookIdLookup = copies.ToDictionary(c => c.AccessionNumber, c => c.BookId);

    var bookIds = copies.Select(c => c.BookId).Distinct().ToList();
    var bookQuery = await bookRepository.GetQueryableAsync();
    var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
    var bookLookup = books.ToDictionary(b => b.Id);

    var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
    var catQuery = await bookCategoryRepository.GetQueryableAsync();
    var categories = await AsyncExecuter.ToListAsync(catQuery.Where(c => catIds.Contains(c.Id)));
    var catLookup = categories.ToDictionary(c => c.Id, c => c.Name);

    var items = issues.Select(issue =>
    {
      var dto = new MemberBorrowHistoryDto
      {
        BorrowId = issue.Id,
        BookId = issue.BookCopyId,
        AccessionNo = issue.AccessionNumber,
        IssuedOn = issue.IssueDate,
        DueOn = issue.DueDate
      };

      if (copyBookIdLookup.TryGetValue(issue.AccessionNumber, out var bookId) &&
          bookLookup.TryGetValue(bookId, out var book))
      {
        dto.BookTitle = book.Title;
        dto.BookCategory = catLookup.GetValueOrDefault(book.CategoryId);
      }

      return dto;
    }).ToList();

    return new PaginatedResponse<MemberBorrowHistoryDto>
    {
      TotalCount = totalCount,
      CurrentPage = page,
      PageSize = pageSize,
      TotalPages = totalPages,
      Items = items
    };
  }
}
