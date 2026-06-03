using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task TimeHrPhaseAsync(string name, Func<Task> action)
	{
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		await action();
		stopwatch.Stop();
		_logger.LogInformation("[HRSeeder] {PhaseName} completed in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
	}

	private async Task<T> TimeHrPhaseAsync<T>(string name, Func<Task<T>> action)
	{
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await action();
		stopwatch.Stop();
		_logger.LogInformation("[HRSeeder] {PhaseName} completed in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
		return result;
	}
}
