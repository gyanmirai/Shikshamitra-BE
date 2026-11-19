using System;
using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class EsiConfigurationPostgresFactAttribute : FactAttribute
{
	public EsiConfigurationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ESI_TEST_POSTGRES")))
			Skip = "Requires a separate empty loopback payroll_tests_* database in ESI_TEST_POSTGRES.";
	}
}
public class EsiConfigurationPostgresTestFactory : PayrollPostgresTestFactory
{
	protected override string ConnectionVariable => "ESI_TEST_POSTGRES";
}
public class EsiConfigurationPostgres_Tests
{
	[EsiConfigurationPostgresFact]
	public async Task Configuration_commands_are_atomic_under_concurrent_postgres_transactions()
	{
		using var factory = new EsiConfigurationPostgresTestFactory();
		var suite = new EsiStatutoryApi_Tests(factory);
		await suite.Concurrent_saves_accept_exactly_one_reviewed_version();
		await suite.Save_waits_for_the_payroll_lock_fence_and_preserves_its_result();
		await suite.Save_uses_current_month_and_retries_are_idempotent();
		await suite.Save_invalidates_current_future_unlocked_but_preserves_locked_and_earlier_snapshots();
	}
}
