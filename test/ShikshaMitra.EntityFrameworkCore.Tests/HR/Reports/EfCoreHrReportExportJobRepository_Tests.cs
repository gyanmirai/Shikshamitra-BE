using System;
using ShikshaMitra.EntityFrameworkCore.HR.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class EfCoreHrReportExportJobRepository_Tests
{
	[Fact]
	public void Requested_Date_Boundaries_Should_Be_Utc_For_Npgsql_Timestamptz_Parameters()
	{
		var unspecified = new DateTime(2026, 7, 26, 9, 30, 0, DateTimeKind.Unspecified);
		var utc = EfCoreHrReportExportJobRepository.NormalizeRequestedBoundary(unspecified);

		utc.Kind.ShouldBe(DateTimeKind.Utc);
		utc.Ticks.ShouldBe(unspecified.Ticks);

		var unboundedFrom = EfCoreHrReportExportJobRepository.ResolveRequestedBoundary(null, lowerBound: true);
		var unboundedTo = EfCoreHrReportExportJobRepository.ResolveRequestedBoundary(null, lowerBound: false);
		unboundedFrom.ShouldBe(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
		unboundedFrom.Kind.ShouldBe(DateTimeKind.Utc);
		unboundedTo.ShouldBe(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
		unboundedTo.Kind.ShouldBe(DateTimeKind.Utc);
	}
}
