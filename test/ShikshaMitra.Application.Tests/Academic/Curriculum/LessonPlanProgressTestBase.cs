using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Academic.Curriculum;

public abstract class LessonPlanProgressTestBase : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    protected static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly Guid TeacherUserId = Guid.Parse("22222222-2222-2222-2222-111111111111");
    protected static readonly Guid TeacherEmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected readonly Guid YearId = Guid.NewGuid();
    protected Guid AcademicMediumId;
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
        AcademicMediumId = stage.EducationMediumId;
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
        AcademicYearId = YearId, TeacherId = TeacherEmployeeId,
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
            input.PlannedDate.UtcDateTime, input.Topic, input.PlannedCompletionPercentage,
            input.TimetableSlotId), autoSave: true);
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

    protected async Task AddHolidayAsync(DateTime date, string title)
    {
        var calendarId = Guid.NewGuid();
        await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(
            new AcademicCalendar(
                calendarId,
                TenantId,
                AcademicMediumId,
                null,
                "Lesson plan test calendar",
                new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31)),
            autoSave: true);
        await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(
            new AcademicCalendarDay(
                Guid.NewGuid(),
                TenantId,
                calendarId,
                date,
                DayType.Holiday,
                title),
            autoSave: true);
    }

    protected async Task SeedLessonPlanOwnerAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
            new IdentityUser(
                TeacherUserId,
                $"lesson-teacher-{suffix[..8]}",
                $"{suffix[..12]}@test.local",
                TenantId),
            autoSave: true);
        await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(
            new IdentityRole(roleId, $"lesson-teacher-{suffix[..8]}", TenantId),
            autoSave: true);

        var teacher = new Employee(
            TeacherEmployeeId,
            TenantId,
            TeacherUserId,
            $"LP{suffix[..10]}",
            new EmployeeBasicInfo(
                ProfilePictureBlobName: null,
                LastName: "Patel",
                FirstName: "Akshay",
                MiddleName: null,
                ShortName: "Akshay",
                EmployeeStatusType: EmployeeStatusType.Active,
                DateOfBirth: new DateOnly(1990, 1, 1),
                FamilyCast: null,
                CastType: null,
                GenderType: null,
                Religion: null,
                MaritalStatus: null,
                BloodGroup: null,
                MobileNo: null,
                SecondaryMobileNo: null,
                Email: $"{suffix[..12]}@test.local",
                PanNo: $"ABCDE{suffix[..4].ToUpperInvariant()}F",
                AdharNo: $"1{Math.Abs((long)suffix.GetHashCode()):D11}"[..12],
                Address1: null,
                Address2: null,
                Address3: null,
                Department: "Academics",
                Designation: "Teacher",
                DateOfJoining: new DateOnly(2025, 1, 1),
                EmployeeGroup: null,
                Qualification: null,
                RoleId: roleId,
                IdentificationMark1: null,
                City: null,
                State: null,
                Country: null,
                PinCode: null,
                ShiftId: null),
            new BankDetails(null, null, null, null, null),
            null);
        await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(teacher, autoSave: true);
    }

    protected IDisposable AsLessonPlanOwner()
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, TeacherUserId.ToString()),
            new(AbpClaimTypes.UserName, "akshay.patel"),
            // The test role satisfies the coarse permission interceptor; resource ownership
            // is independently enforced by LessonPlanAppService.
            new(AbpClaimTypes.Role, "admin"),
            new(AbpClaimTypes.TenantId, TenantId.ToString())
        };

        return GetRequiredService<ICurrentPrincipalAccessor>().Change(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}
