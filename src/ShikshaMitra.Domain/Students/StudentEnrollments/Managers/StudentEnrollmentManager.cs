using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentManagerNew(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	ICurrentTenant currentTenant
) : DomainService
{
	public async Task<string> GenerateInquiryNumberAsync(Guid academicMediumId)
	{
		var studentQueryable = await studentRepository.GetQueryableAsync();
		var additionalDetailQueryable = await studentAdditionalDetailRepository.GetQueryableAsync();

		// Pull candidate inquiry numbers and safely parse on client-side to avoid DB-side parse errors
		var candidateInquiryNumbers =
			(from s in studentQueryable
			 join sad in additionalDetailQueryable on s.Id equals sad.Id
			 where s.AcademicMediumId == academicMediumId && s.TenantId == currentTenant.Id
			 select sad.InquiryNo)
			.AsEnumerable(); // switch to client-side for safe parsing

		int maxNumber = 0;
		foreach (var inquiry in candidateInquiryNumbers)
		{
			if (string.IsNullOrWhiteSpace(inquiry))
			{
				continue;
			}

			// Expect format: INQ###, tolerate extra spaces
			var trimmed = inquiry.Trim();
			if (trimmed.Length >= 4 && trimmed.StartsWith("INQ", StringComparison.OrdinalIgnoreCase))
			{
				var numericPart = trimmed.Substring(3);
				if (int.TryParse(numericPart, out var n) && n > maxNumber)
				{
					maxNumber = n;
				}
			}
		}

		var nextNumber = maxNumber + 1;
		return $"INQ{nextNumber:D3}";
	}
}
