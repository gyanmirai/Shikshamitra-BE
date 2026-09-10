using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public abstract class LessonPlanProgressTestBase : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    protected static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected readonly Guid YearId = Guid.NewGuid();
    protected Guid StandardId;
    protected Guid DivisionId;
    protected Guid SubjectId;
    protected Guid ChapterId;
    protected ILessonPlanAppService Service => GetRequiredService<ILessonPlanAppService>();
    protected IRepository<LessonPlan, Guid> Plans => GetRequiredService<IRepository<LessonPlan, Guid>>();
    protected ICurrentTenant Tenant => GetRequiredService<ICurrentTenant>();

    protected async Task SeedContextAsync()
    {
        var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
        var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
        var standard = await GetRequiredService<StandardManager>().CreateAsync(stageId, "Progress Class", 12, null);
        await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, autoSave: true);
        StandardId = standard.Id;
        var division = await GetRequiredService<DivisionManager>().CreateAsync(StandardId, "B", 35);
        await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
        DivisionId = division.Id;
        var subject = new Subject(Guid.NewGuid(), TenantId, "Core Mathematics", "#112233", stage.EducationMediumId);
        await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
        SubjectId = subject.Id;
        var assigned = new AssignedSubject(Guid.NewGuid(), TenantId, StandardId, SubjectId, "MATH");
        await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assigned, autoSave: true);
        var chapter = new AssignedSubjectChapter(Guid.NewGuid(), assigned.Id, 1, "Core Maths ONE",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10))
        { TenantId = TenantId, AssignedSubjectId = assigned.Id };
        await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(chapter, autoSave: true);
        ChapterId = chapter.Id;
    }

    protected CreateLessonPlanDto Input(int target = 30) => new()
    {
        AcademicYearId = YearId, TeacherId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        StandardId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId, ChapterId = ChapterId,
        PlannedDate = DateTime.UtcNow.AddDays(1), Topic = Guid.NewGuid().ToString(),
        PlannedCompletionPercentage = target
    };

    protected GetLessonPlanChapterProgressInput ProgressInput() => new()
    {
        AcademicYearId = YearId, StandardId = StandardId, DivisionId = DivisionId,
        SubjectId = SubjectId, ChapterId = ChapterId
    };

    protected async Task<LessonPlan> InsertPlanAsync(CreateLessonPlanDto? input = null)
    {
        input ??= Input(90);
        return await Plans.InsertAsync(new LessonPlan(Guid.NewGuid(), Tenant.Id, input.AcademicYearId,
            input.TeacherId, input.StandardId, input.DivisionId, input.SubjectId, input.ChapterId,
            input.PlannedDate, input.Topic, input.PlannedCompletionPercentage), autoSave: true);
    }

    protected async Task AddDiaryAsync(LessonPlan plan, int progress, bool submitted = true, bool conducted = true)
    {
        var date = DateTime.UtcNow.AddDays(-1);
        var diary = new TeacherDiary(Guid.NewGuid(), Tenant.Id, plan.Id, plan.TeacherId, date);
        diary.Update(date, conducted, "Earlier topic", null, progress, null, null, null, null,
            false, null, null, null, null, null, null, null, null, null, false, null, null);
        if (submitted) diary.Submit();
        await GetRequiredService<IRepository<TeacherDiary, Guid>>().InsertAsync(diary, autoSave: true);
    }
}
