using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
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
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Members;

[Authorize(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)]
[RemoteService(false)]
public partial class LibraryMemberAppService(
IRepository<LibraryMember, Guid> libraryMemberRepository,
IStudentRepository studentRepository,
IRepository<Standard, Guid> standardRepository,
IRepository<Division, Guid> divisionRepository,
IRepository<BookCopy, Guid> bookCopyRepository,
IRepository<Book, Guid> bookRepository,
IRepository<BookCategory, Guid> bookCategoryRepository,
IRepository<LibraryCharge, Guid> libraryChargeRepository,
IRepository<LibraryChargeCategory, Guid> libraryChargeCategoryRepository,
LibraryMemberManager libraryMemberManager,
IGuidGenerator guidGenerator,
ILibraryAccessService libraryAccess,
LibraryMemberStatisticsReader statisticsReader,
LibraryStoredLoanScope loanScope,
LibraryStoredReservationScope requestScope,
ILibraryChargePositionReader chargePositionReader
) : ApplicationService, ILibraryMemberAppService
{
  [Authorize(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.Update)]
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

  [Authorize(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.Update)]
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
    await libraryAccess.EnsureMemberAsync(studentId);
    var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
    var student = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(student => student.Id == studentId));

    return student ?? throw new EntityNotFoundException($"Student with id: {studentId} not found");
  }

  public async Task<PaginatedResponse<LibraryMemberDto>> GetListAsync(GetLibraryMembersInput input)
  {
    var studentQueryable = await libraryAccess.GetStudentsAsync();
    var employeeQueryable = await libraryAccess.GetEmployeesAsync();

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

    var statistics = await GetStatisticsMapAsync(students.Select(x => x.Id).Concat(employees.Select(x => x.Id)));

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
          Statistics = statistics[student.Id]
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
          Statistics = statistics[emp.Id]
        });
      }
    }

    var pageSize = input.MaxResultCount <= 0 ? 10 : input.MaxResultCount;
    var currentPage = (input.SkipCount / pageSize) + 1;

    return new PaginatedResponse<LibraryMemberDto>
    {
      TotalCount = totalCount,
      CurrentPage = currentPage,
      PageSize = pageSize,
      TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
      Items = items
    };
  }

  public async Task<PaginatedResponse<MemberBorrowedBookDto>> GetBorrowedBooksAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filteredQuery = (await GetMemberLoanQueryAsync(memberId)).Where(i => i.Status == BookIssueStatus.Issued);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var issues = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(i => i.SchoolIssueDate).ThenByDescending(i => i.CreationTime).ThenBy(i => i.Id)
        .Skip((page - 1) * pageSize).Take(pageSize));

    var books = await GetMemberIssueBooksAsync(issues);

    var items = issues.Select(issue =>
    {
      var dto = new MemberBorrowedBookDto
      {
        Id = issue.Id,
        AccessionNo = issue.AccessionNumber,
        IssuedOn = issue.IssueDate,
        DueOn = issue.DueDate,
        AcademicYearId = issue.AcademicYearId,
        SchoolIssueDate = issue.SchoolIssueDate,
        SchoolDueDate = issue.SchoolDueDate,
        IssueDateProvenance = (int)issue.IssueDateProvenance,
        DueDateProvenance = (int)issue.DueDateProvenance
      };

      var book = books[issue.Id];
      dto.CopyId = book.CopyId; dto.BookId = book.BookId; dto.BookTitle = book.Title; dto.BookCategory = book.Category;
      dto.CopyLinkSource = book.LinkSource; dto.RequiresCopyReconciliation = book.RequiresReconciliation;

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
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filteredQuery = await GetMemberRequestQueryAsync(memberId);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var reservations = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(r => r.SchoolRequestedDate).ThenByDescending(r => r.CreationTime).ThenBy(r => r.Id)
        .Skip((page - 1) * pageSize).Take(pageSize));

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
        ReservedOn = reservation.RequestedDate,
        AcademicYearId = reservation.AcademicYearId,
        RequestedDate = reservation.SchoolRequestedDate,
        RequestExpiryDate = reservation.SchoolRequestExpiryDate,
        RequestedDateProvenance = (int)reservation.RequestedDateProvenance,
        RequestExpiryDateProvenance = (int)reservation.RequestExpiryDateProvenance,
        State = (int)reservation.State
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

  public async Task<PaginatedResponse<MemberBorrowHistoryDto>> GetBorrowHistoryAsync(Guid memberId, int page = 1, int pageSize = 10)
  {
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);
    var filteredQuery = await GetMemberLoanQueryAsync(memberId);

    var totalCount = await AsyncExecuter.CountAsync(filteredQuery);
    var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

    var issues = await AsyncExecuter.ToListAsync(
      filteredQuery.OrderByDescending(i => i.SchoolIssueDate).ThenByDescending(i => i.CreationTime).ThenBy(i => i.Id)
        .Skip((page - 1) * pageSize).Take(pageSize));

    var books = await GetMemberIssueBooksAsync(issues);

    var items = issues.Select(issue =>
    {
      var dto = new MemberBorrowHistoryDto
      {
        BorrowId = issue.Id,
        BookId = books[issue.Id].BookId ?? Guid.Empty,
        AccessionNo = issue.AccessionNumber,
        IssuedOn = issue.IssueDate,
        DueOn = issue.DueDate,
        AcademicYearId = issue.AcademicYearId,
        SchoolIssueDate = issue.SchoolIssueDate,
        SchoolDueDate = issue.SchoolDueDate,
        SchoolReturnDate = issue.SchoolReturnDate,
        IssueDateProvenance = (int)issue.IssueDateProvenance,
        DueDateProvenance = (int)issue.DueDateProvenance,
        ReturnDateProvenance = (int)issue.ReturnDateProvenance,
        ReturnedOn = issue.ReturnDate,
        StoredStatus = (int)issue.Status
      };

      var book = books[issue.Id];
      dto.CopyId = book.CopyId; dto.BookTitle = book.Title; dto.BookCategory = book.Category;
      dto.CopyLinkSource = book.LinkSource; dto.RequiresCopyReconciliation = book.RequiresReconciliation;

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
