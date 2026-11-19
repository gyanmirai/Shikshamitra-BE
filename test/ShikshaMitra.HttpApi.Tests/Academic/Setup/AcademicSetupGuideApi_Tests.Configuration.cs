using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Setup;

public partial class AcademicSetupGuideApi_Tests
{
	[Fact]
	public async Task All_nine_states_follow_real_records_for_selected_year_medium_and_class()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school); using var client = As(school);
		var url = StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.First}";
		(await ReadAsync(client, url)).GetProperty("settings").EnumerateArray().ShouldAllBe(x => x.GetProperty("configurationState").GetString() == "empty");
		await InAsync(school.TenantId, async services => {
			await services.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(Guid.NewGuid(), school.TenantId, "Mathematics", "#123456", school.MediumId), true);
			await services.GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(new ElectiveGroup(Guid.NewGuid(), school.TenantId, "Languages", "#123456", school.MediumId), true);
			await services.GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>().InsertAsync(new WeeklyLectureSlot(Guid.NewGuid(), school.TenantId, classes.First), true);
			await services.GetRequiredService<IRepository<LectureSlotConfig, Guid>>().InsertAsync(new LectureSlotConfig(Guid.NewGuid(), school.TenantId, classes.First, "08:00-14:00"), true);
			await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(school.MediumId, "Practice");
			await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(school.MediumId, "Project", defaultMarks: 100, defaultEvaluationMethod: EvaluationMethod.Marks);
			await services.GetRequiredService<IRepository<GradingScale, Guid>>().InsertAsync(new GradingScale(Guid.NewGuid(), school.TenantId, "Marks", "100", school.MediumId), true);
			await services.GetRequiredService<IRepository<ExamType, Guid>>().InsertAsync(new ExamType(Guid.NewGuid(), school.TenantId, "Unit test", null, school.MediumId), true);
			var rubric = new AssessmentRubric(Guid.NewGuid(), school.TenantId, null, null, school.MediumId, null, null, null, null, null,
				true, WorkType.Both, RubricScoringModel.PointsBased, RubricVisibility.Institution, "School rubric", null, null, null, null, null);
			await services.GetRequiredService<IRepository<AssessmentRubric, Guid>>().InsertAsync(rubric, true);
		});
		(await ReadAsync(client, url)).GetProperty("settings").EnumerateArray().ShouldAllBe(x => x.GetProperty("configurationState").GetString() == "configured");
		var sibling = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.Second}");
		Setting(sibling, "weekly-lectures").GetProperty("configurationState").GetString().ShouldBe("empty");
		Setting(sibling, "lecture-slots").GetProperty("configurationState").GetString().ShouldBe("empty");
	}

	[Fact]
	public async Task Class_scope_allows_its_inherited_medium_settings_but_rejects_sibling_classes()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school);
		await InAsync(school.TenantId, async services => {
			var scope = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scope.DeleteAsync(x => x.UserId == school.ActorId, true);
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(school.MediumId);
			await scope.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), school.TenantId, userId: school.ActorId, academicYearId: school.YearId,
				educationBoardId: medium.EducationBoardId, educationMediumId: school.MediumId, educationMediumStageId: classes.Stage, standardId: classes.First), true);
		});
		using var client = As(school);
		(await client.GetAsync(StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.First}")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync(StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.Second}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(StateUrl(school) + $"?standardId={classes.First}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task<(Guid Stage, Guid First, Guid Second)> AddClassesAsync(School school)
	{
		(Guid, Guid, Guid) result = default;
		await InAsync(school.TenantId, async services => {
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(school.MediumId, "Primary");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var manager = services.GetRequiredService<StandardManager>();
			var first = await manager.CreateAsync(stage.Id, "Class 1", 5, null);
			var second = await manager.CreateAsync(stage.Id, "Class 2", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(first, true);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(second, true);
			result = (stage.Id, first.Id, second.Id);
		}); return result;
	}
}
