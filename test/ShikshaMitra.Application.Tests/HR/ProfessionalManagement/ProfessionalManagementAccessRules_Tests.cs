using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementAccessRules_Tests
{
	[Fact]
	public void CanSelfReview_Should_Only_Allow_Assigned_Employee()
	{
		var employeeId = Guid.NewGuid();
		var assignment = CreateAssignment(employeeId);

		ProfessionalManagementAccessRules.CanSelfReview(employeeId, assignment).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanSelfReview(Guid.NewGuid(), assignment).ShouldBeFalse();
		ProfessionalManagementAccessRules.CanSelfReview(null, assignment).ShouldBeFalse();
	}

	[Fact]
	public void CanReviewEmployee_Should_Allow_Configured_Reviewers_Only()
	{
		var reportingManagerId = Guid.NewGuid();
		var hodId = Guid.NewGuid();
		var principalId = Guid.NewGuid();
		var assignment = CreateAssignment(
			employeeId: Guid.NewGuid(),
			reportingManagerId,
			hodId,
			principalId);

		ProfessionalManagementAccessRules.CanReviewEmployee(reportingManagerId, assignment).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanReviewEmployee(hodId, assignment).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanReviewEmployee(principalId, assignment).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanReviewEmployee(Guid.NewGuid(), assignment).ShouldBeFalse();
		ProfessionalManagementAccessRules.CanReviewEmployee(null, assignment).ShouldBeFalse();
	}

	[Fact]
	public void CanUploadEvidence_Should_Respect_Permission_Path_And_Record_Role()
	{
		var employeeId = Guid.NewGuid();
		var reviewerId = Guid.NewGuid();
		var assignment = CreateAssignment(employeeId, reportingManagerEmployeeId: reviewerId);

		ProfessionalManagementAccessRules.CanUploadEvidence(
			employeeId,
			assignment,
			hasSelfReviewPermission: true,
			hasReviewEmployeesPermission: false).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanUploadEvidence(
			reviewerId,
			assignment,
			hasSelfReviewPermission: false,
			hasReviewEmployeesPermission: true).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanUploadEvidence(
			reviewerId,
			assignment,
			hasSelfReviewPermission: true,
			hasReviewEmployeesPermission: false).ShouldBeFalse();
		ProfessionalManagementAccessRules.CanUploadEvidence(
			employeeId,
			assignment,
			hasSelfReviewPermission: false,
			hasReviewEmployeesPermission: true).ShouldBeFalse();
		ProfessionalManagementAccessRules.CanUploadEvidence(
			Guid.NewGuid(),
			assignment,
			hasSelfReviewPermission: true,
			hasReviewEmployeesPermission: true).ShouldBeFalse();
	}

	[Fact]
	public void CanAddObservation_Should_Require_Current_Reviewer_To_Match_Observer()
	{
		var reviewerId = Guid.NewGuid();
		var assignment = CreateAssignment(Guid.NewGuid(), reportingManagerEmployeeId: reviewerId);

		ProfessionalManagementAccessRules.CanAddObservation(reviewerId, assignment, reviewerId).ShouldBeTrue();
		ProfessionalManagementAccessRules.CanAddObservation(reviewerId, assignment, Guid.NewGuid()).ShouldBeFalse();
		ProfessionalManagementAccessRules.CanAddObservation(Guid.NewGuid(), assignment, reviewerId).ShouldBeFalse();
	}

	private static PerformanceReviewAssignment CreateAssignment(
		Guid employeeId,
		Guid? reportingManagerEmployeeId = null,
		Guid? hodEmployeeId = null,
		Guid? principalEmployeeId = null) =>
		new(
			Guid.NewGuid(),
			tenantId: Guid.NewGuid(),
			cycleId: Guid.NewGuid(),
			templateId: Guid.NewGuid(),
			employeeId,
			reportingManagerEmployeeId,
			hodEmployeeId,
			principalEmployeeId);
}
