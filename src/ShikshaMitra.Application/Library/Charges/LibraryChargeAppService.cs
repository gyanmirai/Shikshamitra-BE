using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Charges;

[RemoteService(false)]
public class LibraryChargeAppService(
    IRepository<LibraryCharge, Guid> _chargeRepository,
    IRepository<LibraryChargeCategory, Guid> _chargeCategoryRepository,
    IRepository<BookIssue, Guid> _issueRepository,
    IRepository<BookCopy, Guid> _bookCopyRepository,
    IRepository<Book, Guid> _bookRepository,
    IRepository<Student, Guid> _studentRepository,
    IRepository<Employee, Guid> _employeeRepository,
    IRepository<Standard, Guid> _standardRepository,
    IRepository<Division, Guid> _divisionRepository)
    : ApplicationService, ILibraryChargeAppService
{
    public async Task<GenericCreatedResponse> CreateAsync(CreateChargeRequest input)
    {
        // Validate charge category exists
        var category = await _chargeCategoryRepository.FindAsync(input.ChargeCategoryId);
        if (category == null)
            throw new UserFriendlyException("Charge category not found.", code: "404");

        // Determine member role
        int memberRole = await ResolveMemberRoleAsync(input.MemberId);

        var charge = new LibraryCharge(
            GuidGenerator.Create(),
            input.MemberId,
            memberRole,
            input.ChargeCategoryId,
            input.BookIssueId,
            input.Amount,
            input.ChargeDate,
            input.DueDate,
            CurrentTenant.Id)
        {
            Remarks = input.Remarks
        };

        await _chargeRepository.InsertAsync(charge, autoSave: true);

        return new GenericCreatedResponse
        {
            Id = charge.Id.ToString(),
            Message = "Library charge created successfully."
        };
    }

    public async Task<PaginatedResponse<ChargeListItemDto>> GetListAsync(int page = 1, int pageSize = 20)
    {
        var query = await _chargeRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

        var charges = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreationTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize));

        // Batch-resolve lookups
        var memberIds = charges.Select(c => c.MemberId).Distinct().ToList();
        var categoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
        var issueIds = charges.Where(c => c.BookIssueId.HasValue).Select(c => c.BookIssueId!.Value).Distinct().ToList();

        // Load charge categories
        var catQuery = await _chargeCategoryRepository.GetQueryableAsync();
        var categories = await AsyncExecuter.ToListAsync(
            catQuery.Where(c => categoryIds.Contains(c.Id)));
        var catLookup = categories.ToDictionary(c => c.Id, c => c.ChargeType);

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

        // Load book issues for linked charges
        Dictionary<Guid, BookIssue> issueLookup = new();
        Dictionary<string, string> accessionTitleLookup = new();
        if (issueIds.Any())
        {
            var issueQuery = await _issueRepository.GetQueryableAsync();
            var issues = await AsyncExecuter.ToListAsync(
                issueQuery.Where(i => issueIds.Contains(i.Id)));
            issueLookup = issues.ToDictionary(i => i.Id);

            // Load book copies for accession numbers
            var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();
            var copyQuery = await _bookCopyRepository.GetQueryableAsync();
            var copies = await AsyncExecuter.ToListAsync(
                copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
            var bookIds = copies.Select(c => c.BookId).Distinct().ToList();

            var bookQuery = await _bookRepository.GetQueryableAsync();
            var books = await AsyncExecuter.ToListAsync(
                bookQuery.Where(b => bookIds.Contains(b.Id)));
            var bookLookup = books.ToDictionary(b => b.Id, b => b.Title);

            // Build accession number -> book title lookup
            foreach (var copy in copies)
            {
                if (bookLookup.TryGetValue(copy.BookId, out var title))
                {
                    accessionTitleLookup[copy.AccessionNumber] = title;
                }
            }
        }

        var items = charges.Select(charge =>
        {
            var dto = new ChargeListItemDto
            {
                Id = charge.Id,
                MemberId = charge.MemberId,
                Role = charge.MemberRole,
                ChargeType = catLookup.GetValueOrDefault(charge.ChargeCategoryId, "Unknown"),
                Amount = charge.Amount,
                ChargeDate = charge.ChargeDate,
                DueDate = charge.DueDate,
                PaymentStatus = (int) charge.PaymentStatus,
                PaidDate = charge.PaidDate
            };

            // Resolve member name
            if (studentLookup.TryGetValue(charge.MemberId, out var student))
            {
                dto.MemberName = $"{student.FirstName} {student.LastName}".Trim();
            }
            else if (empLookup.TryGetValue(charge.MemberId, out var emp))
            {
                dto.MemberName = $"{emp.FirstName} {emp.LastName}".Trim();
            }

            // Resolve book info from linked issue
            if (charge.BookIssueId.HasValue &&
                issueLookup.TryGetValue(charge.BookIssueId.Value, out var issue))
            {
                dto.AccessionNo = issue.AccessionNumber;
                accessionTitleLookup.TryGetValue(issue.AccessionNumber, out var bookTitle);
                dto.BookTitle = bookTitle;
            }

            return dto;
        }).ToList();

        return new PaginatedResponse<ChargeListItemDto>
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = items
        };
    }

    public async Task<ChargeDetailDto> GetAsync(Guid id)
    {
        var charge = await _chargeRepository.FindAsync(id);
        if (charge == null)
            throw new UserFriendlyException("Library charge not found.", code: "404");

        // Load charge category
        var category = await _chargeCategoryRepository.FindAsync(charge.ChargeCategoryId);

        // Build detail dto
        var dto = new ChargeDetailDto
        {
            Id = charge.Id,
            MemberId = charge.MemberId,
            Role = charge.MemberRole,
            ChargeCategoryId = charge.ChargeCategoryId,
            ChargeType = category?.ChargeType ?? "Unknown",
            BookIssueId = charge.BookIssueId,
            Amount = charge.Amount,
            ChargeDate = charge.ChargeDate,
            DueDate = charge.DueDate,
            PaymentStatus = (int) charge.PaymentStatus,
            PaidDate = charge.PaidDate,
            Remarks = charge.Remarks,
            CreatedAt = charge.CreationTime,
            LastModifiedAt = charge.LastModificationTime
        };

        // Resolve member info
        if (charge.MemberRole == 0)
        {
            var student = await _studentRepository.FindAsync(charge.MemberId);
            if (student != null)
            {
                dto.MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim();
                dto.Avatar = student.AdditionalDetail?.ProfilePictureUrl;
                dto.RollNo = student.RollNo?.ToString();
                dto.GrNo = student.GrNo?.ToString();
                dto.ContactNo = student.ContactDetail?.PrimaryContactNo;

                string? standardName = null, divisionName = null;
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
                dto.ClassDivision = standardName != null && divisionName != null
                    ? $"{standardName}-{divisionName}"
                    : standardName ?? divisionName;
            }
        }
        else
        {
            var employee = await _employeeRepository.FindAsync(charge.MemberId);
            if (employee != null)
            {
                dto.MemberName = $"{employee.FirstName} {employee.LastName}".Trim();
                dto.Avatar = employee.ProfilePictureBlobName;
                dto.ContactNo = employee.MobileNo;
            }
        }

        // Resolve book info from linked issue
        if (charge.BookIssueId.HasValue)
        {
            var issue = await _issueRepository.FindAsync(charge.BookIssueId.Value);
            if (issue != null)
            {
                dto.AccessionNo = issue.AccessionNumber;

                var copyQuery = await _bookCopyRepository.GetQueryableAsync();
                var copy = await AsyncExecuter.FirstOrDefaultAsync(
                    copyQuery.Where(c => c.AccessionNumber == issue.AccessionNumber));
                if (copy != null)
                {
                    var book = await _bookRepository.FindAsync(copy.BookId);
                    dto.BookTitle = book?.Title;
                }
            }
        }

        return dto;
    }

    public async Task<GenericUpdatedResponse> UpdateAsync(Guid id, UpdateChargeRequest input)
    {
        var charge = await _chargeRepository.FindAsync(id);
        if (charge == null)
            throw new UserFriendlyException("Library charge not found.", code: "404");

        if (input.Amount.HasValue) charge.Amount = input.Amount.Value;
        if (input.DueDate.HasValue) charge.DueDate = input.DueDate.Value;
        if (input.PaymentStatus.HasValue) charge.PaymentStatus = (ChargePaymentStatus) input.PaymentStatus.Value;
        if (input.PaidDate.HasValue) charge.PaidDate = input.PaidDate.Value;
        if (input.Remarks != null) charge.Remarks = input.Remarks;

        await _chargeRepository.UpdateAsync(charge, autoSave: true);

        return new GenericUpdatedResponse
        {
            Id = charge.Id.ToString(),
            Message = "Library charge updated successfully."
        };
    }

    public async Task DeleteAsync(Guid id)
    {
        var charge = await _chargeRepository.FindAsync(id);
        if (charge == null)
            throw new UserFriendlyException("Library charge not found.", code: "404");

        await _chargeRepository.DeleteAsync(charge, autoSave: true);
    }

    private async Task<int> ResolveMemberRoleAsync(Guid memberId)
    {
        var student = await _studentRepository.FindAsync(memberId);
        if (student != null) return 0;

        var employee = await _employeeRepository.FindAsync(memberId);
        if (employee != null) return 1;

        throw new UserFriendlyException($"Member with id '{memberId}' not found as student or employee.", code: "404");
    }
}
