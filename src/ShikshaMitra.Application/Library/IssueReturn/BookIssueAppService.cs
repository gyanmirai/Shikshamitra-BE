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
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Catalogue;
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
public partial class BookIssueAppService(
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Members.LibraryMember, Guid> libraryMemberRepository,
	ILibraryAccessService libraryAccess,
	LibraryMemberStatisticsReader statisticsReader,
	LibraryCirculationReadService circulationQueries,
	LibraryCatalogueLabelReader catalogueLabels,
	ILibraryLoanAppService canonicalLoans)
	: ApplicationService, IBookIssueAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	public async Task<GenericCreatedResponse> IssueAsync(IssueBookRequest input)
	{
		if (input.Checkout == null || input.AccessionNo != null || input.MemberId != Guid.Empty ||
			input.IssueDate != default || input.DueDate != default)
			throw CanonicalCommandRequired("Send only Checkout with explicit school dates, copy ID, preview revision and operation key.");
		var result = await canonicalLoans.CheckoutAsync(input.Checkout);
		return new GenericCreatedResponse { Id = result.Loan.Id.ToString(), Message = "Book issued successfully." };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	public async Task<GenericCreatedResponse> ReturnAsync(ReturnBookRequest input)
	{
		if (input.Return == null || !input.LoanId.HasValue || input.LoanId == Guid.Empty || input.AccessionNo != null ||
			input.MemberId != Guid.Empty || input.ReturnDate != default || input.ChargeCategoryId.HasValue ||
			input.Amount.HasValue || input.FineDueDate.HasValue || input.CopyStatus.HasValue || input.Remarks != null)
			throw CanonicalCommandRequired("Send only LoanId and Return with the exact copy ID, loan revision, operation key and explicit school return date.");
		var result = await canonicalLoans.ReturnAsync(input.LoanId.Value, input.Return);
		return new GenericCreatedResponse { Id = result.Loan.Id.ToString(), Message = "Book returned successfully." };
	}

	private static BusinessException CanonicalCommandRequired(string instruction) =>
		new(LibraryCirculationErrorCodes.CanonicalCommandRequired,
			"The earlier timestamp and accession-only command requires review. " + instruction);

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
		var labels = await catalogueLabels.ReadAsync();

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
			Language = labels.LanguageLabel(book) ?? string.Empty,
			LanguageCode = labels.LanguageCode(book), MaterialCode = book.MaterialCode, MaterialLabel = labels.MaterialLabel(book),
			Category = category?.Name ?? string.Empty,
			Pages = book.Pages
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)]
	public async Task<LibraryMemberDto> GetMemberAsync(Guid memberId)
	{
		memberId = await libraryAccess.EnsureMemberAsync(memberId);
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
	public Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(int page = 1, int pageSize = 20) =>
		circulationQueries.GetPageAsync(null, page, pageSize);

	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	public async Task<ReturnBookSearchDto> SearchReturnBookAsync(string accessionNo)
	{
		var copy = await FindCopyByAccessionAsync(accessionNo);
		if (copy == null)
		{
			throw LibraryException(LibraryErrorCodes.CopyNotFound, $"Book copy with accession number '{accessionNo}' was not found.");
		}

		var issue = await FindOpenIssueAsync(copy, accessionNo);

		if (issue == null)
		{
			throw LibraryException(LibraryErrorCodes.ActiveIssueNotFound, $"No active issue found for accession number '{accessionNo}'.");
		}
		await canonicalLoans.GetAsync(issue.Id);
		await libraryAccess.EnsureMemberAsync(issue.MemberId);
		EnsureConfirmedCopy(issue, copy);

		var book = await bookRepository.GetAsync(copy.BookId);
		var category = await categoryRepository.FindAsync(book.CategoryId);

		var memberDetails = await BuildReturnMemberDetailsAsync(issue);
		var labels = await catalogueLabels.ReadAsync();

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
				Language = (int?)book.Language,
				LanguageCode = labels.LanguageCode(book), LanguageLabel = labels.LanguageLabel(book),
				Edition = book.Edition,
				Category = category?.Name ?? string.Empty,
				Pages = book.Pages,
				CoverImage = book.CoverImage
			},
			MemberDetails = memberDetails
		};
	}

	private async Task<BookCopy?> FindCopyByAccessionAsync(string accessionNo)
	{
		var scan = NormalizeAccession(accessionNo);
		var normalizedAccession = scan.ToUpperInvariant();
		var scanCopyId = LibraryCopyScanCode.ReadCopyId(scan) ?? Guid.Empty;
		var copyQuery = await bookCopyRepository.GetQueryableAsync();
		var matches = await AsyncExecuter.ToListAsync(copyQuery.Where(c =>
			c.NormalizedAccessionNumber == normalizedAccession || c.Id == scanCopyId));
		if (matches.Count > 1)
			throw LibraryException(LibraryErrorCodes.DuplicateAccessionNumber,
				"This scan matches more than one copy. Review the conflicting accession before using its label.");
		var copy = matches.SingleOrDefault();
		return copy != null && (string.Equals(copy.AccessionNumber, scan, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(LibraryCopyScanCode.For(copy), scan, StringComparison.OrdinalIgnoreCase)) ? copy : null;
	}

	private async Task<LibraryMemberDto> BuildStudentMemberDtoAsync(Student student)
	{
		var classDivision = await ResolveStudentClassDivisionAsync(student);
		var statistics = (await statisticsReader.ReadAsync([student.Id]))[student.Id];

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
			Statistics = statistics
		};
	}

	private async Task<LibraryMemberDto> BuildEmployeeMemberDtoAsync(Employee employee)
	{
		var statistics = (await statisticsReader.ReadAsync([employee.Id]))[employee.Id];

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
			Statistics = statistics
		};
	}

	private async Task<ReturnBookSearchMemberDto> BuildReturnMemberDetailsAsync(BookIssue issue)
	{
		var personId = await libraryAccess.EnsureMemberAsync(issue.MemberId);
		var memberDetails = new ReturnBookSearchMemberDto { Id = personId };

		if (issue.MemberRole == 0)
		{
			var student = await studentRepository.FindAsync(personId);
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
			var employee = await employeeRepository.FindAsync(personId);
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

	private static string NormalizeAccession(string accessionNumber)
	{
		return (accessionNumber ?? string.Empty).Trim();
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
