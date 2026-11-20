using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Charges;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.View)]
[RemoteService(false)]
public partial class LibraryChargeAppService(
	IRepository<LibraryCharge, Guid> chargeRepository,
	IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	IRepository<Book, Guid> bookRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	ILibraryAccessService libraryAccess,
	ILibraryInventoryLock inventoryLock,
	ICurrentTenant tenant)
	: ApplicationService, ILibraryChargeAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(CreateChargeRequest input)
	{
		await inventoryLock.AcquireAsync(tenant.Id);
		input.MemberId = await libraryAccess.EnsureMemberAsync(input.MemberId);
		await EnsureChargeCategoryAsync(input.ChargeCategoryId);

		var memberRole = await ResolveActiveMemberRoleAsync(input.MemberId);

		if (input.BookIssueId.HasValue)
		{
			await EnsureIssueBelongsToMemberAsync(input.BookIssueId.Value, input.MemberId);
		}

		LibraryCharge charge;
		if (input.AcademicYearId.HasValue || input.SchoolChargeDate.HasValue || input.SchoolDueDate.HasValue)
		{
			if (!input.AcademicYearId.HasValue || !input.SchoolChargeDate.HasValue || !input.SchoolDueDate.HasValue ||
				string.IsNullOrWhiteSpace(input.Remarks) || !tenant.Id.HasValue)
				throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview);
			await libraryAccess.GetContextAsync(input.AcademicYearId.Value);
			var category = await chargeCategoryRepository.GetAsync(input.ChargeCategoryId);
			if (category.AcademicYearId != input.AcademicYearId)
				throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview,
					"The assessment year and charge category year must match.");
			var yearEvidence = JsonSerializer.Serialize(new
			{
				input.AcademicYearId, input.BookIssueId, ConfirmedBy = CurrentUser.Id,
				ConfirmedAt = DateTimeOffset.UtcNow, Reason = input.Remarks,
				Provenance = LibraryAcademicYearProvenance.ExplicitAssessmentContext
			});
			var dateEvidence = JsonSerializer.Serialize(new
			{
				input.SchoolChargeDate, input.SchoolDueDate, ConfirmedBy = CurrentUser.Id,
				ConfirmedAt = DateTimeOffset.UtcNow, Reason = input.Remarks
			});
			charge = LibraryCharge.CreateConfirmed(GuidGenerator.Create(), input.MemberId, memberRole,
				input.ChargeCategoryId, input.BookIssueId, input.Amount, input.SchoolChargeDate.Value,
				input.SchoolDueDate.Value, input.AcademicYearId.Value,
				LibraryAcademicYearProvenance.ExplicitAssessmentContext, yearEvidence, dateEvidence,
				input.Remarks, tenant.Id.Value);
		}
		else
		{
			EnsureChargeDates(input.ChargeDate, input.DueDate);
			charge = new LibraryCharge(
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
		}

		await chargeRepository.InsertAsync(charge, autoSave: true);

		return new GenericCreatedResponse
		{
			Id = charge.Id.ToString(),
			Message = "Library charge created successfully."
		};
	}

	public async Task<PaginatedResponse<ChargeListItemDto>> GetListAsync(int page = 1, int pageSize = 20)
	{
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = await GetReadableChargesAsync();
		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

		var offset = ((long)page - 1) * pageSize;
		List<LibraryCharge> charges = offset >= totalCount ? [] : await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((int)offset)
				.Take(pageSize));

		var items = await BuildChargeListDtosAsync(charges);

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
		var charge = await chargeRepository.FindAsync(id);
		if (charge == null)
		{
			throw LibraryException(LibraryErrorCodes.LibraryChargeNotFound, "Library charge was not found.");
		}

		await EnsureChargeReadableAsync(charge.Id);
		var category = await chargeCategoryRepository.FindAsync(charge.ChargeCategoryId);
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
			SchoolChargeDate = charge.SchoolChargeDate,
			SchoolDueDate = charge.SchoolDueDate,
			ChargeDateProvenance = charge.ChargeDateProvenance,
			DueDateProvenance = charge.DueDateProvenance,
			AcademicYearId = charge.AcademicYearId,
			AcademicYearProvenance = charge.AcademicYearProvenance,
			Currency = charge.Currency,
			FinanceSourceLinkId = charge.FinanceSourceLinkId,
			SourceVersion = charge.GetAssessmentVersion(),
			PaymentStatus = (int)charge.PaymentStatus,
			PaidDate = charge.PaidDate,
			Remarks = charge.Remarks,
			CreatedAt = charge.CreationTime,
			LastModifiedAt = charge.LastModificationTime
		};

		await EnrichChargeMemberAsync(dto, charge);
		await EnrichChargeBookAsync(dto, charge);

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Update)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid id, UpdateChargeRequest input)
	{
		await inventoryLock.AcquireAsync(tenant.Id);
		var charge = await chargeRepository.FindAsync(id);
		if (charge == null)
		{
			throw LibraryException(LibraryErrorCodes.LibraryChargeNotFound, "Library charge was not found.");
		}

		await EnsureChargeReadableAsync(charge.Id);
		var paymentStatus = input.PaymentStatus.HasValue ? (ChargePaymentStatus?)input.PaymentStatus.Value : null;
		charge.EnsureCanAmend(paymentStatus, input.PaidDate);
		if (!string.Equals(input.ExpectedSourceVersion, charge.GetAssessmentVersion(), StringComparison.Ordinal))
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged,
				"The assessment changed. Reload it before applying an amendment.");
		charge.ApplyUpdate(input.Amount, input.DueDate,
			paymentStatus,
			input.PaidDate, input.Remarks, DateTime.UtcNow, input.SchoolDueDate,
			input.SchoolDueDate.HasValue ? JsonSerializer.Serialize(new
			{
				SchoolChargeDate = charge.SchoolChargeDate, input.SchoolDueDate,
				ConfirmedBy = CurrentUser.Id, ConfirmedAt = DateTimeOffset.UtcNow, Reason = input.Remarks
			}) : null);

		await chargeRepository.UpdateAsync(charge, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = charge.Id.ToString(),
			Message = "Library charge updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		await inventoryLock.AcquireAsync(tenant.Id);
		var charge = await chargeRepository.FindAsync(id);
		if (charge == null)
		{
			throw LibraryException(LibraryErrorCodes.LibraryChargeNotFound, "Library charge was not found.");
		}

		await EnsureChargeReadableAsync(charge.Id);
		charge.EnsureCanDelete();

		await chargeRepository.DeleteAsync(charge, autoSave: true);
	}

	private async Task<List<ChargeListItemDto>> BuildChargeListDtosAsync(IReadOnlyCollection<LibraryCharge> charges)
	{
		var memberIds = charges.Select(c => c.MemberId).Distinct().ToList();
		var categoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
		var issueIds = charges.Where(c => c.BookIssueId.HasValue).Select(c => c.BookIssueId!.Value).Distinct().ToList();

		var catQuery = await chargeCategoryRepository.GetQueryableAsync();
		var categories = await AsyncExecuter.ToListAsync(catQuery.Where(c => categoryIds.Contains(c.Id)));
		var catLookup = categories.ToDictionary(c => c.Id, c => c.ChargeType);

		var studentQuery = await studentRepository.GetQueryableAsync();
		var students = await AsyncExecuter.ToListAsync(studentQuery.Where(s => memberIds.Contains(s.Id)));
		var studentLookup = students.ToDictionary(s => s.Id);

		var empQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(empQuery.Where(e => memberIds.Contains(e.Id)));
		var empLookup = employees.ToDictionary(e => e.Id);

		var issueLookup = new Dictionary<Guid, BookIssue>();
		var accessionTitleLookup = new Dictionary<string, string>();
		if (issueIds.Count > 0)
		{
			var issueQuery = await issueRepository.GetQueryableAsync();
			var issues = await AsyncExecuter.ToListAsync(issueQuery.Where(i => issueIds.Contains(i.Id)));
			issueLookup = issues.ToDictionary(i => i.Id);

			var accessionNos = issues.Select(i => i.AccessionNumber).Distinct().ToList();
			var copyQuery = await bookCopyRepository.GetQueryableAsync();
			var copies = await AsyncExecuter.ToListAsync(copyQuery.Where(c => accessionNos.Contains(c.AccessionNumber)));
			var bookIds = copies.Select(c => c.BookId).Distinct().ToList();

			var bookQuery = await bookRepository.GetQueryableAsync();
			var books = await AsyncExecuter.ToListAsync(bookQuery.Where(b => bookIds.Contains(b.Id)));
			var bookLookup = books.ToDictionary(b => b.Id, b => b.Title);

			foreach (var copy in copies)
			{
				if (bookLookup.TryGetValue(copy.BookId, out var title))
				{
					accessionTitleLookup[copy.AccessionNumber] = title;
				}
			}
		}

		return charges.Select(charge =>
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
				SchoolChargeDate = charge.SchoolChargeDate,
				SchoolDueDate = charge.SchoolDueDate,
				ChargeDateProvenance = charge.ChargeDateProvenance,
				DueDateProvenance = charge.DueDateProvenance,
				AcademicYearId = charge.AcademicYearId,
				AcademicYearProvenance = charge.AcademicYearProvenance,
				Currency = charge.Currency,
				FinanceSourceLinkId = charge.FinanceSourceLinkId,
				SourceVersion = charge.GetAssessmentVersion(),
				PaymentStatus = (int)charge.PaymentStatus,
				PaidDate = charge.PaidDate
			};

			if (studentLookup.TryGetValue(charge.MemberId, out var student))
			{
				dto.MemberName = $"{student.FirstName} {student.LastName}".Trim();
			}
			else if (empLookup.TryGetValue(charge.MemberId, out var emp))
			{
				dto.MemberName = $"{emp.FirstName} {emp.LastName}".Trim();
			}

			if (charge.BookIssueId.HasValue && issueLookup.TryGetValue(charge.BookIssueId.Value, out var issue))
			{
				dto.AccessionNo = issue.AccessionNumber;
				accessionTitleLookup.TryGetValue(issue.AccessionNumber, out var bookTitle);
				dto.BookTitle = bookTitle;
			}

			return dto;
		}).ToList();
	}

	private async Task EnrichChargeMemberAsync(ChargeDetailDto dto, LibraryCharge charge)
	{
		if (charge.MemberRole == 0)
		{
			var student = await AsyncExecuter.FirstOrDefaultAsync((await studentRepository.GetQueryableAsync())
				.Where(row => row.TenantId == tenant.Id && row.Id == charge.MemberId)
				.Select(row => new
				{
					row.FirstName, row.MiddleName, row.LastName, row.RollNo, row.GrNo, row.StandardId, row.DivisionId,
					Avatar = row.AdditionalDetail == null ? null : row.AdditionalDetail.ProfilePictureUrl,
					ContactNo = row.ContactDetail == null ? null : row.ContactDetail.PrimaryContactNo
				}));
			if (student == null)
			{
				return;
			}

			dto.MemberName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim();
			dto.Avatar = student.Avatar;
			dto.RollNo = student.RollNo?.ToString();
			dto.GrNo = student.GrNo?.ToString();
			dto.ContactNo = student.ContactNo;

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

			dto.ClassDivision = standardName != null && divisionName != null
				? $"{standardName}-{divisionName}"
				: standardName ?? divisionName;
		}
		else
		{
			var employee = await employeeRepository.FindAsync(charge.MemberId);
			if (employee == null)
			{
				return;
			}

			dto.MemberName = $"{employee.FirstName} {employee.LastName}".Trim();
			dto.Avatar = employee.ProfilePictureBlobName;
			dto.ContactNo = employee.MobileNo;
		}
	}

	private async Task EnrichChargeBookAsync(ChargeDetailDto dto, LibraryCharge charge)
	{
		if (!charge.BookIssueId.HasValue)
		{
			return;
		}

		var issue = await issueRepository.FindAsync(charge.BookIssueId.Value);
		if (issue == null)
		{
			return;
		}

		dto.AccessionNo = issue.AccessionNumber;

		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var copy = await AsyncExecuter.FirstOrDefaultAsync(copyQuery.Where(c => c.AccessionNumber == issue.AccessionNumber));
		if (copy == null)
		{
			return;
		}

		var book = await bookRepository.FindAsync(copy.BookId);
		dto.BookTitle = book?.Title;
	}

	private async Task EnsureChargeCategoryAsync(Guid chargeCategoryId)
	{
		var category = await chargeCategoryRepository.FindAsync(chargeCategoryId);
		if (category == null)
		{
			throw LibraryException(LibraryErrorCodes.ChargeCategoryNotFound, "Charge category was not found.");
		}
	}

	private async Task EnsureIssueBelongsToMemberAsync(Guid issueId, Guid memberId)
	{
		var issue = await issueRepository.FindAsync(issueId);
		if (issue == null || issue.MemberId != memberId)
		{
			throw LibraryException(LibraryErrorCodes.ActiveIssueNotFound, "Linked book issue was not found for this member.");
		}
	}

	private async Task<int> ResolveActiveMemberRoleAsync(Guid memberId)
	{
		var student = await AsyncExecuter.FirstOrDefaultAsync((await studentRepository.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && row.Id == memberId)
			.Select(row => new { Active = row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true }));
		if (student != null)
		{
			if (student.Active)
			{
				return 0;
			}

			throw LibraryException(LibraryErrorCodes.MemberInactive, "Student is not an active library member.");
		}

		var employee = await AsyncExecuter.FirstOrDefaultAsync((await employeeRepository.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && row.Id == memberId)
			.Select(row => new { Active = row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember }));
		if (employee != null)
		{
			if (employee.Active)
			{
				return 1;
			}

			throw LibraryException(LibraryErrorCodes.MemberInactive, "Staff member is not an active library member.");
		}

		throw LibraryException(LibraryErrorCodes.MemberNotFound, $"Member with id '{memberId}' was not found as student or employee.");
	}

	private static void EnsureChargeDates(DateTime chargeDate, DateTime dueDate)
	{
		if (chargeDate == default || dueDate == default || dueDate.Date < chargeDate.Date)
		{
			throw LibraryException(LibraryErrorCodes.InvalidCharge, "Charge date and due date are required, and due date cannot be before charge date.");
		}
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
