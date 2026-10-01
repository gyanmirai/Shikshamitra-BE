using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
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
    private async Task<T> Insert<T>(T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
        await GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
    private IDisposable SignIn(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "test")));

    private async Task<Fixture> Seed(Guid tenant, bool parent = false, bool publish = true)
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).Date;
        var year = await Insert(await GetRequiredService<AcademicYearManager>().CreateAsync("Current", today.AddDays(-30), today.AddDays(300)));
        var board = await Insert(await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
        var medium = await Insert(await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
        var stage = await Insert(await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
        var standard = await Insert(await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 8", 12, null));
        var division = await Insert(await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30));
        var student = await Insert(new Student(Guid.NewGuid(), tenant, "Real", "", "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id));
        var enrollment = await Insert(StudentEnrollment.Create(Guid.NewGuid(), tenant, student.Id, year.Id, standard.Id, division.Id, 12));
        var user = new IdentityUser(Guid.NewGuid(), "portal", "portal@example.test", tenant);
        user.SetEmailConfirmed(true);
        await Insert(user);
        var link = await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenant, student.Id, user.Id,
            parent ? "guardian" : "student", parent ? "mother" : "self", Guid.NewGuid()));
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
    public Task Draft_timetable_and_inactive_enrollment_are_explicit_empty_states() => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, publish: false); using var identity = SignIn(f.User.Id);
        (await Service.GetContextAsync()).Availability.ShouldBe("noPublishedTimetable");
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
        var slot = new TimetableSlot(Guid.NewGuid(), f.Timetable.Id, TimetableDayOfWeek.Monday, 3, new(9,30), new(10,15), SlotType.Period, electiveGroupId: group.Id);
        slot.AddSubject(new(Guid.NewGuid(), slot.Id, first.Id)); slot.AddSubject(new(Guid.NewGuid(), slot.Id, second.Id));
        f.Timetable.AddSlot(slot); await GetRequiredService<IRepository<TimetableEntity, Guid>>().UpdateAsync(f.Timetable, true);
        await Insert(new StudentElectiveSelection(Guid.NewGuid(), id, f.Student.Id, group.Id, second.Id, f.Medium.Id));
        var periods = (await Service.GetScheduleAsync(f.Request)).Days.Single().Periods;
        periods.ShouldContain(x => x.Subject != null && x.Subject.Name == "Gujarati");
        periods.ShouldNotContain(x => x.Subject != null && x.Subject.Name == "Hindi");
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
}
