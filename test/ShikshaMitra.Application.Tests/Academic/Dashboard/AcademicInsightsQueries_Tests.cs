using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Volo.Abp;
using Volo.Abp.Identity;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicInsightsQueries_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public Task Normal_Save_Shaped_Class_Teacher_Mappings_With_Null_Year_Must_Be_Visible() => WithUnitOfWorkAsync(async () =>
	{
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var medium = Guid.NewGuid(); var standard = Guid.NewGuid(); var stage = Guid.NewGuid();
		using var selectedTenant = GetRequiredService<ICurrentTenant>().Change(tenant);
		var repository = GetRequiredService<IRepository<ClassTeacher.ClassTeacher, Guid>>();
		var allowed = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
		var current = new ClassTeacher.ClassTeacher(Guid.NewGuid(), tenant, standard, allowed[0], Guid.NewGuid(), medium,
			educationMediumStageId: stage);
		current.AcademicYearId.ShouldBeNull();
		await repository.InsertAsync(current, autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), tenant, standard, allowed[1], Guid.NewGuid(), medium, academicYearId: year), autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), tenant, standard, allowed[2], Guid.NewGuid(), medium, academicYearId: Guid.NewGuid()), autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), tenant, standard, allowed[3], Guid.NewGuid(), medium, status: ClassTeacherAssignmentStatus.Draft), autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), tenant, standard, allowed[4], Guid.NewGuid(), Guid.NewGuid()), autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), tenant, standard, Guid.NewGuid(), Guid.NewGuid(), medium), autoSave: true);
		await repository.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), standard, allowed[4], Guid.NewGuid(), medium), autoSave: true);
		var c = new AcademicInsightsContext(new() { AcademicYearId = year, BoardId = Guid.NewGuid(), EducationMediumId = medium }, tenant,
			new UserScopeContext { TenantId = tenant, DivisionIds = allowed.ToList() }, false, allowed.Select(id => new AcademicInsightDivision(id, standard, stage, id.ToString())).ToList());
		var result = await GetRequiredService<AcademicInsightsTeachingReader>().ClassroomsAsync(c);
		result.Metrics.Single(x => x.Key == "mapped").Value.ShouldBe(2);
		result.Series.Single().Points.Single(x => x.Key == allowed[0].ToString()).Status.ShouldBe("mapped");
		result.Series.Single().Points.Where(x => x.Status == "mapped").Count().ShouldBe(2);
	});
	[Fact]
	public Task Context_Must_Reject_Inconsistent_Hierarchy_And_Missing_UserScope() => WithUnitOfWorkAsync(async () =>
	{
		var tenant = Guid.NewGuid(); using var selectedTenant = GetRequiredService<ICurrentTenant>().Change(tenant);
		var actor = new IdentityUser(Guid.NewGuid(), "insights-reader", "insights-reader@example.test", tenant);
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(actor, autoSave: true);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, actor.Id.ToString()), new Claim(AbpClaimTypes.TenantId, tenant.ToString())], "test")));
		var year = await GetRequiredService<AcademicYearManager>().CreateAsync("Insights year", DateTime.Today.AddDays(-30), DateTime.Today.AddDays(300));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
		var board = await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, autoSave: true);
		var medium = await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English");
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, autoSave: true);
		var reader = GetRequiredService<AcademicInsightsContextReader>();
		var wrongYear = await Should.ThrowAsync<BusinessException>(() => reader.ReadAsync(new() { AcademicYearId = Guid.NewGuid(), BoardId = board.Id, EducationMediumId = medium.Id }));
		wrongYear.Code.ShouldBe("Academic:Insights:InvalidContext");
		var wrongMedium = await Should.ThrowAsync<BusinessException>(() => reader.ReadAsync(new() { AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = Guid.NewGuid() }));
		wrongMedium.Code.ShouldBe("Academic:Insights:InvalidContext");
		await Should.ThrowAsync<AbpAuthorizationException>(() => reader.ReadAsync(new() { AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = medium.Id }));
	});
	[Fact]
	public Task Direct_Application_Entry_Must_Reject_Anonymous_Host_And_Forged_Tenant_Identity() => WithUnitOfWorkAsync(async () =>
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		var principal = GetRequiredService<ICurrentPrincipalAccessor>();
		var service = GetRequiredService<IAcademicInsightsAppService>();
		var input = new AcademicInsightsInput { AcademicYearId = Guid.NewGuid(), BoardId = Guid.NewGuid(), EducationMediumId = Guid.NewGuid() };
		using (tenant.Change(Guid.NewGuid()))
		using (principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(input));
		using (tenant.Change(null))
		using (principal.Change(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString())], "test"))))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(input));
		using (tenant.Change(Guid.NewGuid()))
		using (principal.Change(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, Guid.NewGuid().ToString())], "test"))))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(input));
	});
	[Fact]
	public Task Lesson_Query_Must_Exclude_Other_Years_Divisions_And_Tenants() => WithUnitOfWorkAsync(async () =>
	{
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var division = Guid.NewGuid(); var standard = Guid.NewGuid();
		using var selectedTenant = GetRequiredService<ICurrentTenant>().Change(tenant);
		var repository = GetRequiredService<IRepository<LessonPlan, Guid>>();
		LessonPlan Plan(Guid school, Guid academicYear, Guid classroom) => new(Guid.NewGuid(), school, academicYear,
			Guid.NewGuid(), standard, classroom, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "Recorded lesson", 30);
		await repository.InsertAsync(Plan(tenant, year, division), autoSave: true);
		await repository.InsertAsync(Plan(tenant, Guid.NewGuid(), division), autoSave: true);
		await repository.InsertAsync(Plan(tenant, year, Guid.NewGuid()), autoSave: true);
		await repository.InsertAsync(Plan(Guid.NewGuid(), year, division), autoSave: true);
		var c = new AcademicInsightsContext(new() { AcademicYearId = year, BoardId = Guid.NewGuid(), EducationMediumId = Guid.NewGuid() }, tenant,
			new UserScopeContext { TenantId = tenant, DivisionIds = [division], IsAdmin = true }, false, [new(division, standard, Guid.NewGuid(), "Class A")]);
		var result = await GetRequiredService<AcademicInsightsTeachingReader>().LessonsAsync(c);
		result.Metrics.Single(x => x.Key == "plans").Value.ShouldBe(1);
		result.Series.Single().Points.Sum(x => x.Value).ShouldBe(1);
	});

	[Fact]
	public Task Empty_Queries_Must_Execute_Without_Manufacturing_Observations() => WithUnitOfWorkAsync(async () =>
	{
		var tenant = Guid.NewGuid(); using var selectedTenant = GetRequiredService<ICurrentTenant>().Change(tenant);
		var c = new AcademicInsightsContext(new() { AcademicYearId = Guid.NewGuid(), BoardId = Guid.NewGuid(), EducationMediumId = Guid.NewGuid() }, tenant,
			new UserScopeContext { TenantId = tenant }, false, []);
		var teaching = GetRequiredService<AcademicInsightsTeachingReader>();
		var tasks = GetRequiredService<AcademicInsightsTaskReader>();
		var sections = new[] { await teaching.CurriculumAsync(c), await teaching.TimetableAsync(c), await teaching.ExamsAsync(c),
			await tasks.TasksAsync(c), await tasks.EvaluationAsync(c), await GetRequiredService<AcademicInsightsStudentReader>().ReadAsync(c),
			await GetRequiredService<AcademicInsightsOperationalReader>().CalendarAsync(c) };
		sections.ShouldAllBe(x => x.Status == "empty");
		sections.SelectMany(x => x.Series).SelectMany(x => x.Points).ShouldBeEmpty();
	});
}
