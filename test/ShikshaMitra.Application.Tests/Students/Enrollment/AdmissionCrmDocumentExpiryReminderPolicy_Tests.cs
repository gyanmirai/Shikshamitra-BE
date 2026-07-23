using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmDocumentExpiryReminderPolicy_Tests
{
	[Fact]
	public void Should_Include_Current_Documents_Inside_Their_Configured_Reminder_Window()
	{
		var now = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
		var assignment = CreateAssignment(30);
		var leadId = Guid.NewGuid();
		var superseded = CreateDocument(assignment, leadId, 1, now.AddDays(90));
		superseded.MarkSuperseded(now);
		var expiring = CreateDocument(assignment, leadId, 2, now.AddDays(15));

		var leadIds = AdmissionCrmDocumentExpiryReminderPolicy.GetExpiringLeadIds(
			[assignment],
			[expiring, superseded],
			now);

		leadIds.ShouldContain(leadId);
	}

	[Fact]
	public void Should_Exclude_Expired_Rejected_And_Unconfigured_Documents()
	{
		var now = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
		var configuredAssignment = CreateAssignment(30);
		var unconfiguredAssignment = CreateAssignment(null);
		var rejected = CreateDocument(configuredAssignment, Guid.NewGuid(), 1, now.AddDays(5));
		rejected.Reject(Guid.NewGuid(), "Details do not match.", now);
		var expired = CreateDocument(configuredAssignment, Guid.NewGuid(), 1, now.AddDays(-1));
		var unconfigured = CreateDocument(unconfiguredAssignment, Guid.NewGuid(), 1, now.AddDays(5));

		var leadIds = AdmissionCrmDocumentExpiryReminderPolicy.GetExpiringLeadIds(
			[configuredAssignment, unconfiguredAssignment],
			[rejected, expired, unconfigured],
			now);

		leadIds.ShouldBeEmpty();
	}

	private static AdmissionCrmDocumentChecklistAssignment CreateAssignment(int? daysBefore)
	{
		var assignmentId = Guid.NewGuid();
		var template = new AdmissionCrmDocumentChecklistTemplateDto
		{
			Id = Guid.NewGuid(),
			AcademicYearId = Guid.NewGuid(),
			Name = "Identity documents",
			Requirements =
			[
				new AdmissionCrmDocumentRequirementDto
				{
					Key = "income-certificate",
					Name = "Income certificate",
					ValidityDateRequired = true,
					ExpiryReminderDaysBefore = daysBefore
				}
			]
		};
		return new AdmissionCrmDocumentChecklistAssignment(
			assignmentId,
			Guid.NewGuid(),
			template.AcademicYearId,
			Guid.NewGuid(),
			Guid.NewGuid(),
			template.Id,
			1,
			JsonSerializer.Serialize(template, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
			DateTime.UtcNow);
	}

	private static AdmissionCrmApplicantDocument CreateDocument(
		AdmissionCrmDocumentChecklistAssignment assignment,
		Guid leadId,
		int version,
		DateTime validUntil) =>
		new(
			Guid.NewGuid(),
			assignment.TenantId,
			assignment.AcademicYearId,
			leadId,
			assignment.Id,
			"income-certificate",
			version,
			$"blob-{Guid.NewGuid():N}",
			"income.pdf",
			"application/pdf",
			128,
			Guid.NewGuid().ToString("N"),
			validUntil,
			DateTime.UtcNow);
}
