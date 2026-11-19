using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Timetable.Portal;

public class PortalTimetableAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private IPortalTimetableAppService Service => GetRequiredService<IPortalTimetableAppService>();

    [Theory]
    [InlineData(false, false, GenderEnum.Female)]
    [InlineData(true, false, GenderEnum.Female)]
    [InlineData(false, true, GenderEnum.Female)]
    [InlineData(false, false, GenderEnum.Male)]
    [InlineData(true, false, GenderEnum.Male)]
    [InlineData(false, true, GenderEnum.Male)]
    public Task My_profile_includes_the_authorized_full_student_context(bool parent, bool legacySelf, GenderEnum gender) => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: parent, legacySelf: legacySelf, gender: gender); using var identity = SignIn(f.User.Id);
        var config = new ShikshaMitra.Academic.Timetable.Settings.LectureSlots.LectureSlotConfig(Guid.NewGuid(), id, f.Student.StandardId!.Value, "08:30-14:30");
        config.AddSlotGroup(new ShikshaMitra.Academic.Timetable.Settings.LectureSlots.LectureSlotGroup(Guid.NewGuid(), config.Id, 1, 1, 1, [1]));
        await Insert(config);
        var service = GetRequiredService<IProfileAppService>();
        var profile = await service.GetAsync();
        profile.UserName.ShouldBe(f.User.UserName);
        var session = (PortalSessionDto)profile.ExtraProperties["portalSession"]!;
        session.Version.ShouldBe(1);
        session.Status.ShouldBe("ready");
        session.Students.Single().Account.StudentId.ShouldBe(f.Student.Id);
        session.Students.Single().Account.Relationship.ShouldBe(parent ? "mother" : "self");
        session.Students.Single().ContextStatus.ShouldBe("loaded");
        var contexts = ((List<PortalTimetableContextDto>)profile.ExtraProperties["studentContexts"]!).Single();
        contexts.Student.Id.ShouldBe(f.Student.Id);
        contexts.Student.Gender.ShouldBe(gender);
        contexts.Student.MediumId.ShouldBe(f.Medium.Id);
        contexts.Student.MediumName.ShouldBe("English");
        contexts.Student.StageId.ShouldBe(f.Student.AcademicMediumStageId);
        contexts.Student.StageName.ShouldBe("Primary");
        contexts.Student.StandardId.ShouldBe(f.Student.StandardId);
        contexts.Student.StandardName.ShouldBe("Class 8");
        contexts.Student.DivisionName.ShouldBe("A");
        contexts.Student.RollNumber.ShouldBe(12);
        contexts.CurrentAcademicYear!.Id.ShouldBe(f.Year.Id);
        contexts.AvailableAcademicYears.Single().Id.ShouldBe(f.Year.Id);
        contexts.WorkingDays.ShouldBe(["tuesday"]);
        contexts.Timezone.ShouldBe("Asia/Kolkata");
        contexts.Availability.ShouldBe("available");
        var json = JsonSerializer.SerializeToElement(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var studentJson = json.GetProperty("extraProperties").GetProperty("studentContexts")[0].GetProperty("student");
        var sessionJson = json.GetProperty("extraProperties").GetProperty("portalSession");
        sessionJson.GetProperty("version").GetInt32().ShouldBe(1);
        sessionJson.GetProperty("students")[0].GetProperty("account").GetProperty("userId").GetGuid().ShouldBe(f.User.Id);
        sessionJson.GetProperty("students")[0].GetProperty("context").GetRawText().ShouldBe(json.GetProperty("extraProperties").GetProperty("studentContexts")[0].GetRawText());
        studentJson.GetProperty("id").GetGuid().ShouldBe(f.Student.Id);
        studentJson.GetProperty("gender").GetInt32().ShouldBe((int)gender);
        studentJson.GetProperty("mediumId").GetGuid().ShouldBe(f.Medium.Id);
        studentJson.GetProperty("mediumName").GetString().ShouldBe("English");
        studentJson.GetProperty("stageId").GetGuid().ShouldBe(f.Student.AcademicMediumStageId!.Value);
        studentJson.GetProperty("stageName").GetString().ShouldBe("Primary");
    });

    [Fact]
    public Task My_profile_does_not_include_revoked_or_unconfirmed_portal_data() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, legacySelf: true); await Insert(f.Link); using var identity = SignIn(f.User.Id);
        var service = GetRequiredService<IProfileAppService>();
        ((List<PortalTimetableContextDto>)(await service.GetAsync()).ExtraProperties["studentContexts"]!).Count.ShouldBe(1);
        f.User.SetEmailConfirmed(false); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        ((List<PortalTimetableContextDto>)(await service.GetAsync()).ExtraProperties["studentContexts"]!).ShouldBeEmpty();
        var restricted = (PortalSessionDto)(await service.GetAsync()).ExtraProperties["portalSession"]!;
        restricted.Status.ShouldBe("restricted");
        restricted.Students.ShouldBeEmpty();
        f.User.SetEmailConfirmed(true); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
        ((List<PortalTimetableContextDto>)(await service.GetAsync()).ExtraProperties["studentContexts"]!).ShouldBeEmpty();
    });

    [Fact]
    public Task My_profile_does_not_include_foreign_tenant_links_and_preserves_no_enrollment() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            var foreignTenant = GetRequiredService<ICurrentTenant>().Id!.Value;
            var foreign = await Seed(foreignTenant);
            await Insert(new StudentPortalAccountLink(Guid.NewGuid(), foreignTenant, foreign.Student.Id, f.User.Id, "guardian", "mother", Guid.NewGuid()));
        }
        f.Enrollment.Deactivate(); await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(f.Enrollment, true);
        var context = ((List<PortalTimetableContextDto>)(await GetRequiredService<IProfileAppService>().GetAsync()).ExtraProperties["studentContexts"]!).Single();
        context.Student.Id.ShouldBe(f.Student.Id);
        context.Student.Gender.ShouldBeNull();
        context.Student.MediumId.ShouldBeNull();
        context.Student.MediumName.ShouldBeNull();
        context.Student.StageId.ShouldBeNull();
        context.Student.StageName.ShouldBeNull();
        context.Availability.ShouldBe("noEnrollment");
        context.CurrentAcademicYear.ShouldBeNull();
    });

    [Fact]
    public Task Profile_preserves_good_children_when_one_enrollment_is_invalid() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: true); using var identity = SignIn(f.User.Id);
        var medium = await Insert(await GetRequiredService<EducationMediumManager>().CreateAsync(f.Medium.EducationBoardId, "Other"));
        var sibling = await Insert(new Student(Guid.NewGuid(), id, "Second", "", "Child", f.Year.Id, f.Student.AcademicBoardId!.Value,
            medium.Id, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId, f.Student.DivisionId));
        await Insert(StudentEnrollment.Create(Guid.NewGuid(), id, sibling.Id, f.Year.Id, f.Student.StandardId, f.Student.DivisionId, 2));
        await Insert(new StudentPortalAccountLink(Guid.NewGuid(), id, sibling.Id, f.User.Id, "guardian", "mother", Guid.NewGuid()));
        var profile = await GetRequiredService<IProfileAppService>().GetAsync();
        var session = (PortalSessionDto)profile.ExtraProperties["portalSession"]!;
        session.Students.Count.ShouldBe(2);
        session.Students.Single(x => x.Account.StudentId == f.Student.Id).ContextStatus.ShouldBe("loaded");
        var failed = session.Students.Single(x => x.Account.StudentId == sibling.Id);
        failed.ContextStatus.ShouldBe("invalid"); failed.Context.ShouldBeNull();
        ((List<PortalTimetableContextDto>)profile.ExtraProperties["studentContexts"]!).Single().Student.Id.ShouldBe(f.Student.Id);
        await Should.ThrowAsync<BusinessException>(() => Service.GetContextAsync(sibling.Id));
    });

    [Fact]
    public Task Profile_batches_context_queries_and_resolves_associations_once() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: true); using var identity = SignIn(f.User.Id);
        using var queries = new PortalProfileQueryObserver();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await GetRequiredService<IProfileAppService>().GetAsync();
        var oneCount = queries.Commands.Count;
        var oneMs = timer.ElapsedMilliseconds;
        for (var index = 0; index < 2; index++)
        {
            var sibling = await Insert(new Student(Guid.NewGuid(), id, "Sibling", "", $"{index}", f.Year.Id, f.Student.AcademicBoardId!.Value,
                f.Medium.Id, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId, f.Student.DivisionId));
            await Insert(StudentEnrollment.Create(Guid.NewGuid(), id, sibling.Id, f.Year.Id, f.Student.StandardId, f.Student.DivisionId, index + 2));
            await Insert(new StudentPortalAccountLink(Guid.NewGuid(), id, sibling.Id, f.User.Id, "guardian", "mother", Guid.NewGuid()));
        }
        queries.Commands.Clear(); timer.Restart();
        var session = (PortalSessionDto)(await GetRequiredService<IProfileAppService>().GetAsync()).ExtraProperties["portalSession"]!;
        var threeMs = timer.ElapsedMilliseconds;
        session.Students.Count.ShouldBe(3);
        session.Students.ShouldAllBe(x => x.ContextStatus == "loaded");
        queries.Commands.Count.ShouldBe(oneCount);
        queries.Commands.Count(x => x.Contains("StudentPortalAccountLinks", StringComparison.Ordinal)).ShouldBe(2);
        Console.WriteLine($"Portal profile local query sample: one child {oneCount} commands/{oneMs} ms; three children {queries.Commands.Count} commands/{threeMs} ms.");
    });

    [Theory]
    [InlineData("timeout")]
    [InlineData("authorization")]
    [InlineData("cancellation")]
    public Task Profile_context_failures_preserve_their_security_and_retry_meaning(string failure) => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: true); using var identity = SignIn(f.User.Id);
        Exception error = failure switch
        {
            "timeout" => new TimeoutException("Fixture timeout"),
            "authorization" => new AbpAuthorizationException(),
            _ => new OperationCanceledException()
        };
        var service = new StudentPortalProfileAppService(GetRequiredService<IdentityUserManager>(),
            GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.IdentityOptions>>(),
            GetRequiredService<StudentPortalAccessService>(), new FaultingContextReader(error))
            { LazyServiceProvider = new Volo.Abp.DependencyInjection.AbpLazyServiceProvider(ServiceProvider) };
        if (failure == "cancellation")
        {
            await Should.ThrowAsync<OperationCanceledException>(() => service.GetAsync());
            return;
        }
        var profile = await service.GetAsync();
        var session = (PortalSessionDto)profile.ExtraProperties["portalSession"]!;
        ((List<PortalTimetableContextDto>)profile.ExtraProperties["studentContexts"]!).ShouldBeEmpty();
        if (failure == "authorization")
        {
            session.Status.ShouldBe("restricted"); session.Students.ShouldBeEmpty();
        }
        else
        {
            session.Status.ShouldBe("ready");
            session.Students.Single().Account.StudentId.ShouldBe(f.Student.Id);
            session.Students.Single().ContextStatus.ShouldBe("unavailable");
            session.Students.Single().Context.ShouldBeNull();
        }
    });

    [Volo.Abp.DependencyInjection.DisableConventionalRegistration]
    private sealed class FaultingContextReader(Exception error) : PortalStudentContextReader(null!, null!, null!, null!, null!, null!, null!, null!)
    {
        protected internal override Task<Dictionary<Guid, PortalStudentContextResult>> ReadAsync(Guid[] authorizedIds, Guid? academicYearId = null) =>
            Task.FromException<Dictionary<Guid, PortalStudentContextResult>>(error);
    }

    private async Task<T> Insert<T>(T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
        await GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
    private IDisposable SignIn(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "test")));

    private async Task<Fixture> Seed(Guid tenant, bool parent = false, bool publish = true, bool legacySelf = false, bool legacyPlacement = false, GenderEnum? gender = null)
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).Date;
        var year = await Insert(await GetRequiredService<AcademicYearManager>().CreateAsync("Current", today.AddDays(-30), today.AddDays(300)));
        var board = await Insert(await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
        var medium = await Insert(await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
        var stage = await Insert(await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
        var standard = await Insert(await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 8", 12, null));
        var division = await Insert(await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30));
        var student = await Insert(new Student(Guid.NewGuid(), tenant, "Real", "", "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id,
            rollNo: 12, status: legacyPlacement ? StudentLifeCycleStatusEnum.Enrolled : StudentLifeCycleStatusEnum.New));
        if (gender.HasValue)
            await Insert(new StudentAdditionalDetail(student.Id, tenant, null, gender, null, string.Empty, null, null));
        var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, student.Id, year.Id, standard.Id, division.Id, 12);
        if (!legacyPlacement) await Insert(enrollment);
        var user = new IdentityUser(Guid.NewGuid(), "portal", "portal@example.test", tenant);
        user.SetEmailConfirmed(true);
        await Insert(user);
        var link = new StudentPortalAccountLink(Guid.NewGuid(), tenant, student.Id, user.Id,
            parent ? "guardian" : "student", parent ? "mother" : "self", Guid.NewGuid());
        if (legacySelf)
        {
            student.LinkUser(user.Id);
            await GetRequiredService<IRepository<Student, Guid>>().UpdateAsync(student, true);
        }
        else await Insert(link);
        var subject = await Insert(new Subject(Guid.NewGuid(), tenant, "Mathematics", "#008967", medium.Id));
        var timetable = new TimetableEntity(Guid.NewGuid(), tenant, standard.Id, division.Id);
        var monday = DateOnly.FromDateTime(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)));
        var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 1, new(8, 30), new(9, 15), SlotType.Period);
        slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, subject.Id));
        timetable.AddSlot(slot);
        timetable.AddSlot(new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 2, new(9,15), new(9,30), SlotType.Break, BreakType.Short));
        if (publish) timetable.Publish();
        await Insert(timetable);
        return new(student, user, link, year, medium, enrollment, timetable, monday);
    }
    private record Fixture(Student Student, IdentityUser User, StudentPortalAccountLink Link, AcademicYear Year,
        EducationMedium Medium, StudentEnrollment Enrollment, TimetableEntity Timetable, DateOnly Date)
    {
        public PortalTimetableScheduleInput Request => new() { StudentId = Student.Id, AcademicYearId = Year.Id, FromDate = Date, ToDate = Date };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Linked_student_and_parent_read_real_published_schedule(bool parent) => WithUnitOfWorkAsync(async () =>
    {
        using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
        var f = await Seed(GetRequiredService<ICurrentTenant>().Id!.Value, parent);
        using var identity = SignIn(f.User.Id);
        var context = await Service.GetContextAsync(f.Student.Id);
        context.Student.Name.ShouldBe("Real Student");
        context.Student.Gender.ShouldBeNull();
        context.Student.StandardName.ShouldBe("Class 8");
        context.Student.DivisionName.ShouldBe("A");
        context.Student.RollNumber.ShouldBe(12);
        context.CurrentAcademicYear!.Id.ShouldBe(f.Year.Id);
        context.WorkingDays.ShouldBe(["monday"]);
        var day = (await Service.GetScheduleAsync(f.Request)).Days.Single();
        day.Periods.Count.ShouldBe(2);
        day.Periods[0].Subject!.Name.ShouldBe("Mathematics");
        day.Periods[0].StartDateTime.Offset.ShouldBe(TimeSpan.FromMinutes(330));
        day.Periods[0].StartDateTime.Hour.ShouldBe(8);
        day.Periods[1].PeriodType.ShouldBe("break");
        day.Periods[1].CanMessageTeacher.ShouldBeFalse();
    });

    [Fact]
    public Task Legacy_current_student_placement_reads_existing_timetable_without_creating_enrollment() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, legacySelf: true, legacyPlacement: true); using var identity = SignIn(f.User.Id);
        var context = await Service.GetContextAsync();
        context.Availability.ShouldBe("available");
        context.Student.StandardId.ShouldBe(f.Student.StandardId);
        context.Student.MediumName.ShouldBe("English");
        context.Student.StageId.ShouldBe(f.Student.AcademicMediumStageId);
        context.Student.StageName.ShouldBe("Primary");
        context.Student.DivisionId.ShouldBe(f.Student.DivisionId);
        context.Student.RollNumber.ShouldBe(12);
        context.CurrentAcademicYear!.Id.ShouldBe(f.Year.Id);
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods[0].Subject!.Name.ShouldBe("Mathematics");
        (await GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(x => x.StudentId == f.Student.Id)).ShouldBe(0);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Inactive_or_deleted_enrollment_blocks_legacy_placement_fallback(bool deleted) => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, legacyPlacement: true); using var identity = SignIn(f.User.Id);
        await Insert(f.Enrollment);
        if (deleted) await GetRequiredService<IRepository<StudentEnrollment, Guid>>().DeleteAsync(f.Enrollment, true);
        else
        {
            f.Enrollment.Deactivate();
            await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(f.Enrollment, true);
        }
        (await Service.GetContextAsync()).Availability.ShouldBe("noEnrollment");
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods.ShouldBeEmpty();
    });

    [Fact]
    public Task Legacy_persisted_self_association_reads_profile_and_schedule_with_all_access_checks() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, legacySelf: true); using var identity = SignIn(f.User.Id);
        var profile = GetRequiredService<IStudentOnboardingAppService>();
        var linked = (await profile.GetMyPortalStudentsAsync()).Single();
        linked.StudentId.ShouldBe(f.Student.Id);
        linked.UserId.ShouldBe(f.User.Id);
        linked.AccountType.ShouldBe("student");
        linked.Relationship.ShouldBe("self");
        (await Service.GetContextAsync()).Student.Id.ShouldBe(f.Student.Id);
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods.Count.ShouldBe(2);
        (await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(x => x.UserId == f.User.Id)).ShouldBe(0);
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(Guid.NewGuid()));
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => profile.GetMyPortalStudentsAsync());
            await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
        }
        f.User.SetEmailConfirmed(false); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        await Should.ThrowAsync<AbpAuthorizationException>(() => profile.GetMyPortalStudentsAsync());
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
        f.User.SetEmailConfirmed(true); f.User.SetIsActive(false);
        await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        await Should.ThrowAsync<AbpAuthorizationException>(() => profile.GetMyPortalStudentsAsync());
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
    });

    [Fact]
    public Task Explicit_self_link_deduplicates_legacy_association_and_revocation_prevents_fallback() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, legacySelf: true); using var identity = SignIn(f.User.Id);
        await Insert(f.Link);
        var profile = GetRequiredService<IStudentOnboardingAppService>();
        (await profile.GetMyPortalStudentsAsync()).Count.ShouldBe(1);
        (await Service.GetContextAsync()).Student.Id.ShouldBe(f.Student.Id);
        await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
        (await profile.GetMyPortalStudentsAsync()).ShouldBeEmpty();
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetScheduleAsync(f.Request));
    });

    [Fact]
    public Task Draft_timetable_and_inactive_enrollment_are_explicit_empty_states() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, publish: false); using var identity = SignIn(f.User.Id);
        var context = await Service.GetContextAsync();
        context.Availability.ShouldBe("noPublishedTimetable");
        context.Student.MediumName.ShouldBe("English");
        context.Student.StageName.ShouldBe("Primary");
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods.ShouldBeEmpty();
        f.Enrollment.Deactivate(); await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(f.Enrollment, true);
        (await Service.GetContextAsync()).Availability.ShouldBe("noEnrollment");
    });

    [Fact]
    public Task Arbitrary_student_other_tenant_unconfirmed_user_and_revoked_link_are_denied() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(Guid.NewGuid()));
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
            await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
        f.User.SetEmailConfirmed(false); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
        f.User.SetEmailConfirmed(true); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
        await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
        await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
    });

    [Fact]
    public Task Selected_elective_is_the_only_subject_in_a_parallel_period() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var group = await Insert(new ElectiveGroup(Guid.NewGuid(), id, "Languages", "#008967", f.Medium.Id));
        var first = await Insert(new Subject(Guid.NewGuid(), id, "Hindi", "#008967", f.Medium.Id));
        var second = await Insert(new Subject(Guid.NewGuid(), id, "Gujarati", "#008967", f.Medium.Id));
        group.AddSubject(first.Id); group.AddSubject(second.Id);
        await GetRequiredService<IRepository<ElectiveGroup, Guid>>().UpdateAsync(group, true);
        await Insert(new AssignedSubject(Guid.NewGuid(), id, f.Student.StandardId!.Value, first.Id, "HIN", SubjectStatus.Published, SubjectType.Elective, group.Id));
        await Insert(new AssignedSubject(Guid.NewGuid(), id, f.Student.StandardId!.Value, second.Id, "GUJ", SubjectStatus.Published, SubjectType.Elective, group.Id));
        var slot = new TimetableSlot(Guid.NewGuid(), f.Timetable.Id, TimetableDayOfWeek.Monday, 3, new(9,30), new(10,15), SlotType.Period, electiveGroupId: group.Id);
        slot.AddSubject(new(Guid.NewGuid(), slot.Id, first.Id)); slot.AddSubject(new(Guid.NewGuid(), slot.Id, second.Id));
        f.Timetable.AddSlot(slot); await GetRequiredService<IRepository<TimetableEntity, Guid>>().UpdateAsync(f.Timetable, true);
        var approved = new StudentElectiveSelection(Guid.NewGuid(), id, f.Student.Id, group.Id, second.Id, f.Medium.Id);
        approved.LockSelection();
        await Insert(approved);
        var periods = (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods;
        periods.ShouldContain(x => x.Subject != null && x.Subject.Name == "Gujarati");
        periods.ShouldNotContain(x => x.Subject != null && x.Subject.Name == "Hindi");
    });

    [Theory]
    [InlineData(false, false, "electivePreferenceRequired")]
    [InlineData(true, false, "electiveApprovalPending")]
    [InlineData(true, true, "available")]
    public Task Mobile_context_and_schedule_share_whole_week_elective_approval_policy(bool assigned, bool locked, string availability) => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var subject = await Insert(new Subject(Guid.NewGuid(), id, "Hindi", "#008967", f.Medium.Id));
        var group = new ElectiveGroup(Guid.NewGuid(), id, "Language", "#008967", f.Medium.Id);
        group.AddSubject(subject.Id); await Insert(group);
        await Insert(new AssignedSubject(Guid.NewGuid(), id, f.Student.StandardId!.Value, subject.Id, "HIN", SubjectStatus.Published, SubjectType.Elective, group.Id));
        // The request is for Monday, but a missing Wednesday choice must still gate it.
        var slot = new TimetableSlot(Guid.NewGuid(), f.Timetable.Id, TimetableDayOfWeek.Wednesday, 1, new(8, 30), new(9, 15), SlotType.Period, electiveGroupId: group.Id);
        slot.AddSubject(new(Guid.NewGuid(), slot.Id, subject.Id)); f.Timetable.AddSlot(slot);
        await GetRequiredService<IRepository<TimetableEntity, Guid>>().UpdateAsync(f.Timetable, true);
        if (assigned)
        {
            var selection = new StudentElectiveSelection(Guid.NewGuid(), id, f.Student.Id, group.Id, subject.Id, f.Medium.Id);
            if (locked) selection.LockSelection();
            await Insert(selection);
        }
        var context = await Service.GetContextAsync(f.Student.Id, f.Year.Id);
        context.Availability.ShouldBe(availability);
        var result = await Service.GetScheduleAsync(f.Request);
        result.Availability.ShouldBe(availability);
        if (!locked) result.Days.SelectMany(day => day.Periods).ShouldBeEmpty();
        else result.Days.SelectMany(day => day.Periods).ShouldNotBeEmpty();
        // Shared profile/enrollment context retains its own meaning; only mobile timetable is gated.
        var profile = await GetRequiredService<IProfileAppService>().GetAsync();
        ((List<PortalTimetableContextDto>)profile.ExtraProperties["studentContexts"]!).Single().Availability.ShouldBe("available");
    });

    [Fact]
    public Task Only_published_applicable_active_calendar_closures_hide_lessons() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var calendar = new AcademicCalendar(Guid.NewGuid(), id, f.Medium.Id, f.Year.Id, "School", f.Year.StartDate, f.Year.EndDate);
        await Insert(calendar);
        await Insert(new AcademicCalendarDay(Guid.NewGuid(), id, calendar.Id, f.Date.ToDateTime(TimeOnly.MinValue), DayType.Holiday, "School holiday"));
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods.Count.ShouldBe(2);
        calendar.Publish(f.User.Id); await GetRequiredService<IRepository<AcademicCalendar, Guid>>().UpdateAsync(calendar, true);
        var day = (await Service.GetScheduleAsync(f.Request)).Days.Single();
        day.DayType.ShouldBe("holiday"); day.DayTitle.ShouldBe("School holiday"); day.Periods.ShouldBeEmpty();
    });

    [Fact]
    public Task Confirmed_exact_slot_substitution_is_applied_and_cancelled_request_is_ignored() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var slot = f.Timetable.Slots.First(); var subject = slot.Subjects.Single();
        var change = new SubstitutionRequest(Guid.NewGuid(), id, f.Date.ToDateTime(TimeOnly.MinValue), "1", slot.StartTime, slot.EndTime,
            "Class 8", "A", "Mathematics", "Original teacher", "Room", "Private leave reason", SubstitutionPriority.Normal,
            SubstitutionSource.Manual, true, academicYearId: f.Year.Id, standardId: f.Student.StandardId, divisionId: f.Student.DivisionId,
            timetableId: f.Timetable.Id, timetableSlotId: slot.Id, timetableSlotSubjectId: subject.Id, subjectId: subject.SubjectId);
        change.Assign(Guid.NewGuid(), "Replacement teacher", null, null, null, "Low", SubstitutionConflictStatus.Clear, null, false);
        await Insert(change);
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods[0].ReplacementTeacher.ShouldBeNull();
        change.Acknowledge(null); await GetRequiredService<IRepository<SubstitutionRequest, Guid>>().UpdateAsync(change, true);
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods[0].ReplacementTeacher!.Name.ShouldBe("Replacement teacher");
        change.Cancel(null); await GetRequiredService<IRepository<SubstitutionRequest, Guid>>().UpdateAsync(change, true);
        (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods[0].ReplacementTeacher.ShouldBeNull();
    });

    [Fact]
    public Task Half_day_returns_only_periods_inside_published_working_hours() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var calendar = new AcademicCalendar(Guid.NewGuid(), id, f.Medium.Id, f.Year.Id, "Half day", f.Year.StartDate, f.Year.EndDate);
        calendar.Publish(f.User.Id); await Insert(calendar);
        await Insert(new AcademicCalendarDay(Guid.NewGuid(), id, calendar.Id, f.Date.ToDateTime(TimeOnly.MinValue), DayType.HalfDay, "Short school day",
            startTime: new TimeSpan(8,30,0), endTime: new TimeSpan(9,15,0)));
        var day = (await Service.GetScheduleAsync(f.Request)).Days.Single();
        day.DayType.ShouldBe("halfDay"); day.Periods.Count.ShouldBe(1);
        day.Periods.Single().Subject!.Name.ShouldBe("Mathematics");
    });

    [Fact]
    public Task Parent_with_multiple_children_must_select_a_linked_child() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: true); using var identity = SignIn(f.User.Id);
        var sibling = await Insert(new Student(Guid.NewGuid(), id, "Second", "", "Child", f.Year.Id, f.Student.AcademicBoardId!.Value,
            f.Medium.Id, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId, f.Student.DivisionId));
        await Insert(new StudentPortalAccountLink(Guid.NewGuid(), id, sibling.Id, f.User.Id, "guardian", "mother", Guid.NewGuid()));
        await Should.ThrowAsync<BusinessException>(() => Service.GetContextAsync());
        (await Service.GetContextAsync(f.Student.Id)).Student.Id.ShouldBe(f.Student.Id);
        (await Service.GetContextAsync(sibling.Id)).Student.Name.ShouldBe("Second Child");
    });

    [Fact]
    public Task Invalid_range_and_historical_year_are_rejected() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id); using var identity = SignIn(f.User.Id);
        var request = f.Request; request.ToDate = request.FromDate.AddDays(7);
        await Should.ThrowAsync<BusinessException>(() => Service.GetScheduleAsync(request));
        request.ToDate = request.FromDate.AddDays(-1);
        await Should.ThrowAsync<BusinessException>(() => Service.GetScheduleAsync(request));
        await Should.ThrowAsync<BusinessException>(() => Service.GetContextAsync(f.Student.Id, Guid.NewGuid()));
    });

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public Task Left_student_is_denied_on_existing_session_and_legacy_fallback(bool parent, bool legacySelf) => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id, parent: parent, legacySelf: legacySelf, legacyPlacement: true);
		using var identity = SignIn(f.User.Id);
		(await Service.GetContextAsync(f.Student.Id)).Student.Id.ShouldBe(f.Student.Id);
		f.Student.ChangeOperationalStatus(StudentStatus.Left, "School exit");
		await GetRequiredService<IRepository<Student, Guid>>().UpdateAsync(f.Student, true);
		(await GetRequiredService<StudentPortalAccessService>().GetMyStudentsAsync()).ShouldBeEmpty();
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
		(await GetRequiredService<StudentPortalLoginService>().CanAccessAsync(f.User)).ShouldBeFalse();
	});

	[Fact]
	public Task Pending_temporary_password_blocks_existing_portal_session() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id);
		f.User.SetShouldChangePasswordOnNextLogin(true);
		await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetContextAsync(f.Student.Id));
	});

	[Fact]
	public Task Parent_mobile_alias_is_unique_email_backed_and_tenant_bound() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id, parent: true);
		var roles = GetRequiredService<IdentityRoleManager>();
		(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", id))).Succeeded.ShouldBeTrue();
		var manager = GetRequiredService<IdentityUserManager>();
		(await manager.AddToRoleAsync(f.User, "Parent")).Succeeded.ShouldBeTrue();
		f.User.SetPhoneNumber("9988776655", false);
		await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
		var login = GetRequiredService<StudentPortalLoginService>();
		(await login.FindMobileUsernameAsync("9988776655")).ShouldBe(f.User.UserName);
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			(await login.FindMobileUsernameAsync("9988776655")).ShouldBeNull();
		var other = new IdentityUser(Guid.NewGuid(), "other", "other@example.test", id);
		other.SetPhoneNumber("9988776655", false); await Insert(other);
		(await login.FindMobileUsernameAsync("9988776655")).ShouldBeNull();
	});
}
