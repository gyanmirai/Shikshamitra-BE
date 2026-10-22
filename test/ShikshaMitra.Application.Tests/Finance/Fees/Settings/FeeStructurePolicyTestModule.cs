using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Settings;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class FeeStructurePolicyTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		// The shared fixture covers 2025-26. Keep policy effectiveness independent of the machine date.
		context.Services.Replace(ServiceDescriptor.Singleton<IClock>(new FixtureClock()));
	}

	public sealed class FixtureClock : IClock
	{
		public DateTime Current { get; set; } = new(2025, 10, 1);
		public DateTime? AdvanceAfterNextRead { get; set; }
		public DateTime Now
		{
			get
			{
				var now = Current;
				if (AdvanceAfterNextRead.HasValue)
				{
					Current = AdvanceAfterNextRead.Value;
					AdvanceAfterNextRead = null;
				}
				return now;
			}
		}
		public DateTimeKind Kind => DateTimeKind.Unspecified;
		public bool SupportsMultipleTimezone => false;
		public DateTime Normalize(DateTime dateTime) => dateTime;
	}
}
