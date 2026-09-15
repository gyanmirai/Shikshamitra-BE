using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Transport;

public class TransportReportPlanGuard_Tests
{
	private readonly TransportReportPlanGuard _guard = new();
	private readonly Guid _mediumId = Guid.NewGuid();

	[Fact]
	public void Should_Accept_Report_Specific_Filters()
	{
		var plan = Plan("route-wise-student-allocation");
		plan.Query.RouteId = Guid.NewGuid();
		plan.Query.MemberType = TransportMemberType.Student;
		plan.Query.Status = nameof(TransportMemberStatus.Active);

		Should.NotThrow(() => _guard.Validate(plan, _mediumId, null));
	}

	[Fact]
	public void Should_Reject_Unsupported_Report()
	{
		var exception = Should.Throw<BusinessException>(() =>
			_guard.Validate(Plan("gps-tracking"), _mediumId, null));

		exception.Code.ShouldBe(TransportErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Filter_That_Report_Does_Not_Use()
	{
		var plan = Plan("transport-performance-dashboard");
		plan.Query.VehicleId = Guid.NewGuid();

		Should.Throw<BusinessException>(() => _guard.Validate(plan, _mediumId, null))
			.Code.ShouldBe(TransportErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Status_From_Another_Report_Family()
	{
		var plan = Plan("vehicle-master");
		plan.Query.Status = nameof(TransportApplicationStatus.Approved);

		Should.Throw<BusinessException>(() => _guard.Validate(plan, _mediumId, null))
			.Code.ShouldBe(TransportErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Context_Or_Date_Tampering()
	{
		var plan = Plan("monthly-transportation-summary");
		plan.Query.AcademicMediumId = Guid.NewGuid();
		plan.Query.FromDate = new DateTime(2026, 9, 10);
		plan.Query.ToDate = new DateTime(2026, 9, 1);

		Should.Throw<BusinessException>(() => _guard.Validate(plan, _mediumId, null))
			.Code.ShouldBe(TransportErrorCodes.ReportRequestInvalid);
	}

	private TransportAiReportExecutionPlanDto Plan(string reportKey) => new()
	{
		ReportKey = reportKey,
		Format = "csv",
		Query = new TransportReportListRequestDto
		{
			AcademicMediumId = _mediumId,
			PageNumber = 1,
			PageSize = 25
		}
	};
}
