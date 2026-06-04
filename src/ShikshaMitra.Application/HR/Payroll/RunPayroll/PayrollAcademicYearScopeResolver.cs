using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAcademicYearScopeResolver(
	IAcademicYearRepository academicYearRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<Guid?> ResolveAsync(Guid? academicYearId, int year, int month)
	{
		if (!academicYearId.HasValue)
		{
			return null;
		}

		var query = await academicYearRepository.GetQueryableAsync();
		query = query.Where(x => x.Id == academicYearId.Value);
		var academicYear = await asyncExecuter.FirstOrDefaultAsync(query)
			?? throw new BusinessException(HRErrorCodes.PayrollAcademicYearNotFound)
				.WithData("academicYearId", academicYearId.Value);

		var monthStart = new DateTime(year, month, 1);
		var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		if (academicYear.StartDate > monthEnd || academicYear.EndDate < monthStart)
		{
			throw new BusinessException(HRErrorCodes.PayrollMonthOutsideAcademicYear)
				.WithData("academicYearId", academicYearId.Value)
				.WithData("month", $"{year:D4}-{month:D2}")
				.WithData("academicYearStart", academicYear.StartDate)
				.WithData("academicYearEnd", academicYear.EndDate);
		}

		return academicYear.Id;
	}
}
