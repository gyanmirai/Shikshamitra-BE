using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;
public class StudentQueryService(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository) : DomainService
{
	public async Task<IQueryable<StudentQueryResult>> GetStudentWithDetailsQueryAsync(
		Expression<Func<Student, bool>>? studentFilter = null,
		Expression<Func<StudentAdditionalDetail, bool>>? additionalDetailFilter = null,
		Expression<Func<StudentContactDetail, bool>>? contactDetailFilter = null
	)
	{
		var studentQueryable = await studentRepository.GetQueryableAsync();
		var additionalDetailQueryable = await studentAdditionalDetailRepository.GetQueryableAsync();
		var contactDetailQueryable = await studentContactDetailRepository.GetQueryableAsync();

		if (studentFilter != null)
			studentQueryable = studentQueryable.Where(studentFilter);

		if (additionalDetailFilter != null)
			additionalDetailQueryable = additionalDetailQueryable.Where(additionalDetailFilter);

		if (contactDetailFilter != null)
			contactDetailQueryable = contactDetailQueryable.Where(contactDetailFilter);

		var query = studentQueryable
			.Join(
				additionalDetailQueryable,
				s => s.Id,
				sad => sad.Id,
				(s, sad) => new { Student = s, AdditionalDetail = sad }
			)
			.Join(
				contactDetailQueryable,
				x => x.Student.Id,
				scd => scd.Id,
				(x, scd) =>
					new StudentQueryResult
					{
						Student = x.Student,
						AdditionalDetail = x.AdditionalDetail,
						ContactDetail = scd,
					}
			);

		return query;
	}
}
