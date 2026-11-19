using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiConfiguration_Tests
{
	[Fact]
	public void Off_retains_registration_but_has_no_calculation_context()
	{
		var version = new EsiPayrollConfiguration(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1),
			false, "ESI-1", new(2026, 4, 1), "request");
		version.ToCalculationEstablishment().ShouldBeNull();
		version.RegistrationNumber.ShouldBe("ESI-1");
		version.CoveredSince.ShouldBe(new DateTime(2026, 4, 1));
	}
	[Theory]
	[InlineData(2)]
	[InlineData(30)]
	public void Effective_date_must_be_first_of_month(int day) =>
		Should.Throw<BusinessException>(() => new EsiPayrollConfiguration(Guid.NewGuid(), null,
			new(2026, 9, day), false, null, null, "request"));

	[Fact]
	public void Cancellation_preserves_the_version_and_treatments()
	{
		var component = Guid.NewGuid();
		var version = new EsiPayrollConfiguration(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1),
			true, "ESI-1", new(2026, 4, 1), "request",
			treatments: new Dictionary<Guid, StatutoryWageTreatment> { [component] = StatutoryWageTreatment.Included });
		version.ToCalculationEstablishment()!.Id.ShouldBe(version.Id);
		version.Cancel("Schedule corrected");
		version.IsCancelled.ShouldBeTrue();
		version.ToCalculationEstablishment().ShouldBeNull();
		version.GetTreatments()[component].ShouldBe(StatutoryWageTreatment.Included);
	}
	[Fact]
	public void Compensation_variance_prevents_finalization() =>
		PayrollLockPreconditionGuard.HasBlockingLockIssue("[\"COMPENSATION_TARGET_VARIANCE\"]").ShouldBeTrue();
}
