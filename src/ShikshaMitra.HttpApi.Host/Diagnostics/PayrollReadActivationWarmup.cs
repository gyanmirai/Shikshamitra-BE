using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.RunPayroll;

namespace ShikshaMitra.Diagnostics;

/// <summary>Initializes the Run Payroll dependency graph once per API worker.</summary>
public sealed class PayrollReadActivationWarmup(IServiceScopeFactory scopes, ILogger<PayrollReadActivationWarmup> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var timer = Stopwatch.StartNew();
		try
		{
			// Resolving the graph initializes the DI factories and proxies without calling a payroll operation.
			await using var scope = scopes.CreateAsyncScope();
			_ = scope.ServiceProvider.GetRequiredService<IPayrollPreviewRunAppService>();
		}
		catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning("Run Payroll service activation warmup failed after {ElapsedMs} ms ({ExceptionType}).",
				timer.ElapsedMilliseconds, exception.GetType().Name);
			return;
		}

		logger.LogInformation("Run Payroll service activation warmed in {ElapsedMs} ms.", timer.ElapsedMilliseconds);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
