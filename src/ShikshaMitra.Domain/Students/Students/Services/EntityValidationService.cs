using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;

/// <summary>
/// Domain service responsible for validating student entities and related business rules.
/// Encapsulates validation logic to keep the application layer thin and maintain DDD principles.
/// </summary>
public class EntityValidationService(
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Student, Guid> studentRepository) : DomainService
{
	/// <summary>
	/// Validates that an academic medium exists in the system.
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <exception cref="BusinessException">Thrown when the academic medium does not exist</exception>
	public async Task ValidateAcademicMediumExistsAsync(Guid academicMediumId)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == academicMediumId))
		{
			throw new BusinessException(
				StudentRegistrationErrorCodes.AcademicMediumNotFound)
				.WithData("academicMediumId", academicMediumId);
		}
	}

	/// <summary>
	/// Validates that a standard exists in the system.
	/// </summary>
	/// <param name="standardId">The standard identifier</param>
	/// <exception cref="BusinessException">Thrown when the standard does not exist</exception>
	public async Task ValidateStandardExistsAsync(Guid standardId)
	{
		if (!await standardRepository.AnyAsync(x => x.Id == standardId))
		{
			throw new BusinessException(
				StudentRegistrationErrorCodes.StandardNotFound)
				.WithData("standardId", standardId);
		}
	}

	/// <summary>
	/// Validates that a student exists in the system.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <exception cref="BusinessException">Thrown when the student does not exist</exception>
	public async Task ValidateStudentExistsAsync(Guid studentId)
	{
		if (!await studentRepository.AnyAsync(x => x.Id == studentId))
		{
			throw new BusinessException("ShikshaMitra.Student:StudentNotFound")
				.WithData("studentId", studentId);
		}
	}

	/// <summary>
	/// Validates that a student is in one of the allowed statuses for a specific operation.
	/// </summary>
	/// <param name="student">The student entity to validate</param>
	/// <param name="allowedStatuses">The list of allowed statuses</param>
	/// <param name="errorCode">The localization error code to use if validation fails</param>
	/// <exception cref="BusinessException">Thrown when the student is not in an allowed status</exception>
	public void ValidateStudentStatus(
		Student student,
		IEnumerable<StudentLifeCycleStatusEnum> allowedStatuses,
		string errorCode)
	{
		if (!allowedStatuses.Contains(student.Status))
		{
			throw new BusinessException(errorCode)
				.WithData("currentStatus", student.Status);
		}
	}

	/// <summary>
	/// Validates that all sibling student IDs exist in the system.
	/// </summary>
	/// <param name="siblingStudentIds">The list of sibling student identifiers</param>
	/// <exception cref="BusinessException">Thrown when any sibling student does not exist</exception>
	public async Task ValidateSiblingStudentsExistAsync(List<Guid> siblingStudentIds)
	{
		if (siblingStudentIds == null || !siblingStudentIds.Any())
		{
			return;
		}

		foreach (var siblingId in siblingStudentIds)
		{
			if (!await studentRepository.AnyAsync(x => x.Id == siblingId))
			{
				throw new BusinessException(StudentRegistrationErrorCodes.InvalidSiblingStudent)
					.WithData("siblingId", siblingId);
			}
		}
	}

	/// <summary>
	/// Validates that the student has additional detail populated.
	/// </summary>
	/// <param name="student">The student entity to validate</param>
	/// <exception cref="BusinessException">Thrown when additional detail is missing</exception>
	public void ValidateAdditionalDetailExists(Student student)
	{
		if (student.AdditionalDetail == null)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.MissingAdditionalDetail);
		}
	}
}
