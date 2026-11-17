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
		if (candidates.Any(candidate => Normalize(candidate.ApplicantName) == Normalize(name) &&
			(!birthDate.HasValue || !candidate.DateOfBirth.HasValue || birthDate.Value.Date == candidate.DateOfBirth.Value.Date)))
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateStudent);
		// Family membership and parent access still require the existing explicit sibling confirmation.
	}

	private static string Normalize(string name) => string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
