using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Domain service responsible for enrollment-related business operations,
/// including generating unique inquiry numbers per academic medium.
/// </summary>
public class StudentEnrollmentManager(
	IStudentRepository studentRepository
) : DomainService
{
	private const string InquiryPrefix = "INQ";

	/// <summary>
	/// Generates the next sequential inquiry number for the given academic medium
	/// within the current tenant.
	/// </summary>
	/// <param name="academicMediumId">The academic medium to scope the sequence to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A formatted inquiry number, e.g. <c>INQ001</c>.</returns>
	public async Task<string> GenerateInquiryNumberAsync(
		Guid academicMediumId,
		CancellationToken cancellationToken = default)
	{
		var maxSequence = await studentRepository.GetMaxInquirySequenceForMediumAsync(
			academicMediumId,
			CurrentTenant.Id,
			cancellationToken);

		return $"{InquiryPrefix}{maxSequence + 1:D3}";
	}

	public async Task ValidateFormNumberUniqueAsync(
		string formNumber,
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		bool exists = await studentRepository.AnyAsync(
			s => s.AdditionalDetail!.RegistrationFormNo == formNumber && s.AcademicYearId == academicYearId,
			cancellationToken);
		if (exists)
		{
			throw new BusinessException($"Student with form number: {formNumber} already exists.");
		}
	}
}
