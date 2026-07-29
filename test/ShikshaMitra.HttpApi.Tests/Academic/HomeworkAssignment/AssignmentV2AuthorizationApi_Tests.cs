using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentV2AuthorizationApi_Tests
{
	[Fact]
	public void Assignment_V2_Actions_Should_Use_The_Canonical_Learning_Task_Permissions()
	{
		var view = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View;
		var add = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add;
		var delete = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Delete;
		var expectedPolicies = new Dictionary<string, string>
		{
			[nameof(AssignmentV2AppService.CreateAsync)] = add,
			[nameof(AssignmentV2AppService.GetListAsync)] = view,
			[nameof(AssignmentV2AppService.GetAsync)] = view,
			[nameof(AssignmentV2AppService.UpdateAsync)] = add,
			[nameof(AssignmentV2AppService.DeleteAsync)] = delete,
			[nameof(AssignmentV2AppService.ChangeStatusAsync)] = add,
			[nameof(AssignmentV2AppService.GetDashboardAsync)] = view,
			[nameof(AssignmentV2AppService.SendReminderAsync)] = add,
			[nameof(AssignmentV2AppService.GetHistoryAsync)] = view,
		};

		foreach (var (methodName, expectedPolicy) in expectedPolicies)
		{
			var method = typeof(AssignmentV2AppService).GetMethod(methodName);
			method.ShouldNotBeNull();
			var authorize = method.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
			authorize.ShouldNotBeNull();
			authorize.Policy.ShouldBe(expectedPolicy);
		}
	}

	[Fact]
	public void Feedback_And_Assessment_Methods_Should_Require_Canonical_Permissions()
	{
		var view = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View;
		var add = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add;
		var expectedPolicies = new Dictionary<(System.Type Type, string Method), string>
		{
			[(typeof(AssignmentFeedbackAppService), nameof(AssignmentFeedbackAppService.GetSubmissionsAsync))] = view,
			[(typeof(AssignmentFeedbackAppService), nameof(AssignmentFeedbackAppService.GiveFeedbackAsync))] = add,
			[(typeof(AssignmentFeedbackAppService), nameof(AssignmentFeedbackAppService.UpdateStatusAsync))] = add,
			[(typeof(StudentSubmissionAppService), nameof(StudentSubmissionAppService.UploadAssignmentSubmissionAsync))] = add,
			[(typeof(StudentSubmissionAppService), nameof(StudentSubmissionAppService.GetAssignmentAssessmentsAsync))] = view,
			[(typeof(RubricAssessmentAppService), nameof(RubricAssessmentAppService.UpsertEvaluationAsync))] = add,
			[(typeof(RubricAssessmentAppService), nameof(RubricAssessmentAppService.GetEvaluationsAsync))] = view,
			[(typeof(RubricAssessmentAppService), nameof(RubricAssessmentAppService.SubmitEvaluationAsync))] = add,
			[(typeof(RubricAssessmentAppService), nameof(RubricAssessmentAppService.ApproveEvaluationAsync))] = add,
		};

		foreach (var ((type, methodName), expectedPolicy) in expectedPolicies)
		{
			var method = type.GetMethod(methodName);
			method.ShouldNotBeNull();
			method.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Policy.ShouldBe(expectedPolicy);
		}
	}

	[Fact]
	public void Assignment_V2_App_Service_Should_Not_Expose_A_Second_Conventional_Api()
	{
		var remoteService = typeof(AssignmentV2AppService)
			.GetCustomAttribute<RemoteServiceAttribute>(inherit: true);

		remoteService.ShouldNotBeNull();
		remoteService.IsEnabled.ShouldBeFalse();
	}
}
