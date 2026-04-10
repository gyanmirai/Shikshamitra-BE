using System;
using Xunit;

namespace ShikshaMitra.Infrastructure.Traits;

/// <summary>
/// Opt-in fact for hosted smoke tests.
/// Tests are skipped unless APITEST_ENABLE_SMOKE=true (or 1).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SmokeFactAttribute : FactAttribute
{
	public SmokeFactAttribute()
	{
		var value = Environment.GetEnvironmentVariable("APITEST_ENABLE_SMOKE");
		var enabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);

		if (!enabled)
		{
			Skip = "Smoke tests are opt-in. Set APITEST_ENABLE_SMOKE=true to run.";
		}
	}
}
