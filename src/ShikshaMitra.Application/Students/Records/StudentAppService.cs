using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Students.Records;

/// <summary>
/// Application service for student record management following ABP and DDD best practices
/// </summary>
[RemoteService(false)]
public class StudentAppService(
	IRepository<Student, Guid> studentRepository,
	IAsyncQueryableExecuter asyncExecuter,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentAppService
{
	public async Task<PagedResultDto<StudentDto>> GetStudentsByDivisionAsync(
		Guid divisionId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		try
		{
			// Validate input parameters
			ValidateInputParameters(skipCount, maxResultCount);

			// Build the query with filters
			var queryable = await studentRepository.GetQueryableAsync();
			var filteredQuery = ApplyFilters(queryable, divisionId, searchTerm);
			filteredQuery = await studentScopeAccessService.ApplyCurrentUserScopeAsync(filteredQuery);

			// Get total count for pagination before applying skip/take
			var totalCount = await asyncExecuter.CountAsync(filteredQuery);

			// Apply sorting for consistent pagination results
			var sortedQuery = ApplySorting(filteredQuery);

			// Apply pagination
			var students = await asyncExecuter.ToListAsync(
				sortedQuery
					.Skip(skipCount)
					.Take(maxResultCount)
			);

			// Map entities to DTOs
			var studentDtos = students.Select(MapToDto).ToList();

			return new PagedResultDto<StudentDto>(totalCount, studentDtos);
		}
		catch (ArgumentException)
		{
			// Re-throw argument exceptions with their original validation messages
			throw;
		}
		catch (Exception ex)
		{
			// Log the exception for diagnostics
			Logger.LogError(ex, "Error retrieving students");

			// Wrap in BusinessException with context for proper error handling
			throw new BusinessException(
				"ShikshaMitra:StudentRetrievalFailed",
				"An error occurred while retrieving students. Please try again or contact support if the issue persists."
			)
			.WithData("DivisionId", divisionId.ToString() ?? "null")
			.WithData("SkipCount", skipCount)
			.WithData("MaxResultCount", maxResultCount)
			.WithData("SearchTerm", searchTerm ?? "null");
		}
	}

	/// <summary>
	/// Validates input parameters for pagination
	/// </summary>
	private static void ValidateInputParameters(int skipCount, int maxResultCount)
	{
		if (skipCount < 0)
		{
			throw new ArgumentException(
				"Skip count cannot be negative.",
				nameof(skipCount)
			);
		}

		if (maxResultCount <= 0)
		{
			throw new ArgumentException(
				"Max result count must be greater than zero.",
				nameof(maxResultCount)
			);
		}

		if (maxResultCount > 1000)
		{
			throw new ArgumentException(
				"Max result count cannot exceed 1000 for performance reasons.",
				nameof(maxResultCount)
			);
		}
	}

	/// <summary>
	/// Applies filters to the student query based on division, search term, and tenant
	/// Following ABP multi-tenancy and soft-delete best practices
	/// </summary>
	private IQueryable<Student> ApplyFilters(
		IQueryable<Student> query,
		Guid? divisionId,
		string? searchTerm
	)
	{
		// Apply multi-tenant filter (ABP best practice)
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(s => s.TenantId == CurrentTenant.Id);
		}

		// Filter by division if provided
		// Allows null or empty Guid to return all students
		if (divisionId.HasValue && divisionId.Value != Guid.Empty)
		{
			query = query.Where(s => s.DivisionId == divisionId.Value);
		}

		// Apply search filter across multiple fields: FirstName, MiddleName, LastName, GrNo, RollNo
		// Using Contains for case-insensitive search (translated to LIKE by EF Core)
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(s =>
				(s.FirstName != null && s.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(s.MiddleName != null && s.MiddleName.ToLower().Contains(normalizedSearchTerm)) ||
				(s.LastName != null && s.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(s.GrNo.HasValue && s.GrNo.ToString().Contains(normalizedSearchTerm)) ||
				(s.GrNumber != null && s.GrNumber.ToLower().Contains(normalizedSearchTerm)) ||
				(s.RollNo.HasValue && s.RollNo.ToString().Contains(normalizedSearchTerm))
			);
		}

		// Apply soft-delete filter (ABP best practice)
		query = query.Where(s => !s.IsDeleted);

		return query;
	}

	/// <summary>
	/// Applies consistent sorting for predictable pagination
	/// Sorts by RollNo, then FirstName, then LastName
	/// </summary>
	private static IQueryable<Student> ApplySorting(IQueryable<Student> query)
	{
		return query
			.OrderBy(s => s.RollNo ?? int.MaxValue)
			.ThenBy(s => s.FirstName ?? string.Empty)
			.ThenBy(s => s.LastName ?? string.Empty);
	}

	/// <summary>
	/// Maps Student entity to StudentDto with safe null handling
	/// Follows DDD principles by accessing aggregate parts (AdditionalDetail, ContactDetail)
	/// </summary>
	private StudentDto MapToDto(Student student)
	{
		if (student == null)
		{
			throw new ArgumentNullException(nameof(student), "Student entity cannot be null during mapping.");
		}

		try
		{
			return new StudentDto
			{
				StudentId = student.Id,
				GrNo = student.GrNo,
				GrNumber = student.GrNumber,
				RollNo = student.RollNo,
				InquiryNo = student.AdditionalDetail?.InquiryNo ?? string.Empty,
				FormNo = student.AdditionalDetail?.RegistrationFormNo,
				StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
				FirstName = student.FirstName,
				MiddleName = student.MiddleName,
				LastName = student.LastName,
				AcademicYearId = student.AcademicYearId,
				AcademicBoardId = student.AcademicBoardId,
				AcademicMediumId = student.AcademicMediumId,
				StandardId = student.StandardId ?? Guid.Empty,
				Standard = string.Empty, // To be populated by navigation property if needed
				DivisionId = student.DivisionId,
				Division = string.Empty, // To be populated by navigation property if needed
				DateOfBirth = student.AdditionalDetail?.DateOfBirth,
				Age = CalculateAge(student.AdditionalDetail?.DateOfBirth),
				DateOfInquiry = student.AdditionalDetail?.DateOfInquiry,
				Gender = student.AdditionalDetail?.Gender,
				PrimaryContactNo = student.ContactDetail?.PrimaryContactNo,
				SecondaryContactNo = student.ContactDetail?.SecondaryContactNo,
				Email = student.ContactDetail?.Email,
				Address1 = student.ContactDetail?.Address1,
				Address2 = student.ContactDetail?.Address2,
				Address3 = student.ContactDetail?.Address3,
				City = student.ContactDetail?.City,
				State = student.ContactDetail?.State,
				Country = student.ContactDetail?.Country,
				PinCode = student.ContactDetail?.Pincode,
				PreviousSchool = student.AdditionalDetail?.PreviousSchool,
				InquiryPreferenceId = student.AdditionalDetail?.InquiryPreferenceId
			};
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error mapping student data");
			throw new BusinessException(
				"ShikshaMitra:StudentMappingFailed",
				"An error occurred while processing student data."
			).WithData("StudentId", student.Id);
		}
	}

	/// <summary>
	/// Builds full name from name parts, handling nulls and whitespace
	/// </summary>
	private static string BuildFullName(string? firstName, string? middleName, string? lastName)
	{
		return string.Join(" ", new[] { firstName, middleName, lastName }
			.Where(n => !string.IsNullOrWhiteSpace(n)))
			.Trim();
	}

	/// <summary>
	/// Calculates age from date of birth
	/// Returns formatted string like "25 years" or null if DOB is not available
	/// </summary>
	private static string? CalculateAge(DateTimeOffset? dateOfBirth)
	{
		if (!dateOfBirth.HasValue)
		{
			return null;
		}

		var today = DateTimeOffset.UtcNow;
		var age = today.Year - dateOfBirth.Value.Year;

		// Adjust if birthday hasn't occurred this year yet
		if (dateOfBirth.Value.Date > today.AddYears(-age).Date)
		{
			age--;
		}

		return age >= 0 ? $"{age} years" : null;
	}
}
