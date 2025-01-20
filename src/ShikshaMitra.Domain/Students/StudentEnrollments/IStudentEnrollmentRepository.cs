using System;
using Volo.Abp.Domain.Repositories;
using System.Threading.Tasks;


namespace ShikshaMitra.Students.StudentEnrollments;

public interface IStudentEnrollmentRepository : IRepository<StudentEnrollment, Guid>
{
	// public Task<StudentEnrollment?> GetStudentEnrollmentByUserIdAsync(Guid userId);

	Task<StudentEnrollment?> GetLastStudentInquiryAsync();
}