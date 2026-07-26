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
	public void Hr_Audit_Projection_Should_Preserve_Date_Like_Names_And_Compare_Known_Fields_By_Business_Semantics()
	{
		const string changedFieldsJson = """
		[
		  {
		    "field": "name",
		    "oldValue": "2026-01-01",
		    "newValue": "Jan 1 2026"
		  },
		  {
		    "field": "effectiveFrom",
		    "oldValue": "2026-01-01T00:00:00Z",
		    "newValue": "2025-12-31T16:00:00-08:00"
		  },
		  {
		    "field": "effectiveTo",
		    "oldValue": "2026-01-31T00:00:00Z",
		    "newValue": "2026-01-31T08:00:00+08:00"
		  },
		  {
		    "field": "timeIn",
		    "oldValue": "2026-01-01T08:00:00Z",
		    "newValue": "2026-01-01T00:00:00-08:00"
		  }
		]
		""";
		var auditEvent = new HrAuditEvent(
			Guid.NewGuid(),
			null,
			Guid.NewGuid(),
			"payroll_policy",
			Guid.NewGuid(),
			"2026-01-01",
			"edited",
			Guid.NewGuid(),
			"Admin",
			DateTime.UtcNow,
			"Edited a date-like policy name.",
			changedFieldsJson: changedFieldsJson);

		var dto = HrAuditTrailProjection.ToDto(auditEvent);

		dto.ChangedFieldCount.ShouldBe(2);
		dto.ChangedFields[0].Field.ShouldBe("name");
		dto.ChangedFields[0].OldValue.ShouldBe("2026-01-01");
		dto.ChangedFields[0].NewValue.ShouldBe("Jan 1 2026");
		dto.ChangedFields[1].Field.ShouldBe("effectiveFrom");
		dto.ChangedFields[1].OldValue.ShouldBe("2026-01-01T00:00:00Z");
		dto.ChangedFields[1].NewValue.ShouldBe("2025-12-31T16:00:00-08:00");
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
