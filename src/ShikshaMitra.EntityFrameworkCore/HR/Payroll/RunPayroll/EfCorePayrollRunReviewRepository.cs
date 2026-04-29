using System;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class EfCorePayrollRunReviewRepository
	: EfCoreRepository<ShikshaMitraDbContext, PayrollRunReview, Guid>
{
	public EfCorePayrollRunReviewRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}
}
