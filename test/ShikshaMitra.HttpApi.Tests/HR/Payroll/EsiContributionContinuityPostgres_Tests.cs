using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiContributionContinuityPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Finalized_and_paid_evidence_preserves_contributions_on_postgres()
	{
		using var factory = new PayrollPostgresTestFactory();
		var suite = new EsiContributionContinuityApi_Tests(factory);
		await suite.Above_ceiling_salary_keeps_contributions_after_finalized_unlocked_payroll(SalarySlipStatus.Finalized);
		await suite.Above_ceiling_salary_keeps_contributions_after_finalized_unlocked_payroll(SalarySlipStatus.Paid);
	}
}