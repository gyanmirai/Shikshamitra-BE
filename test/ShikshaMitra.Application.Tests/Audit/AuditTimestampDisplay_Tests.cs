using System;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.HR.Audit;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Audit;

public class AuditTimestampDisplay_Tests
{
	[Fact]
	public void Hr_Audit_Projection_Should_Display_In_Institution_Time_And_Preserve_Utc()
	{
		var occurredAt = new DateTime(2026, 6, 8, 17, 51, 5, DateTimeKind.Utc);
		var auditEvent = new HrAuditEvent(
			Guid.NewGuid(),
			null,
			Guid.NewGuid(),
			"payroll_policy",
			Guid.NewGuid(),
			"Operations Payroll Policy",
			"edited",
			Guid.NewGuid(),
			"Admin",
			occurredAt,
			"Edited payroll policy Operations Payroll Policy");

		var dto = HrAuditTrailProjection.ToDto(auditEvent);

		dto.OccurredAtUtc.ShouldBe(occurredAt);
		dto.OccurredAtDisplay.ShouldBe("08 Jun 2026, 11:21 PM");
	}

	[Fact]
	public void Finance_Audit_Projection_Should_Display_In_Institution_Time_And_Preserve_Utc()
	{
		var occurredAt = new DateTime(2026, 6, 8, 17, 51, 5, DateTimeKind.Utc);
		var auditEvent = new FinanceAuditEvent(
			Guid.NewGuid(),
			null,
			Guid.NewGuid(),
			"collections",
			"fee_payment",
			Guid.NewGuid(),
			"REC-001",
			"payment",
			Guid.NewGuid(),
			"Test Student",
			"101",
			"5",
			Guid.NewGuid(),
			"Admin",
			"Admin",
			occurredAt,
			"Fee payment received");

		var dto = FinanceAuditTrailProjection.ToDto(auditEvent);

		dto.OccurredAtUtc.ShouldBe(occurredAt);
		dto.OccurredAtDisplay.ShouldBe("08 Jun 2026, 11:21 PM");
	}
}
