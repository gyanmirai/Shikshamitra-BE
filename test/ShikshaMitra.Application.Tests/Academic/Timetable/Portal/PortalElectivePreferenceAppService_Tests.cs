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

public class PortalElectivePreferenceAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private IPortalElectivePreferenceAppService Service => GetRequiredService<IPortalElectivePreferenceAppService>();
	[Fact]
	public Task Direct_calls_reject_foreign_child_tenant_historical_year_and_inactive_identity() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id);
		var group = await AddGroup(f);
		var input = Input(f, group);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetAsync(Guid.NewGuid(), f.Year.Id));
		input.StudentId = Guid.NewGuid();
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.SubmitAsync(input));
		input.StudentId = f.Student.Id; input.AcademicYearId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetAsync(f.Student.Id, f.Year.Id));
		f.User.SetIsActive(false); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.GetAsync(f.Student.Id, f.Year.Id));
		input.AcademicYearId = f.Year.Id;
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.SubmitAsync(input));
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Legacy_assignments_remain_read_only_without_mobile_receipt(bool locked) => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id); var group = await AddGroup(f);
		var input = Input(f, group);
		var selection = new StudentElectiveSelection(Guid.NewGuid(), id, f.Student.Id, group.Id, input.PreferredSubjectId, f.Medium.Id, "Original preference");
		if (locked) selection.LockSelection(); await Insert(selection);
		var state = await Service.GetAsync(f.Student.Id, f.Year.Id);
		state.Groups.Single().Status.ShouldBe(locked ? "approved" : "pendingApproval");
		state.Groups.Single().CanSubmit.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input))).Code.ShouldBe("Academic:ElectivePreference:Conflict");
		(await GetRequiredService<IRepository<StudentElectivePreferenceSubmission, Guid>>().CountAsync()).ShouldBe(0);
	});

	[Fact]
	public Task Soft_deleted_legacy_assignment_requires_school_action_without_reinsert() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id); var group = await AddGroup(f); var input = Input(f, group);
		var selection = await Insert(new StudentElectiveSelection(Guid.NewGuid(), id, f.Student.Id, group.Id, input.PreferredSubjectId, f.Medium.Id));
		await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().DeleteAsync(selection, true);
		(await Service.SubmitAsync(input)).Groups.Single().Status.ShouldBe("schoolActionRequired");
		(await GetRequiredService<IRepository<StudentElectivePreferenceSubmission, Guid>>().CountAsync()).ShouldBe(0);
	});

	[Fact]
	public Task Unpublished_class_options_and_missing_occurrences_are_not_selectable() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id); var group = await AddGroup(f); var input = Input(f, group);
		input.PreferredSubjectId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input));
		input.PreferredSubjectId = group.Subjects.First().SubjectId; input.ElectiveGroupId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input));
		var slot = new TimetableSlot(Guid.NewGuid(), f.Timetable.Id, TimetableDayOfWeek.Friday, 3, new(9,30), new(10,15), SlotType.Period, electiveGroupId: group.Id);
		slot.AddSubject(new(Guid.NewGuid(), slot.Id, group.Subjects.Last().SubjectId));
		f.Timetable.AddSlot(slot); await GetRequiredService<IRepository<TimetableEntity, Guid>>().UpdateAsync(f.Timetable, true);
		var state = await Service.GetAsync(f.Student.Id, f.Year.Id);
		state.Groups.Single().Options.Count.ShouldBe(1);
		state.Groups.Single().Options.Single().SubjectId.ShouldBe(group.Subjects.Last().SubjectId);
		var master = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(group.Subjects.Last().SubjectId);
		master.Archive("School review"); await GetRequiredService<IRepository<Subject, Guid>>().UpdateAsync(master, true);
		(await Service.GetAsync(f.Student.Id, f.Year.Id)).Availability.ShouldBe("electiveSetupRequired");
		master.Restore(); await GetRequiredService<IRepository<Subject, Guid>>().UpdateAsync(master, true);
		var published = (await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetListAsync()).Where(x => x.ElectiveGroupId == group.Id);
		foreach (var item in published) { item.ChangeStatus(SubjectStatus.Draft); await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(item, true); }
		state = await Service.GetAsync(f.Student.Id, f.Year.Id);
		state.Availability.ShouldBe("electiveSetupRequired"); state.Groups.Single().CanSubmit.ShouldBeFalse();
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task No_electives_preserves_published_and_unpublished_availability(bool published) => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id, publish: published); using var identity = SignIn(f.User.Id);
		var state = await Service.GetAsync(f.Student.Id, f.Year.Id);
		state.Availability.ShouldBe(published ? "available" : "noPublishedTimetable"); state.CanViewTimetable.ShouldBe(published);
		state.Groups.ShouldBeEmpty();
	});

	private static PortalElectivePreferenceSubmitInput Input(Fixture f, ElectiveGroup group) => new() {
		StudentId = f.Student.Id, AcademicYearId = f.Year.Id, ElectiveGroupId = group.Id,
		PreferredSubjectId = group.Subjects.First().SubjectId, ClientRequestId = Guid.NewGuid() };

	[Fact]
	public Task Groups_submit_independently_and_missing_choices_precede_pending_approval() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
		var f = await Seed(id); using var identity = SignIn(f.User.Id);
		var first = await AddGroup(f); var second = await AddGroup(f, "Computer Studies");
		var input = Input(f, first);
		var state = await Service.SubmitAsync(input);
		state.Availability.ShouldBe("electivePreferenceRequired");
		state.Groups.Count(x => x.Status == "selectionRequired").ShouldBe(1);
		state.Groups.Count(x => x.Status == "pendingApproval").ShouldBe(1);
		first.Archive("School review"); await GetRequiredService<IRepository<ElectiveGroup, Guid>>().UpdateAsync(first, true);
		(await Service.GetAsync(f.Student.Id, f.Year.Id)).Availability.ShouldBe("electiveSetupRequired");
		first.Restore(); await GetRequiredService<IRepository<ElectiveGroup, Guid>>().UpdateAsync(first, true);
		var other = Input(f, second); other.ClientRequestId = input.ClientRequestId;
		(await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(other))).Code.ShouldBe("Academic:ElectivePreference:Conflict");
		other.ClientRequestId = Guid.NewGuid(); state = await Service.SubmitAsync(other);
		state.Availability.ShouldBe("electiveApprovalPending"); state.Groups.ShouldAllBe(x => !x.CanSubmit);
		second.Archive("School review"); await GetRequiredService<IRepository<ElectiveGroup, Guid>>().UpdateAsync(second, true);
		(await Service.GetAsync(f.Student.Id, f.Year.Id)).Availability.ShouldBe("electiveSetupRequired");
		input.PreferredSubjectId = first.Subjects.Last().SubjectId;
		(await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input))).Code.ShouldBe("Academic:ElectivePreference:Conflict");
	});
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Submit_is_immutable_and_requires_school_lock(bool parent) => WithUnitOfWorkAsync(async () =>
    {
        var id = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(id);
        var f = await Seed(id, parent: parent); using var identity = SignIn(f.User.Id);
        var group = await AddGroup(f);
        var state = await Service.GetAsync(f.Student.Id, f.Year.Id);
        state.Availability.ShouldBe("electivePreferenceRequired");
        state.Groups.Single().CanSubmit.ShouldBeTrue();
        var input = new PortalElectivePreferenceSubmitInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
            ElectiveGroupId = group.Id, PreferredSubjectId = group.Subjects.First().SubjectId, ClientRequestId = Guid.NewGuid() };
        (await Service.SubmitAsync(input)).Groups.Single().Status.ShouldBe("pendingApproval");
        (await Service.SubmitAsync(input)).Groups.Single().Status.ShouldBe("pendingApproval");
        var receipt = (await GetRequiredService<IRepository<StudentElectivePreferenceSubmission, Guid>>().GetListAsync()).Single();
        receipt.SubmittingUserId.ShouldBe(f.User.Id);
        receipt.PreferredSubjectId.ShouldBe(input.PreferredSubjectId);
        var selection = (await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().GetListAsync()).Single();
        selection.IsLocked.ShouldBeFalse(); selection.StudentPreference.ShouldNotBeNullOrWhiteSpace();
        input.ClientRequestId = Guid.NewGuid();
        (await Should.ThrowAsync<BusinessException>(() => Service.SubmitAsync(input))).Code.ShouldBe("Academic:ElectivePreference:Conflict");
        selection.UpdateSelection(group.Subjects.Last().SubjectId); selection.LockSelection();
        await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().UpdateAsync(selection, true);
        state = await Service.GetAsync(f.Student.Id, f.Year.Id);
        state.Availability.ShouldBe("available"); state.CanViewTimetable.ShouldBeTrue();
        state.Groups.Single().PreferredSubjectId.ShouldBe(receipt.PreferredSubjectId);
        state.Groups.Single().AssignedSubjectId.ShouldBe(selection.SelectedSubjectId);
        await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().DeleteAsync(selection, true);
        (await Service.GetAsync(f.Student.Id, f.Year.Id)).Groups.Single().Status.ShouldBe("schoolActionRequired");
    });
    private async Task<ElectiveGroup> AddGroup(Fixture f, string name = "Languages")
    {
        var tenant = GetRequiredService<ICurrentTenant>().Id;
        var first = await Insert(new Subject(Guid.NewGuid(), tenant, "Hindi", "#008967", f.Medium.Id));
        var second = await Insert(new Subject(Guid.NewGuid(), tenant, "Gujarati", "#008967", f.Medium.Id));
        var group = new ElectiveGroup(Guid.NewGuid(), tenant, name, "#008967", f.Medium.Id);
        group.AddSubject(first.Id); group.AddSubject(second.Id); await Insert(group);
        foreach (var subject in new[] { first, second })
            await Insert(new AssignedSubject(Guid.NewGuid(), tenant, f.Student.StandardId!.Value, subject.Id, subject.Name,
                SubjectStatus.Published, SubjectType.Elective, group.Id));
        var slot = new TimetableSlot(Guid.NewGuid(), f.Timetable.Id, TimetableDayOfWeek.Tuesday, 3, new(9,30), new(10,15), SlotType.Period, electiveGroupId: group.Id);
        slot.AddSubject(new(Guid.NewGuid(), slot.Id, first.Id)); slot.AddSubject(new(Guid.NewGuid(), slot.Id, second.Id));
        f.Timetable.AddSlot(slot); await GetRequiredService<IRepository<TimetableEntity, Guid>>().UpdateAsync(f.Timetable, true);
        return group;
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

}
