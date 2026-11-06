using System;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollPreviewRun_Tests
{
	private static PayrollPreviewRun Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"2026-08", Guid.NewGuid(), "scope", 50, DateTime.UtcNow);

	[Fact]
	public void Progress_is_monotonic_and_bounded()
	{
		var run = Create();
		run.Start(Guid.NewGuid(), DateTime.UtcNow);
		run.ReportProgress(1, 25);
		run.ReportProgress(1, 10);
		run.CompletedEmployees.ShouldBe(25);
		run.ReportProgress(1, 100);
		run.CompletedEmployees.ShouldBe(50);
	}

	[Fact]
	public void New_previews_are_versioned_and_legacy_previews_default_to_zero()
	{
		var run = Create();
		run.CalculationVersion.ShouldBe(1);
		run.HasCurrentCalculationVersion.ShouldBeTrue();
		run.ExtraProperties.Remove(PayrollPreviewRun.CalculationVersionProperty);
		run.CalculationVersion.ShouldBe(0);
		run.HasCurrentCalculationVersion.ShouldBeFalse();
	}

	[Fact]
	public void Expired_worker_cannot_report_progress_or_retry_replacement()
	{
		var run = Create();
		run.Start(Guid.NewGuid(), DateTime.UtcNow);
		run.Start(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(16));
		Should.Throw<BusinessException>(() => run.ReportProgress(1, 50)).Code.ShouldBe(HRErrorCodes.PayrollPreviewStale);
		Should.Throw<BusinessException>(() => run.Retry(1, "stale")).Code.ShouldBe(HRErrorCodes.PayrollPreviewStale);
		run.Status.ShouldBe("RUNNING");
	}

	[Fact]
	public void Recovery_is_bounded_to_three_attempts()
	{
		var run = Create();
		for (var attempt = 1; attempt <= 3; attempt++)
		{
			run.Start(Guid.NewGuid(), DateTime.UtcNow);
			if (attempt < 3) run.Retry(attempt, HRErrorCodes.PayrollPreviewStale);
		}
		Should.Throw<BusinessException>(() => run.Retry(3, HRErrorCodes.PayrollPreviewStale));
		run.Fail(HRErrorCodes.PayrollPreviewStale, DateTime.UtcNow);
		run.Status.ShouldBe("SUPERSEDED");
		Should.Throw<BusinessException>(() => run.Start(Guid.NewGuid(), DateTime.UtcNow));
	}

	[Fact]
	public void Published_generation_is_terminal()
	{
		var run = Create();
		run.Start(Guid.NewGuid(), DateTime.UtcNow);
		run.Complete("{}", DateTime.UtcNow);
		run.Fail(HRErrorCodes.PayrollPreviewFailed, DateTime.UtcNow);
		run.Status.ShouldBe("SUCCEEDED");
		run.CompletedEmployees.ShouldBe(50);
		Should.Throw<BusinessException>(() => run.Complete("changed", DateTime.UtcNow));
		Should.Throw<BusinessException>(() => run.Start(Guid.NewGuid(), DateTime.UtcNow));
	}

	[Fact]
	public void Review_identifies_the_exact_generation()
	{
		var generation = Guid.NewGuid();
		var review = new PayrollRunReview(Guid.NewGuid(), Guid.NewGuid(), "08-2026", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, generation);
		review.PreviewRunId.ShouldBe(generation);
		var replacement = Guid.NewGuid();
		review.Refresh(Guid.NewGuid(), DateTime.UtcNow, replacement);
		review.PreviewRunId.ShouldBe(replacement);
	}
}
