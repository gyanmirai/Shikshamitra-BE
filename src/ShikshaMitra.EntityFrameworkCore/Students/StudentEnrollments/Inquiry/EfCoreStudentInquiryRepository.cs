using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Linq;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using System.Threading.Tasks;

using ShikshaMitra.Students.StudentEnrollments.Inquiry;


public class EfCoreStudentInquiryRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, StudentInquiry, Guid>(dbContextProvider),IStudentInquiryRepository
{
	public async Task<StudentInquiry?> GetLastStudentInquiryAsync()
	{
		DbSet<StudentInquiry?> studentInquiries = await GetDbSetAsync();

		return await studentInquiries
			.OrderByDescending(e => Convert.ToInt32(e.InquiryNumber.Substring(3)))
			.FirstOrDefaultAsync();
	}
}	