using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
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
    IRepository<BookIssue, Guid> _issueRepository,
    IRepository<BookCopy, Guid> _bookCopyRepository,
    IRepository<Book, Guid> _bookRepository,
    IRepository<BookCategory, Guid> _categoryRepository,
    IRepository<BookReservation, Guid> _reservationRepository,
    IRepository<Student, Guid> _studentRepository,
    IRepository<Employee, Guid> _employeeRepository,
    IRepository<Standard, Guid> _standardRepository,
    IRepository<Division, Guid> _divisionRepository)
    : ApplicationService, IBookIssueAppService
{
    public async Task<GenericCreatedResponse> IssueAsync(IssueBookRequest input)
    {
        // Find the book copy by accession number
        var copyQuery = await _bookCopyRepository.GetQueryableAsync();
        var copy = await AsyncExecuter.FirstOrDefaultAsync(
            copyQuery.Where(c => c.AccessionNumber == input.AccessionNo));

        if (copy == null)
            throw new UserFriendlyException($"Book copy with accession number '{input.AccessionNo}' not found.", code: "404");

        if (copy.Status != BookCopyStatus.Available && copy.Status != BookCopyStatus.Reserved)
            throw new UserFriendlyException($"Book copy '{input.AccessionNo}' is not available for issue. Current status: {copy.Status}.", code: "400");

        // Determine member role (try student first, then employee)
        int memberRole = await ResolveMemberRoleAsync(input.MemberId);

        // Create the issue record
        var issue = new BookIssue(
            GuidGenerator.Create(),
            copy.Id,
            copy.AccessionNumber,
            input.MemberId,
            memberRole,
            input.IssueDate,
            input.DueDate,
            CurrentTenant.Id
        );

        await _issueRepository.InsertAsync(issue, autoSave: true);

        // Update book copy status to Issued
        copy.Status = BookCopyStatus.Issued;
        await _bookCopyRepository.UpdateAsync(copy, autoSave: true);

        return new GenericCreatedResponse
        {
            Id = issue.Id.ToString(),
            Message = "Book issued successfully."
        };
    }

    public async Task<GenericCreatedResponse> ReturnAsync(ReturnBookRequest input)
    {
        // Find the active issue for this accession + member
        var issueQuery = await _issueRepository.GetQueryableAsync();
        var issue = await AsyncExecuter.FirstOrDefaultAsync(
            issueQuery.Where(i =>
                i.AccessionNumber == input.AccessionNo &&
                i.MemberId == input.MemberId &&
                i.Status == BookIssueStatus.Issued));

        if (issue == null)
            throw new UserFriendlyException($"No active issue found for accession '{input.AccessionNo}' and the specified member.", code: "404");

        // Update issue with return info
        issue.ReturnDate = input.ReturnDate;
        issue.Status = BookIssueStatus.Returned;
        issue.ChargeCategoryId = input.ChargeCategoryId;
        issue.Amount = input.Amount;
        issue.FineDueDate = input.FineDueDate;
        issue.Remarks = input.Remarks;

        await _issueRepository.UpdateAsync(issue, autoSave: true);

        // Update book copy status back to Available
        var copyQuery = await _bookCopyRepository.GetQueryableAsync();
        var copy = await AsyncExecuter.FirstOrDefaultAsync(
            copyQuery.Where(c => c.AccessionNumber == input.AccessionNo));

        if (copy != null)
        {
            copy.Status = BookCopyStatus.Available;
            await _bookCopyRepository.UpdateAsync(copy, autoSave: true);
        }

        return new GenericCreatedResponse
        {
            Id = issue.Id.ToString(),
            Message = "Book returned successfully."
        };
    }

    public async Task<AccessionBookDto> GetByAccessionAsync(string accessionNo)
    {
        var copyQuery = await _bookCopyRepository.GetQueryableAsync();
        var copy = await AsyncExecuter.FirstOrDefaultAsync(
            copyQuery.Where(c => c.AccessionNumber == accessionNo));

        if (copy == null)
            throw new UserFriendlyException($"Book copy with accession number '{accessionNo}' not found.", code: "404");

        var book = await _bookRepository.GetAsync(copy.BookId);
        var category = await _categoryRepository.FindAsync(book.CategoryId);

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

    public async Task<LibraryMemberDto> GetMemberAsync(Guid memberId)
    {
        // Try student first
        var student = await _studentRepository.FindAsync(memberId);
        if (student != null)
        {
            return await BuildStudentMemberDtoAsync(student);
        }

        // Try employee
        var employee = await _employeeRepository.FindAsync(memberId);
        if (employee != null)
        {
            return await BuildEmployeeMemberDtoAsync(employee);
        }

        throw new UserFriendlyException($"Member with id '{memberId}' not found.", code: "404");
    }

    public async Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(int page = 1, int pageSize = 20)
    {
        var query = await _issueRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

        var issues = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreationTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize));

        // Batch-resolve member & book info
        var memberIds = issues.Select(i => i.MemberId).Distinct().ToList();
        var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();

        // Load book copies for accession numbers
        var copyQuery = await _bookCopyRepository.GetQueryableAsync();
        var copies = await AsyncExecuter.ToListAsync(
            copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
        var bookIds = copies.Select(c => c.BookId).Distinct().ToList();

        // Load books
        var bookQuery = await _bookRepository.GetQueryableAsync();
        var books = await AsyncExecuter.ToListAsync(
            bookQuery.Where(b => bookIds.Contains(b.Id)));
        var bookLookup = books.ToDictionary(b => b.Id);

        // Book copy -> book mapping
        var copyBookLookup = copies.ToDictionary(c => c.AccessionNumber, c => c.BookId);

        // Load categories
        var catIds = books.Select(b => b.CategoryId).Distinct().ToList();
        var catQuery = await _categoryRepository.GetQueryableAsync();
        var categories = await AsyncExecuter.ToListAsync(
            catQuery.Where(c => catIds.Contains(c.Id)));
        var catLookup = categories.ToDictionary(c => c.Id, c => c.Name);

        // Load students
        var studentQuery = await _studentRepository.GetQueryableAsync();
        var students = await AsyncExecuter.ToListAsync(
            studentQuery.Where(s => memberIds.Contains(s.Id)));
        var studentLookup = students.ToDictionary(s => s.Id);

        // Load employees
        var empQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await AsyncExecuter.ToListAsync(
            empQuery.Where(e => memberIds.Contains(e.Id)));
        var empLookup = employees.ToDictionary(e => e.Id);

        // Resolve standard/division for students
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var stdQuery = await _standardRepository.GetQueryableAsync();
        var standards = await AsyncExecuter.ToListAsync(
            stdQuery.Where(s => standardIds.Contains(s.Id)));
        var stdLookup = standards.ToDictionary(s => s.Id, s => s.Name);

        var divQuery = await _divisionRepository.GetQueryableAsync();
        var divisions = await AsyncExecuter.ToListAsync(
            divQuery.Where(d => divisionIds.Contains(d.Id)));
        var divLookup = divisions.ToDictionary(d => d.Id, d => d.Name);

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
                Status = (int) issue.Status
            };

            // Resolve book info
            if (copyBookLookup.TryGetValue(issue.AccessionNumber, out var bookId) &&
                bookLookup.TryGetValue(bookId, out var book))
            {
                dto.BookTitle = book.Title;
                dto.BookCategory = catLookup.GetValueOrDefault(book.CategoryId);
            }

            // Resolve member info
            if (studentLookup.TryGetValue(issue.MemberId, out var student))
            {
                dto.MemberName = $"{student.FirstName} {student.LastName}".Trim();
                dto.RollNo = student.RollNo?.ToString();
                dto.GrNo = student.GrNo?.ToString();
                dto.StandardName = student.StandardId.HasValue
                    ? stdLookup.GetValueOrDefault(student.StandardId.Value)
                    : null;
                dto.DivisionName = student.DivisionId.HasValue
                    ? divLookup.GetValueOrDefault(student.DivisionId.Value)
                    : null;
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

    private async Task<int> ResolveMemberRoleAsync(Guid memberId)
    {
        var student = await _studentRepository.FindAsync(memberId);
        if (student != null) return 0; // Student

        var employee = await _employeeRepository.FindAsync(memberId);
        if (employee != null) return 1; // Employee/Teacher

        throw new UserFriendlyException($"Member with id '{memberId}' not found as student or employee.", code: "404");
    }

    private async Task<LibraryMemberDto> BuildStudentMemberDtoAsync(Student student)
    {
        string? standardName = null;
        string? divisionName = null;

        if (student.StandardId.HasValue)
        {
            var std = await _standardRepository.FindAsync(student.StandardId.Value);
            standardName = std?.Name;
        }

        if (student.DivisionId.HasValue)
        {
            var div = await _divisionRepository.FindAsync(student.DivisionId.Value);
            divisionName = div?.Name;
        }

        var classDivision = standardName != null && divisionName != null
            ? $"{standardName}-{divisionName}"
            : standardName ?? divisionName;

        // Count statistics
        var issueQuery = await _issueRepository.GetQueryableAsync();
        var currentIssue = await AsyncExecuter.CountAsync(
            issueQuery.Where(i => i.MemberId == student.Id && i.Status == BookIssueStatus.Issued));
        var overdue = await AsyncExecuter.CountAsync(
            issueQuery.Where(i => i.MemberId == student.Id && i.Status == BookIssueStatus.Issued && i.DueDate < DateTime.UtcNow));

        var resQuery = await _reservationRepository.GetQueryableAsync();
        var reserved = await AsyncExecuter.CountAsync(
            resQuery.Where(r => r.MemberId == student.Id));

        return new LibraryMemberDto
        {
            Id = student.Id,
            MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
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
        var issueQuery = await _issueRepository.GetQueryableAsync();
        var currentIssue = await AsyncExecuter.CountAsync(
            issueQuery.Where(i => i.MemberId == employee.Id && i.Status == BookIssueStatus.Issued));
        var overdue = await AsyncExecuter.CountAsync(
            issueQuery.Where(i => i.MemberId == employee.Id && i.Status == BookIssueStatus.Issued && i.DueDate < DateTime.UtcNow));

        var resQuery = await _reservationRepository.GetQueryableAsync();
        var reserved = await AsyncExecuter.CountAsync(
            resQuery.Where(r => r.MemberId == employee.Id));

        return new LibraryMemberDto
        {
            Id = employee.Id,
            MemberName = $"{employee.FirstName} {employee.LastName}".Trim(),
            Avatar = employee.ProfilePictureBlobName,
            Email = employee.Email,
            Role = "Employee",
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
}
