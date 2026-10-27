using System;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public partial class EmployeeApplication_Tests
{
	[Fact]
	public void Assignment_survives_submission_and_finalization_without_changing_legacy_text()
	{
		var now = DateTime.UtcNow;
		var application = CreateApplication(now);
		var id = Guid.NewGuid();
		application.SetWorkAssignment(EmployeeWorkAssignmentModes.SpecificOrganizations,
			Guid.NewGuid(), "2026-27", [id], ["CBSE - English - Primary"]);
		Submit(application, now.AddMinutes(1));
		Assert.Equal("Primary", application.DepartmentAppliedFor);
		Assert.Equal([id], application.WorkAssignmentOrganizationIds);
		Assert.Equal(["CBSE - English - Primary"], application.WorkAssignmentOrganizationLabels);
		AdvanceTo(application, EmployeeApplicationStatus.ReadyForOnboarding);
		application.MarkFinalized(Guid.NewGuid(), now.AddMinutes(2));
		Assert.Equal([id], application.WorkAssignmentOrganizationIds);
	}

	[Fact]
	public void Legacy_or_blank_does_not_infer_assignment()
	{
		var application = CreateApplication(DateTime.UtcNow);
		Assert.Null(application.WorkAssignmentMode);
		Assert.Empty(application.WorkAssignmentOrganizationIds);
		Assert.Equal("Legacy department: Primary", application.WorkAssignmentDisplay);
	}

	[Theory]
	[InlineData(EmployeeWorkAssignmentModes.SchoolWide)]
	[InlineData(EmployeeWorkAssignmentModes.DecideLater)]
	public void Agnostic_assignment_contains_no_organizations(string mode)
	{
		var application = CreateApplication(DateTime.UtcNow);
		application.SetWorkAssignment(mode, null, null, [], []);
		Assert.Empty(application.WorkAssignmentOrganizationIds);
		Assert.Null(application.WorkAssignmentAcademicYearId);
		Assert.Throws<ArgumentException>(() => application.SetWorkAssignment(mode, Guid.NewGuid(), "2026-27", [Guid.NewGuid()], ["Org"]));
	}
}
