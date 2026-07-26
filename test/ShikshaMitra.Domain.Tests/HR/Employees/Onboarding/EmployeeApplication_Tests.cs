using System;
using System.Collections.Generic;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeApplication_Tests
{
	[Fact]
	public void Submit_Sets_Applied_Status_And_Blocks_Duplicate_Submission()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);

		Submit(application, now);

		Assert.Equal(EmployeeApplicationStatus.Applied, application.Status);
		Assert.Equal(now, application.SubmittedAt);

		Assert.Throws<BusinessException>(() => Submit(application, now.AddMinutes(1)));
	}

	[Fact]
	public void Submit_Rejects_Expired_Token()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now.AddDays(-31));

		var exception = Assert.Throws<BusinessException>(() => Submit(application, now));

		Assert.Equal(HRErrorCodes.EmployeeApplicationTokenExpired, exception.Code);
	}

	[Fact]
	public void RegenerateInvitationToken_Allows_Expired_Unsubmitted_Invitation()
	{
		var issuedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(issuedAt);
		var regeneratedAt = issuedAt.AddDays(31);

		application.RegenerateInvitationToken(
			"replacement-hash",
			"replacement-token",
			regeneratedAt.AddDays(30),
			regeneratedAt);

		Assert.Equal("replacement-hash", application.TokenHash);
		Assert.Equal(EmployeeApplicationStatus.InvitationSent, application.Status);
		Assert.Null(application.SubmittedAt);
	}

	[Fact]
	public void RegenerateInvitationToken_Rejects_Submitted_Application_Without_Mutating_It()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		Submit(application, now.AddMinutes(1));
		var originalHash = application.TokenHash;
		var originalSubmittedAt = application.SubmittedAt;

		var exception = Assert.Throws<BusinessException>(() =>
			application.RegenerateInvitationToken(
				"replacement-hash",
				"replacement-token",
				now.AddDays(30),
				now.AddMinutes(2)));

		Assert.Equal(HRErrorCodes.EmployeeApplicationInvalidStatusTransition, exception.Code);
		Assert.Equal(originalHash, application.TokenHash);
		Assert.Equal(originalSubmittedAt, application.SubmittedAt);
		Assert.Equal(EmployeeApplicationStatus.Applied, application.Status);
	}

	[Theory]
	[InlineData(EmployeeApplicationStatus.Rejected)]
	[InlineData(EmployeeApplicationStatus.Withdrawn)]
	public void Submit_Rejects_Closed_Status(EmployeeApplicationStatus status)
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		application.ChangeStatus(status, "Closed by HR");

		var exception = Assert.Throws<BusinessException>(() => Submit(application, now));

		Assert.Equal(HRErrorCodes.EmployeeApplicationInvalidStatusTransition, exception.Code);
	}

	[Fact]
	public void ChangeStatus_Does_Not_Finalize_Application()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		Submit(application, now);

		var exception = Assert.Throws<BusinessException>(() =>
			application.ChangeStatus(EmployeeApplicationStatus.Finalized, null));

		Assert.Equal(HRErrorCodes.EmployeeApplicationInvalidStatusTransition, exception.Code);
	}

	[Theory]
	[InlineData(EmployeeApplicationStatus.Screening, EmployeeApplicationStatus.Applied)]
	[InlineData(EmployeeApplicationStatus.Interviewing, EmployeeApplicationStatus.Screening)]
	[InlineData(EmployeeApplicationStatus.Offered, EmployeeApplicationStatus.Interviewing)]
	[InlineData(EmployeeApplicationStatus.ReadyForOnboarding, EmployeeApplicationStatus.Offered)]
	public void ChangeStatus_Rejects_Backward_Workflow_Transitions(
		EmployeeApplicationStatus current,
		EmployeeApplicationStatus target)
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		Submit(application, now);
		AdvanceTo(application, current);

		var exception = Assert.Throws<BusinessException>(() =>
			application.ChangeStatus(target, "Move backward"));

		Assert.Equal(HRErrorCodes.EmployeeApplicationInvalidStatusTransition, exception.Code);
		Assert.Equal(current, application.Status);
	}

	[Theory]
	[InlineData(EmployeeApplicationStatus.Rejected)]
	[InlineData(EmployeeApplicationStatus.Withdrawn)]
	public void ChangeStatus_Closed_Statuses_Are_Terminal(EmployeeApplicationStatus terminalStatus)
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		Submit(application, now);
		application.ChangeStatus(terminalStatus, "Closed");

		var exception = Assert.Throws<BusinessException>(() =>
			application.ChangeStatus(EmployeeApplicationStatus.Screening, "Reopen"));

		Assert.Equal(HRErrorCodes.EmployeeApplicationInvalidStatusTransition, exception.Code);
		Assert.Equal(terminalStatus, application.Status);
	}

	[Theory]
	[InlineData(EmployeeApplicationStatus.Applied)]
	[InlineData(EmployeeApplicationStatus.Screening)]
	[InlineData(EmployeeApplicationStatus.Interviewing)]
	[InlineData(EmployeeApplicationStatus.Offered)]
	[InlineData(EmployeeApplicationStatus.ReadyForOnboarding)]
	public void ChangeStatus_Rejects_Workflow_Progress_Before_Candidate_Submission(
		EmployeeApplicationStatus status)
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);

		var exception = Assert.Throws<BusinessException>(() =>
			application.ChangeStatus(status, "Advanced by HR"));

		Assert.Equal(HRErrorCodes.EmployeeApplicationSubmissionRequired, exception.Code);
		Assert.Contains("must submit", exception.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(EmployeeApplicationStatus.InvitationSent, application.Status);
	}

	[Theory]
	[InlineData(EmployeeApplicationStatus.Rejected)]
	[InlineData(EmployeeApplicationStatus.Withdrawn)]
	public void ChangeStatus_Allows_Closing_Unsubmitted_Application(EmployeeApplicationStatus status)
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);

		application.ChangeStatus(status, "Closed by HR");

		Assert.Equal(status, application.Status);
	}

	[Fact]
	public void MarkFinalized_Rejects_Unsubmitted_Application()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);

		var exception = Assert.Throws<BusinessException>(() =>
			application.MarkFinalized(Guid.NewGuid(), now.AddDays(1)));

		Assert.Equal(HRErrorCodes.EmployeeApplicationSubmissionRequired, exception.Code);
		Assert.Null(application.FinalizedEmployeeId);
	}

	[Fact]
	public void MarkFinalized_Links_Employee_And_Blocks_Second_Finalization()
	{
		var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
		var application = CreateApplication(now);
		Submit(application, now);
		var employeeId = Guid.NewGuid();

		application.MarkFinalized(employeeId, now.AddDays(1));

		Assert.Equal(EmployeeApplicationStatus.Finalized, application.Status);
		Assert.Equal(employeeId, application.FinalizedEmployeeId);

		Assert.Throws<BusinessException>(() => application.MarkFinalized(Guid.NewGuid(), now.AddDays(2)));
	}

	private static EmployeeApplication CreateApplication(DateTime issuedAt) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"EA-20260612100000-ABC123",
			"token-hash",
			null,
			issuedAt.AddDays(30),
			issuedAt,
			"Riya",
			null,
			"Shah",
			"riya@example.com",
			"9876543210",
			"Teacher",
			"Primary");

	private static void Submit(EmployeeApplication application, DateTime submittedAt) =>
		application.Submit(
			submittedAt,
			new EmployeeApplicationPersonalInfoSnapshot(
				"Riya",
				null,
				null,
				"Shah",
				"riya@example.com",
				"9876543210",
				null,
				new DateOnly(1996, 1, 1),
				1,
				"Hindu",
				0,
				"General",
				0,
				null,
				true,
				"Primary",
				"Teacher",
				"B.Ed",
				null,
				"12 School Road",
				"Near Park",
				null,
				new EmployeeApplicationLocationSnapshot("India", "IN"),
				new EmployeeApplicationLocationSnapshot("Gujarat", "GJ"),
				"Ahmedabad",
				"380001"),
			null,
			null,
			new List<WorkExperience>(),
			null,
			null);

	private static void AdvanceTo(EmployeeApplication application, EmployeeApplicationStatus target)
	{
		foreach (var status in new[]
		         {
			         EmployeeApplicationStatus.Screening,
			         EmployeeApplicationStatus.Interviewing,
			         EmployeeApplicationStatus.Offered,
			         EmployeeApplicationStatus.ReadyForOnboarding
		         })
		{
			if (status > target)
			{
				return;
			}

			application.ChangeStatus(status, "Advance");
		}
	}
}
