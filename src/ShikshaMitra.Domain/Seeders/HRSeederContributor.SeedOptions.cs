using Microsoft.Extensions.Configuration;
using System;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private bool ShouldSeedSampleLeaveScenarios() =>
		_configuration.GetValue(
			"ShikshaMitra:Seeders:HR:SeedSampleLeaveScenarios",
			_configuration.GetValue("ShikshaMitra:Seeders:HR:SeedSampleApprovedLeaves", true));

	private bool ShouldSeedDemoPolicyScenarios() =>
		_configuration.GetValue(SeedDemoPolicyScenariosConfigKey, true);

	private bool ShouldSeedPerformanceManagementDemoData()
		=> ResolveShouldSeedPerformanceManagementDemoData(_configuration);

	internal static bool ResolveShouldSeedPerformanceManagementDemoData(IConfiguration configuration)
	{
		var profile = ShikshaMitraSeederProfile.Normalize(
			configuration[ShikshaMitraSeederProfile.ContextPropertyName] ??
			configuration["ShikshaMitra:Seeders:Profile"]);

		return configuration.GetValue(
			SeedPerformanceManagementDemoDataConfigKey,
			ShikshaMitraSeederProfile.Demo.Contains(profile));
	}

	private int PastAttendanceMonthsToSeed() =>
		ResolvePastAttendanceMonthsToSeed(_configuration);

	internal static int ResolvePastAttendanceMonthsToSeed(IConfiguration configuration) =>
		Math.Clamp(
			configuration.GetValue(
				PastAttendanceMonthsToSeedConfigKey,
				configuration.GetValue(
					PastPayrollMonthsToSeedConfigKey,
					DefaultPastAttendanceMonthsForProfile(configuration))),
			0,   // 0 = skip attendance/payroll seeding entirely (used in tests)
			12);

	private static int DefaultPastAttendanceMonthsForProfile(IConfiguration configuration)
	{
		var profile = ShikshaMitraSeederProfile.Normalize(
			configuration[ShikshaMitraSeederProfile.ContextPropertyName] ??
			configuration["ShikshaMitra:Seeders:Profile"]);

		return profile switch
		{
			ShikshaMitraSeederProfile.FastDemo => 1,
			ShikshaMitraSeederProfile.Minimal => 0,
			_ => 3
		};
	}

	private bool ShouldSeedRichAssets()
	{
		var profile = ShikshaMitraSeederProfile.Normalize(
			_configuration[ShikshaMitraSeederProfile.ContextPropertyName] ??
			_configuration["ShikshaMitra:Seeders:Profile"]);
		var defaultValue = profile == ShikshaMitraSeederProfile.FullDemo;
		return _configuration.GetValue(SeedRichAssetsConfigKey, defaultValue);
	}
}
