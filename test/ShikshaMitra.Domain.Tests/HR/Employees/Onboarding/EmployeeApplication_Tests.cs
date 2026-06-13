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
			"Riya",
			null,
			"Shah",
			"riya@example.com",
			"9876543210",
			null,
			"Teacher",
			"Primary",
			new DateOnly(1996, 1, 1),
			1,
			"Hindu",
			0,
			"General",
			0,
			null,
			"B.Ed",
			"Classroom management",
			"12 School Road",
			"Near Park",
			null,
			null,
			null,
			"Ahmedabad",
			"380001",
			new List<WorkExperience>(),
			null,
			null);
}
