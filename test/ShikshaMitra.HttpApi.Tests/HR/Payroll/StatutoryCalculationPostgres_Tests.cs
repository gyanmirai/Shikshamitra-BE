using System.Threading.Tasks;

namespace ShikshaMitra.HR.Payroll;

public class StatutoryCalculationPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Calculation_configuration_and_coverage_persist_on_postgres()
	{
		using var factory = new PayrollPostgresTestFactory();
		var suite = new StatutoryCalculationApi_Tests(factory);
		await suite.Calculation_changes_require_the_exact_administrator_capability();
		await suite.Rate_overrides_are_scheduled_documented_and_idempotent();
		await suite.Pf_school_coverage_requires_permission_scope_evidence_and_stale_write_protection();
	}
}
