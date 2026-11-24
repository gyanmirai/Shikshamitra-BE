using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>A shared parent contact is a family-match signal, not a duplicate child.</summary>
[RemoteService(false)]
public class AdmissionInquiryStudentDuplicateGuard(
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionApplicant, Guid> applicants) : ApplicationService, ITransientDependency
{
	public async Task EnsureDistinctAsync(AdmissionInquiry inquiry, string name, string phone)
	{
		var applicantQuery = await applicants.GetQueryableAsync();
		var current = inquiry.ApplicantId.HasValue ? await applicants.FindAsync(inquiry.ApplicantId.Value) : null;
		var birthDate = current?.DateOfBirth;
		var candidates = await AsyncExecuter.ToListAsync((await inquiries.GetQueryableAsync())
			.Where(candidate => candidate.Id != inquiry.Id && candidate.AdmissionCycleId == inquiry.AdmissionCycleId &&
				(candidate.NormalizedPhone == phone || inquiry.FamilyId.HasValue && candidate.FamilyId == inquiry.FamilyId))
			.Select(candidate => new
			{
				candidate.ApplicantName,
				DateOfBirth = applicantQuery.Where(applicant => applicant.Id == candidate.ApplicantId)
					.Select(applicant => applicant.DateOfBirth).FirstOrDefault()
			}));
		// Match the existing inquiry review rule: a missing DOB cannot clear a same-name match.
		// No candidate identifiers or contact details are exposed outside the caller's scope.
		if (candidates.Any(candidate => IsSameIdentity(name, birthDate, candidate.ApplicantName, candidate.DateOfBirth)))
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateStudent);
		// Family membership and parent access still require the existing explicit sibling confirmation.
	}

	internal async Task EnsureDistinctSiblingAsync(AdmissionInquiry inquiry, AdmissionApplicant target, AdmissionApplicant source)
	{
		var sourceName = string.Join(" ", new[] { source.FirstName, source.MiddleName, source.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
		if (IsSameIdentity(inquiry.ApplicantName, target.DateOfBirth, sourceName, source.DateOfBirth))
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateStudent);
		await EnsureDistinctAsync(inquiry, inquiry.ApplicantName, inquiry.NormalizedPhone);
	}

	internal static bool IsSameIdentity(string name, DateTime? birthDate, string candidateName, DateTime? candidateBirthDate) =>
		Normalize(name) == Normalize(candidateName) &&
		(!birthDate.HasValue || !candidateBirthDate.HasValue || birthDate.Value.Date == candidateBirthDate.Value.Date);

	private static string Normalize(string name) => string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
