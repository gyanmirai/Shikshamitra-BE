using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipDraftFreshnessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly SalarySlipDraftFreshnessService _service;
	private readonly IClock _clock;

	public SalarySlipDraftFreshnessService_Tests()
	{
		_service = GetRequiredService<SalarySlipDraftFreshnessService>();
		_clock = GetRequiredService<IClock>();
	}

	[Fact]
	public void Should_Keep_Existing_Historical_Draft()
	{
		var slip = CreateDraft(_clock.Now.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture));

		_service.ShouldKeepExistingDraft(slip, hadExistingSalarySlip: true).ShouldBeTrue();
	}

	[Fact]
	public void Should_Not_Keep_New_Historical_Draft()
	{
		var slip = CreateDraft(_clock.Now.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture));

		_service.ShouldKeepExistingDraft(slip, hadExistingSalarySlip: false).ShouldBeFalse();
	}

	[Fact]
	public void Should_Not_Keep_Current_Month_Draft()
	{
		var slip = CreateDraft(_clock.Now.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture));

		_service.ShouldKeepExistingDraft(slip, hadExistingSalarySlip: true).ShouldBeFalse();
	}

	private static SalarySlip CreateDraft(string monthYear) =>
		new(
			Guid.NewGuid(),
			tenantId: null,
			Guid.NewGuid(),
			monthYear,
			new List<SalarySlipLineItem> { new("Basic", 100m) },
			new List<SalarySlipLineItem>(),
			DateTime.UtcNow);
}
