using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiPolicyPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Policy_contracts_and_write_guards_hold_on_postgres()
	{
		using var factory = new PayrollPostgresTestFactory();
		var suite = new EsiPolicyApi_Tests(factory);
		await suite.Policy_versions_change_preview_and_template_save_binds_the_policy();
		await suite.Retries_conflicts_overlaps_and_archived_assignments_are_explicit();
		await suite.Selection_catalogue_does_not_grant_policy_management_and_is_tenant_isolated();
		await new CompensationEsiPreviewApi_Tests(factory)
			.Save_rejects_changed_or_missing_policy_versions_without_creating_a_revision_and_allows_refresh_and_retry();
	}
}
