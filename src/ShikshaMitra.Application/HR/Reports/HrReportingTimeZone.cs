using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace ShikshaMitra.HR.Reports;

public class HrReportingTimeZone(ISettingProvider settings) : ITransientDependency
{
	public async Task<string> GetAsync()
	{
		var value = await settings.GetOrNullAsync("ShikshaMitra.HR.Reporting.TimeZone") ?? "Asia/Kolkata";
		try { return TimeZoneInfo.FindSystemTimeZoneById(value).Id; }
		catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
		{ throw HrReportingEngine.Invalid("The school's reporting timezone is invalid. Ask the school administrator to correct it."); }
	}
}