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

		var query =
			from s in studentQueryable
			join sad in additionalDetailQueryable on s.Id equals sad.Id
			where s.EducationMediumId == academicMediumId && s.TenantId == currentTenant.Id
			orderby Convert.ToInt32(sad.InquiryNo.Substring(3)) descending
			select new { LastStudentInquiryNumber = Convert.ToInt32(sad.InquiryNo.Substring(3)) };

		var lastInquiryNumber = query.FirstOrDefault();

		var nextNumber =
			lastInquiryNumber != null ? lastInquiryNumber.LastStudentInquiryNumber + 1 : 1;

		return $"INQ{nextNumber:D3}";
	}
}
