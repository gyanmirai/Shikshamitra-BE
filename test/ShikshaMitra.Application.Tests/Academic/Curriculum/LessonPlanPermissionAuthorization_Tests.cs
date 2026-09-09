using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanPermissionAuthorization_Tests
{
	[Theory]
	[InlineData(nameof(LessonPlanAppService.GetListAsync))]
	[InlineData(nameof(LessonPlanAppService.GetTimelineAsync))]
	[InlineData(nameof(LessonPlanAppService.GetChapterProgressAsync))]
	[InlineData(nameof(LessonPlanAppService.GetAsync))]
	[InlineData(nameof(LessonPlanAppService.GetTeacherDiariesAsync))]
	[InlineData(nameof(LessonPlanAppService.GetTeacherDiaryAsync))]
	public void Read_Operations_Should_Require_Lesson_Plan_View(string methodName)
	{
		GetPolicy(methodName).ShouldBe(
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View);
	}

	[Theory]
	[InlineData(nameof(LessonPlanAppService.CreateAsync))]
	[InlineData(nameof(LessonPlanAppService.UpdateAsync))]
	[InlineData(nameof(LessonPlanAppService.SubmitAsync))]
	[InlineData(nameof(LessonPlanAppService.BulkSubmitAsync))]
	[InlineData(nameof(LessonPlanAppService.ApproveAsync))]
	[InlineData(nameof(LessonPlanAppService.RejectAsync))]
	[InlineData(nameof(LessonPlanAppService.RequestChangesAsync))]
	[InlineData(nameof(LessonPlanAppService.CreateHomeworkFromLessonPlanAsync))]
	[InlineData(nameof(LessonPlanAppService.CreateTeacherDiaryAsync))]
	[InlineData(nameof(LessonPlanAppService.UpdateTeacherDiaryAsync))]
	[InlineData(nameof(LessonPlanAppService.SubmitTeacherDiaryAsync))]
	public void Write_Operations_Should_Require_Lesson_Plan_Add(string methodName)
	{
		GetPolicy(methodName).ShouldBe(
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add);
	}

	private static string? GetPolicy(string methodName)
	{
		var method = typeof(LessonPlanAppService)
			.GetMethods()
			.Single(candidate => candidate.Name.Equals(methodName, StringComparison.Ordinal));

		return method
			.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.Single()
			.Policy;
	}
}
