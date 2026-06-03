using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollBasisPreviewResolver(
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<PayrollBasisPreviewResolver> logger) : ITransientDependency
{
	public PayrollBasisDaysResult Resolve(
		PayrollPolicy policy,
		int year,
		int month,
		IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate,
		List<string> issueCodes)
	{
		try
		{
			return PayrollBasisDays.ResolvePolicy(
				policy,
				year,
				month,
				workingUnitsByDate.Values.Sum(),
				workingUnitsByDate);
		}
		catch (InvalidOperationException ex) when (
			policy.DayCalculationType == DayCalculationType.ManualDays &&
			ex.Message.StartsWith("ManualDays must be set", StringComparison.Ordinal))
		{
			logger.LogWarning(
				ex,
				"Payroll policy {PayrollPolicyId} is ManualDays but has no manual basis value for {Year}-{Month:00}. Falling back to calendar days for preview.",
				policy.Id,
				year,
				month);
			issueCodes.Add(IssueCodes.PayrollPolicyConfiguration);
			return new PayrollBasisDaysResult(
				DateTime.DaysInMonth(year, month),
				DayCalculationType.TotalMonthDays,
				UsedTenantDefault: true,
				policy.SaturdayWorkPolicy,
				workingUnitsByDate);
		}
	}

	public async Task<List<Holiday>> GetIncludedWorkCalendarHolidaysAsync(Guid workCalendarPolicyId, int year, int month)
	{
		var start = new DateTime(year, month, 1);
		var end = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var query = await workCalendarHolidayRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x =>
			x.WorkCalendarPolicyId == workCalendarPolicyId &&
			x.IsIncluded &&
			x.ToDate >= start &&
			x.FromDate <= end));
		return rows
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName)
			.Select(x => x.ToCalendarHoliday(currentTenant.Id))
			.ToList();
	}
}
