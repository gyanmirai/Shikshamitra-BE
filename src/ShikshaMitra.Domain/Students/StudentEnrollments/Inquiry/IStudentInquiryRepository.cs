using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

using ShikshaMitra.Students.StudentEnrollments.Inquiry;

public interface IStudentInquiryRepository : IRepository<StudentInquiry, Guid>
{
	 Task<StudentInquiry?> GetLastStudentInquiryAsync();
}