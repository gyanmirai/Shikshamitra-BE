using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class EsiHistoricalPostgresFactAttribute : FactAttribute
{
	public EsiHistoricalPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ESI_HISTORICAL_TEST_POSTGRES")))
			Skip = "Requires a separate empty loopback payroll_tests_* database in ESI_HISTORICAL_TEST_POSTGRES.";
	}
}

public class EsiHistoricalPostgresFactory : PayrollPostgresTestFactory
{
	protected override string ConnectionVariable => "ESI_HISTORICAL_TEST_POSTGRES";
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		base.ConfigureTestDatabase(services);
		services.Replace(ServiceDescriptor.Singleton<IClock>(new EsiSetupTestClock()));
	}
}

public class EsiHistoricalConfigurationPostgres_Tests
{
	[EsiHistoricalPostgresFact]
	public async Task Historical_commands_preserve_protected_payroll_and_atomic_configuration_history()
	{
		using var factory = new EsiHistoricalPostgresFactory();
		using var suite = new EsiHistoricalConfigurationApi_Tests(factory);
		await suite.June_initial_setup_and_unlocked_updates_preserve_order_history_and_retries();
		await suite.Historical_save_invalidates_unlocked_reviews_but_keeps_earlier_periods();
		foreach (var protection in new[] { "locked", "finalized", "paid" })
			await suite.Historical_setup_cannot_change_protected_payroll_or_later_continuity(protection, "2026-06");
		await suite.Historical_setup_cannot_change_protected_payroll_or_later_continuity("locked", "2026-12");
		await suite.Historical_save_waits_for_finalization_and_rolls_back_failed_invalidation();
	}
}
