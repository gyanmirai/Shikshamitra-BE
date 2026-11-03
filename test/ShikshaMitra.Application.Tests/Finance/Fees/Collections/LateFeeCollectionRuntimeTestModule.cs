using System;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Collections;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LateFeeCollectionRuntimeTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		// These fixtures seed due dates and historical payments with DateTime.UtcNow.
		// Keep the application clock on the same calendar date after local midnight.
		Configure<AbpClockOptions>(options => options.Kind = DateTimeKind.Utc);
	}
}
