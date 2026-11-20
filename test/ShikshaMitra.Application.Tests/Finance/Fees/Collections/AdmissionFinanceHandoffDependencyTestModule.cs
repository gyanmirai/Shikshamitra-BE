using System;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp.Modularity;

namespace ShikshaMitra.Finance.Fees.Collections;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class AdmissionFinanceHandoffDependencyTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		// Linking an admission account must not activate the independent payment,
		// pricing or collection-form graphs. Their own operations resolve them on demand.
		foreach (var type in new[] { typeof(CollectFeeWorkflow), typeof(StudentFeeStructureAppService),
			typeof(StudentFeeDetailsQueryService), typeof(LateFeeBalanceService),
			typeof(ShikshaMitra.Domain.Finance.Policies.CollectionPolicyResolver) })
			context.Services.AddTransient(type, _ => throw new InvalidOperationException(
				$"Unrelated collection dependency activated: {type.Name}"));
	}
}
