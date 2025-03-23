using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Linq;
using Volo.Abp.EntityFrameworkCore;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp;


public class EfCoreStudentEnrollmentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, StudentEnrollment, Guid>(dbContextProvider), IStudentEnrollmentRepository
{
	// public async Task<StudentEnrollment?> GetStudentEnrollmentByUserIdAsync(Guid userId)
	// {
	// 	DbSet<StudentEnrollment> studentEnrollments = await GetDbSetAsync();
	// 	return await studentEnrollments
	// 		.FirstOrDefaultAsync(e => e.UserId == userId);
	// }

	public async Task<StudentEnrollment?> GetLastStudentInquiryAsync()
	{
		DbSet<StudentEnrollment?> studentEnrollments = await GetDbSetAsync();

		var stud = await studentEnrollments
			.OrderByDescending(e => Convert.ToInt32(e.InquiryNumber.Substring(3)))
			.FirstOrDefaultAsync();

		return stud;
	}

	public async Task<StudentEnrollment> GetInquiryByNumber(string inquiryNumber)
	{
		DbSet<StudentEnrollment?> studentEnrollments = await GetDbSetAsync();

		return await studentEnrollments
			.FirstOrDefaultAsync(e => e.InquiryNumber == inquiryNumber)
			?? throw new UserFriendlyException("Inquiry not found");
	}

	public async Task<StudentEnrollment?> GetLastStudentEnrollmentAsync()
	{
		DbSet<StudentEnrollment?> studentEnrollments = await GetDbSetAsync();

		return await studentEnrollments
					.OrderByDescending(e => e.TemporaryGr)
					.FirstOrDefaultAsync();
	}
}