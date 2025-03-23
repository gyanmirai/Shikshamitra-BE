using System;
using Volo.Abp.Domain.Repositories;
using System.Threading.Tasks;


namespace ShikshaMitra.Students.StudentEnrollments;

public interface IStudentEnrollmentRepository : IRepository<StudentEnrollment, Guid>
{
	Task<StudentEnrollment> GetInquiryByNumber(string inquiryNumber);
	Task<StudentEnrollment?> GetLastStudentEnrollmentAsync();

	// public Task<StudentEnrollment?> GetStudentEnrollmentByUserIdAsync(Guid userId);
	Task<StudentEnrollment?> GetLastStudentInquiryAsync();
}