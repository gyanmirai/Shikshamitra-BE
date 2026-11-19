using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.HomeworkAssignment;
using System.Linq;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicInsightsContract_Tests
{
	[Fact]
	public void Latest_Chapter_Progress_Must_Replace_History_Per_Chapter_And_Division()
	{
		var chapter = Guid.NewGuid(); var division = Guid.NewGuid();
		var old = new ChapterProgress(chapter, division); old.UpdateProgress(0);
		var current = new ChapterProgress(chapter, division); current.UpdateProgress(100);
		typeof(ChapterProgress).GetProperty(nameof(ChapterProgress.CreationTime))!.SetValue(old, DateTime.UtcNow.AddDays(-1));
		typeof(ChapterProgress).GetProperty(nameof(ChapterProgress.CreationTime))!.SetValue(current, DateTime.UtcNow);
		AcademicInsightsTeachingReader.LatestProgressIds(new[] { old, current }.AsQueryable()).Single().ShouldBe(current.Id);
	}

	[Fact]
	public void Lesson_Owners_Must_Include_Own_And_Reporting_Teachers_Without_Their_Peer()
	{
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var division = Guid.NewGuid(); var own = Guid.NewGuid(); var reportee = Guid.NewGuid();
		LessonPlan Plan(Guid teacher, Guid classroom) => new(Guid.NewGuid(), tenant, year, teacher, Guid.NewGuid(), classroom, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "Lesson", 20);
		var plans = new[] { Plan(own, division), Plan(reportee, division), Plan(Guid.NewGuid(), division), Plan(Guid.NewGuid(), Guid.NewGuid()) }.AsQueryable();
		LessonPlanAppService.FilterInsightsOwners(plans, [own, reportee], []).Count().ShouldBe(2);
		LessonPlanAppService.FilterInsightsOwners(plans, [own, reportee], [division]).Count().ShouldBe(3);
	}

	[Fact]
	public void Assignment_Parent_With_A_Foreign_Target_Must_Not_Expose_One_Allowed_Schedule()
	{
		var division = Guid.NewGuid(); var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid(); var stage = Guid.NewGuid();
		var assignment = new AssignmentEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Task", "", Guid.NewGuid(), SubmissionType.OnlineText, 10,
			academicYearId: year, educationBoardId: board, educationMediumId: medium, educationMediumStageId: stage);
		assignment.DivisionSchedules.Add(new(Guid.NewGuid(), assignment.Id, division, null, DateTime.UtcNow));
		assignment.DivisionSchedules.Add(new(Guid.NewGuid(), assignment.Id, Guid.NewGuid(), null, DateTime.UtcNow));
		var scope = new UserScopeContext { AcademicYearIds = [year], DivisionIds = [division] };
		AssignmentV2AppService.ApplyAssignmentScope(new[] { assignment }.AsQueryable(), scope).ShouldBeEmpty();
		scope.WildcardDivisionStandardIds.Add(assignment.StandardId);
		AssignmentV2AppService.ApplyAssignmentScope(new[] { assignment }.AsQueryable(), scope).Count().ShouldBe(1);
	}
	[Theory]
	[InlineData(0, 1)]
	[InlineData(5, 6)]
	[InlineData(6, 0)]
	public void Timetable_Days_Use_DotNet_Numbering_Including_Sunday(int source, int expected) =>
		AcademicInsightsTeachingReader.ToDayOfWeek(source).ShouldBe(expected);
	[Fact]
	public void Empty_Context_Must_Be_Rejected()
	{
		var type = typeof(IAcademicDashboardAppService).Assembly.GetType("ShikshaMitra.Academic.Dashboard.AcademicInsightsInput");
		type.ShouldNotBeNull();
		var input = Activator.CreateInstance(type!);
		var errors = new List<ValidationResult>();
		Validator.TryValidateObject(input!, new ValidationContext(input!), errors, true).ShouldBeFalse();
		errors.Count.ShouldBe(3);
	}

	[Fact]
	public void Missing_Scope_And_Neighbor_Division_Must_Not_Grant_Access()
	{
		var type = typeof(AcademicDashboardAppService).Assembly.GetType("ShikshaMitra.Academic.Dashboard.AcademicInsightsScopePolicy");
		type.ShouldNotBeNull();
		var method = type!.GetMethod("CanReadDivision");
		method.ShouldNotBeNull();
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid(); var stage = Guid.NewGuid();
		var standard = Guid.NewGuid(); var division = Guid.NewGuid();
		var scope = new UserScopeContext();
		bool Allowed(Guid selectedDivision) => (bool)method!.Invoke(null, [scope, year, board, medium, stage, standard, selectedDivision])!;
		Allowed(division).ShouldBeFalse();
		scope.DivisionIds.Add(division);
		Allowed(division).ShouldBeTrue();
		Allowed(Guid.NewGuid()).ShouldBeFalse();
		scope.WildcardDivisionStandardIds.Add(standard);
		Allowed(Guid.NewGuid()).ShouldBeTrue();
	}

	[Fact]
	public void Ancestor_Wildcards_Must_Stay_Inside_Their_Exact_Boundary()
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid();
		var scope = new UserScopeContext { StandardIds = [Guid.NewGuid()], BoardIds = [board], MediumIds = [medium] };
		AcademicInsightsScopePolicy.HasWholeMedium(scope, year, board, medium).ShouldBeFalse();
		scope.WildcardMediumBoardIds.Add(board);
		AcademicInsightsScopePolicy.HasWholeMedium(scope, year, board, medium).ShouldBeTrue();
		AcademicInsightsScopePolicy.HasWholeMedium(scope, year, Guid.NewGuid(), medium).ShouldBeFalse();
		scope.WildcardBoardAcademicYearIds.Add(year);
		AcademicInsightsScopePolicy.HasWholeMedium(scope, year, Guid.NewGuid(), Guid.NewGuid()).ShouldBeTrue();
		AcademicInsightsScopePolicy.HasWholeMedium(scope, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).ShouldBeFalse();
	}

	[Fact]
	public async Task Denied_Feature_Must_Not_Invoke_Its_Query_Or_Return_Data()
	{
		var type = typeof(AcademicInsightsAppService).Assembly.GetType("ShikshaMitra.Academic.Dashboard.AcademicInsightsSectionAccess");
		type.ShouldNotBeNull();
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync("feature.View").Returns(false);
		var invoked = false;
		Func<Task<AcademicInsightSectionDto>> read = () => { invoked = true; return Task.FromResult(new AcademicInsightSectionDto { Metrics = [new("secret", "Secret", 42)] }); };
		var task = (Task<AcademicInsightSectionDto?>)type!.GetMethod("ReadAsync")!.Invoke(null, [checker, "feature.View", read])!;
		(await task).ShouldBeNull();
		invoked.ShouldBeFalse();
	}

	[Fact]
	public async Task Feature_Revocation_Must_Be_Observed_On_The_Next_Read()
	{
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync("feature.View").Returns(true, false);
		var count = 0;
		Task<AcademicInsightSectionDto> Read() { count++; return Task.FromResult(new AcademicInsightSectionDto { Key = "lesson" }); }
		(await AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", Read)).ShouldNotBeNull();
		(await AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", Read)).ShouldBeNull();
		count.ShouldBe(1);
	}

	[Theory]
	[InlineData("Parent", true)]
	[InlineData("Student", true)]
	[InlineData("Parent", false)]
	[InlineData("Student", false)]
	public async Task Portal_Account_Must_Use_Verified_Personal_Path_Even_With_Dashboard_Grant(string role, bool dashboardGranted)
	{
		var type = typeof(AcademicInsightsAppService).Assembly.GetType("ShikshaMitra.Academic.Dashboard.AcademicInsightsEntryAccess");
		type.ShouldNotBeNull();
		var staffCalled = false;
		Func<Task<AcademicInsightsDto>> staff = () => { staffCalled = true; return Task.FromResult(new AcademicInsightsDto { Role = "admin" }); };
		Func<Task<AcademicInsightsDto>> verifiedPersonal = () => Task.FromResult(new AcademicInsightsDto { Role = role.ToLowerInvariant() });
		var task = (Task<AcademicInsightsDto>)type!.GetMethod("ReadAsync")!.Invoke(null, [new[] { role }, dashboardGranted, verifiedPersonal, staff])!;
		(await task).Role.ShouldBe(role.ToLowerInvariant());
		staffCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task Staff_Without_Dashboard_Grant_Must_Not_Fall_Back_To_A_Child_Association()
	{
		var personalCalled = false;
		Task<AcademicInsightsDto> Personal() { personalCalled = true; return Task.FromResult(new AcademicInsightsDto()); }
		await Should.ThrowAsync<AbpAuthorizationException>(() => AcademicInsightsEntryAccess.ReadAsync(["Teacher", "Parent"], false, Personal, Personal));
		personalCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task One_Technical_Reader_Error_Must_Not_Prevent_Another_Section()
	{
		var checker = Substitute.For<IPermissionChecker>(); checker.IsGrantedAsync("feature.View").Returns(true);
		var failed = await AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", () => throw new InvalidOperationException("SQL source unavailable"));
		failed.ShouldNotBeNull(); failed.Status.ShouldBe("error"); failed.Metrics.ShouldBeEmpty(); failed.Items.ShouldBeEmpty();
		var other = await AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", () => Task.FromResult(new AcademicInsightSectionDto { Key = "lesson", Status = "ready" }));
		other!.Status.ShouldBe("ready");
	}

	[Fact]
	public async Task Business_Authorization_And_Cancellation_Errors_Must_Propagate()
	{
		var checker = Substitute.For<IPermissionChecker>(); checker.IsGrantedAsync("feature.View").Returns(true);
		await Should.ThrowAsync<BusinessException>(() => AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", () => throw new BusinessException("Academic:Insights:InvalidContext")));
		await Should.ThrowAsync<AbpAuthorizationException>(() => AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", () => throw new AbpAuthorizationException()));
		await Should.ThrowAsync<OperationCanceledException>(() => AcademicInsightsSectionAccess.ReadAsync(checker, "feature.View", () => throw new OperationCanceledException()));
	}

	[Fact]
	public async Task Sibling_Only_View_Must_Return_Navigation_Without_Reading_Primary_Data_And_Observe_Revocation()
	{
		var method = typeof(AcademicInsightsSectionAccess).GetMethod("ReadFeatureAsync"); method.ShouldNotBeNull();
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync("sibling.View").Returns(true, false);
		var primaryCalled = false;
		Func<Task<AcademicInsightSectionDto>> primary = () => { primaryCalled = true; return Task.FromResult(new AcademicInsightSectionDto()); };
		Task<AcademicInsightSectionDto?> Read() => (Task<AcademicInsightSectionDto?>)method!.Invoke(null,
			[checker, "classrooms", "primary.View", new[] { "sibling.View" }, primary, null])!;
		var navigation = await Read(); navigation.ShouldNotBeNull(); navigation.Key.ShouldBe("classrooms"); navigation.Status.ShouldBe("unavailable");
		navigation.Metrics.ShouldBeEmpty(); navigation.Series.ShouldBeEmpty(); navigation.Items.ShouldBeEmpty();
		(await Read()).ShouldBeNull(); primaryCalled.ShouldBeFalse();
	}
}
