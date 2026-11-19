using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Academic.ClassManagement.TeacherAssignment;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class TeacherAvailabilityAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private const string AvailabilityOverlapMessage =
        "Teacher is already marked unavailable for the selected date and time. Please choose a different time slot.";

    private readonly ITeacherAvailabilityAppService _teacherAvailabilityAppService;
    private readonly ITimetableAppService _timetableAppService;
    private readonly ITeacherAssignmentAppService _teacherAssignmentAppService;
    private readonly IRepository<TeacherAvailability, Guid> _availabilityRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public TeacherAvailabilityAppService_Tests()
    {
        _teacherAvailabilityAppService = GetRequiredService<ITeacherAvailabilityAppService>();
        _timetableAppService = GetRequiredService<ITimetableAppService>();
        _teacherAssignmentAppService = GetRequiredService<ITeacherAssignmentAppService>();
        _availabilityRepository = GetRequiredService<IRepository<TeacherAvailability, Guid>>();
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _employeeAttendanceRepository = GetRequiredService<IEmployeeAttendanceRepository>();
        _roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
        _identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Theory]
    [InlineData("10:00", "11:00")]
    [InlineData("10:30", "11:30")]
    public async Task CreateAsync_Should_Reject_Duplicate_Or_Overlapping_Availability(
        string startTime,
        string endTime)
    {
        var teacherId = Guid.NewGuid();
        var availabilityDate = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            await _availabilityRepository.InsertAsync(
                new TeacherAvailability(
                    Guid.NewGuid(),
                    null,
                    "Test Teacher",
                    availabilityDate,
                    new TimeOnly(10, 0),
                    new TimeOnly(11, 0),
                    TeacherAvailabilityStatus.Unavailable,
                    TeacherAvailabilitySource.Manual,
                    "Existing unavailability",
                    false,
                    true,
                    teacherId: teacherId),
                autoSave: true);

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _teacherAvailabilityAppService.CreateAsync(new CreateTeacherAvailabilityDto
                {
                    TeacherId = teacherId,
                    TeacherName = "Test Teacher",
                    AvailabilityDate = availabilityDate,
                    StartTime = startTime,
                    EndTime = endTime,
                    Reason = "New unavailability"
                }));

            exception.Message.ShouldBe(AvailabilityOverlapMessage);
        }
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_The_Selected_Academic_Year()
    {
        var academicYearId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var availabilityDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var created = await _teacherAvailabilityAppService.CreateAsync(new CreateTeacherAvailabilityDto
            {
                AcademicYearId = academicYearId,
                TeacherId = teacherId,
                TeacherName = "Persistence Test Teacher",
                AvailabilityDate = availabilityDate,
                StartTime = "10:00",
                EndTime = "11:00",
                Reason = "Persistence regression test",
            });

            var persisted = await _availabilityRepository.GetAsync(created.Id);

            persisted.AcademicYearId.ShouldBe(academicYearId);
            persisted.AvailabilityDate.ShouldBe(availabilityDate);
        }
    }

    [Fact]
    public async Task GetAffectedLecturesAsync_Should_Infer_The_Date_Academic_Year()
    {
        var tenantId = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();
        var availabilityDate = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        using (currentTenant.Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            await GrantCurrentActorScopeAsync(tenantId);
            var teacher = await CreateEmployeeAsync(
                Guid.NewGuid(),
                "Academic",
                "Year Teacher",
                tenantId: tenantId);
            await SeedTimetableContextAsync(
                tenantId,
                teacher.Id,
                "2026-2027",
                new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31),
                "Current Class",
                new TimeOnly(8, 0),
                new TimeOnly(8, 45));
            await SeedTimetableContextAsync(
                tenantId,
                teacher.Id,
                "2025-2026",
                new DateTime(2025, 6, 1),
                new DateTime(2026, 5, 31),
                "Historical Class",
                new TimeOnly(12, 30),
                new TimeOnly(13, 0));

            var result = await _teacherAvailabilityAppService.GetAffectedLecturesAsync(
                new CreateTeacherAvailabilityDto
                {
                    TeacherId = teacher.Id,
                    TeacherName = teacher.EmployeeName,
                    AvailabilityDate = availabilityDate,
                    StartTime = "07:00",
                    EndTime = "17:30",
                    IsFullDay = true,
                    Reason = "Academic year scope regression"
                });

            var lecture = result.Items.ShouldHaveSingleItem();
            lecture.ClassName.ShouldBe("Current Class");
            lecture.StartTime.ShouldBe("08:00");
            lecture.EndTime.ShouldBe("08:45");
        }
    }

    [Fact]
    public async Task GetAffectedLecturesAsync_Should_Only_Include_The_Selected_Medium()
    {
        var tenantId = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();
        var availabilityDate = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        using (currentTenant.Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Medium", "Teacher", tenantId: tenantId);
            await GrantCurrentActorScopeAsync(tenantId);
            var selected = await SeedTimetableContextAsync(
                tenantId, teacher.Id, "2026-2027", new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31), "Selected Class", new TimeOnly(8, 0), new TimeOnly(8, 45));
            await SeedTimetableContextAsync(
                tenantId, teacher.Id, "2026-2027", new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31), "Other Medium Class", new TimeOnly(9, 0),
                new TimeOnly(9, 45), selected.AcademicYearId);

            var result = await _teacherAvailabilityAppService.GetAffectedLecturesAsync(
                new CreateTeacherAvailabilityDto
                {
                    AcademicYearId = selected.AcademicYearId,
                    AcademicMediumId = selected.AcademicMediumId,
                    TeacherId = teacher.Id,
                    AvailabilityDate = availabilityDate,
                    StartTime = "07:00",
                    EndTime = "17:30"
                });

            result.Items.ShouldHaveSingleItem().ClassName.ShouldBe("Selected Class");
        }
    }

    [Fact]
    public async Task GetAffectedLecturesAsync_Should_Return_No_Lectures_For_An_Unscheduled_Teacher()
    {
        var tenantId = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();
        var availabilityDate = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        using (currentTenant.Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var scheduledTeacher = await CreateEmployeeAsync(
                Guid.NewGuid(), "Scheduled", "Teacher", tenantId: tenantId);
            var unscheduledTeacher = await CreateEmployeeAsync(
                Guid.NewGuid(), "Unscheduled", "Teacher", tenantId: tenantId);
            var context = await SeedTimetableContextAsync(
                tenantId, scheduledTeacher.Id, "2026-2027", new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31), "Scheduled Class", new TimeOnly(8, 0),
                new TimeOnly(8, 45));

            var result = await _teacherAvailabilityAppService.GetAffectedLecturesAsync(
                new CreateTeacherAvailabilityDto
                {
                    AcademicYearId = context.AcademicYearId,
                    AcademicMediumId = context.AcademicMediumId,
                    TeacherId = unscheduledTeacher.Id,
                    AvailabilityDate = availabilityDate,
                    StartTime = "07:00",
                    EndTime = "17:30"
                });

            result.Items.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Teacher_Timetable_And_Affected_Lectures_Should_Resolve_The_Same_User_Id_Slots()
    {
        var tenantId = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();
        var userId = Guid.NewGuid();
        var availabilityDate = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        using (currentTenant.Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var teacher = await CreateEmployeeAsync(userId, "Legacy", "Teacher", tenantId: tenantId);
            var context = await SeedTimetableContextAsync(
                tenantId, userId, "2026-2027", new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31), "Legacy Class", new TimeOnly(8, 0), new TimeOnly(8, 45));

            using (var unitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
            {
                await _roleRepository.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", tenantId), autoSave: true);
                var identityUser = await _identityUserRepository.GetAsync(userId);
                var roleResult = await GetRequiredService<IdentityUserManager>().AddToRoleAsync(identityUser, "admin");
                roleResult.Succeeded.ShouldBeTrue();
                await unitOfWork.CompleteAsync();
            }

            using (_currentPrincipalAccessor.Change(BuildPrincipal(userId, "admin")))
            {
                var affected = await _teacherAvailabilityAppService.GetAffectedLecturesAsync(
                    new CreateTeacherAvailabilityDto
                    {
                        AcademicYearId = context.AcademicYearId,
                        AcademicMediumId = context.AcademicMediumId,
                        TeacherId = teacher.Id,
                        AvailabilityDate = availabilityDate,
                        StartTime = "07:00",
                        EndTime = "17:30"
                    });
                var timetable = await _timetableAppService.GetByEmployeeAsync(teacher.Id, context.AcademicYearId);

                affected.Items.ShouldHaveSingleItem().ClassName.ShouldBe("Legacy Class");
                timetable.Days["WEDNESDAY"].ShouldHaveSingleItem().StandardName.ShouldBe("Legacy Class");
            }
        }
    }

    [Fact]
    public async Task SaveTeacherAssignmentsForClassAsync_Should_Clear_An_Explicitly_Empty_Teacher_List()
    {
        var tenantId = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Allocation", "Teacher", tenantId: tenantId);
            var context = await SeedTimetableContextAsync(
                tenantId, teacher.Id, "2026-2027", new DateTime(2026, 6, 1),
                new DateTime(2027, 5, 31), "Allocation Class", new TimeOnly(8, 0), new TimeOnly(8, 45));
            var allocationId = Guid.NewGuid();
            var allocation = new TeacherSubjectAllocation(
                allocationId, context.StandardId, context.DivisionId, context.SubjectId);
            allocation.Teachers.Add(new AllocatedTeacher(allocationId, teacher.Id));
            var allocations = GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>();
            await allocations.InsertAsync(allocation, autoSave: true);

            var result = await _teacherAssignmentAppService.SaveTeacherAssignmentsForClassAsync(
                new SaveTeacherAssignmentsDto
                {
                    AcademicMediumId = context.AcademicMediumId,
                    ClassId = context.StandardId,
                    Assignments = [new TeacherAssignmentUpdateDto
                    {
                        DivisionId = context.DivisionId,
                        SubjectId = context.SubjectId,
                        TeacherIds = []
                    }]
                });

            result.Success.ShouldBeTrue();
            result.Message.ShouldContain("1 updated");
            using (var verificationUnitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
            {
                var persisted = await allocations.GetAsync(allocationId, includeDetails: true);
                persisted.Teachers.Where(teacher => !teacher.IsDeleted).ShouldBeEmpty();
                await verificationUnitOfWork.CompleteAsync();
            }

            await _teacherAssignmentAppService.SaveTeacherAssignmentsForClassAsync(
                new SaveTeacherAssignmentsDto
                {
                    AcademicMediumId = context.AcademicMediumId,
                    ClassId = context.StandardId,
                    Assignments = [new TeacherAssignmentUpdateDto
                    {
                        DivisionId = context.DivisionId,
                        SubjectId = context.SubjectId,
                        TeacherIds = [teacher.Id]
                    }]
                });

            using (var verificationUnitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
            {
                var persisted = await allocations.GetAsync(allocationId, includeDetails: true);
                persisted.Teachers.Where(item => !item.IsDeleted)
                    .ShouldHaveSingleItem().TeacherId.ShouldBe(teacher.Id);
                await verificationUnitOfWork.CompleteAsync();
            }
        }
    }

    [Fact]
    public async Task GetListAsync_Should_Not_Create_Availability_From_Attendance()
    {
        var employee = await CreateEmployeeAsync(Guid.NewGuid(), "Akshay", "Patel");
        var attendanceDate = new DateOnly(2026, 9, 17);

        await _employeeAttendanceRepository.InsertAsync(
            new EmployeeAttendance
            {
                EmployeeId = employee.Id,
                AttendanceDate = attendanceDate,
                StatusType = EmployeeAttendanceStatusType.FullDayLeave,
                Source = AttendanceRecordSource.Manual,
                Remarks = "Approved leave"
            },
            autoSave: true);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var result = await _teacherAvailabilityAppService.GetListAsync(
                new GetTeacherAvailabilityInput
                {
                    Date = attendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(0);
            (await _availabilityRepository.GetCountAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task GetListAsync_Should_Return_Reportee_Availability_Only()
    {
        var managerUserId = Guid.NewGuid();
        var manager = await CreateEmployeeAsync(managerUserId, "Jignesh", "Jinjala");
        var reportee = await CreateEmployeeAsync(
            Guid.NewGuid(),
            "Akshay",
            "Patel",
            manager.Id);
        var unrelatedEmployee = await CreateEmployeeAsync(
            Guid.NewGuid(),
            "Unrelated",
            "Teacher");
        var attendanceDate = new DateOnly(2026, 9, 17);

        var availabilityDate = attendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await _availabilityRepository.InsertManyAsync(
            [
                new TeacherAvailability(
                    Guid.NewGuid(),
                    null,
                    "Akshay Patel",
                    availabilityDate,
                    new TimeOnly(9, 0),
                    new TimeOnly(14, 15),
                    TeacherAvailabilityStatus.Unavailable,
                    TeacherAvailabilitySource.Attendance,
                    "Approved leave",
                    true,
                    true,
                    teacherId: reportee.Id),
                new TeacherAvailability(
                    Guid.NewGuid(),
                    null,
                    "Unrelated Teacher",
                    availabilityDate,
                    new TimeOnly(9, 0),
                    new TimeOnly(14, 15),
                    TeacherAvailabilityStatus.Unavailable,
                    TeacherAvailabilitySource.Attendance,
                    "Unrelated leave",
                    true,
                    true,
                    teacherId: unrelatedEmployee.Id)
            ],
            autoSave: true);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(managerUserId, "teacher")))
        {
            var result = await _teacherAvailabilityAppService.GetListAsync(
                new GetTeacherAvailabilityInput
                {
                    Date = attendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(1);
            var availability = result.Items.ShouldHaveSingleItem();
            availability.TeacherId.ShouldBe(reportee.Id);
            availability.TeacherName.ShouldBe("Akshay Patel");
            availability.SourceType.ShouldBe(TeacherAvailabilitySource.Attendance.ToString());

            (await _availabilityRepository.GetCountAsync()).ShouldBe(2);
        }
    }

    [Fact]
    public async Task GetListAsync_Should_Not_Fail_When_Attendance_Overlaps_Manual_Availability()
    {
        var teacher = await CreateEmployeeAsync(
            Guid.NewGuid(),
            "Existing",
            "Teacher");
        var attendanceDate = new DateOnly(2026, 9, 18);
        var availabilityDate = attendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await _availabilityRepository.InsertAsync(
            new TeacherAvailability(
                Guid.NewGuid(),
                null,
                teacher.EmployeeName,
                availabilityDate,
                new TimeOnly(9, 0),
                new TimeOnly(14, 15),
                TeacherAvailabilityStatus.Unavailable,
                TeacherAvailabilitySource.Manual,
                "Already marked unavailable",
                true,
                true,
                teacherId: teacher.Id),
            autoSave: true);
        await _employeeAttendanceRepository.InsertAsync(
            new EmployeeAttendance
            {
                EmployeeId = teacher.Id,
                AttendanceDate = attendanceDate,
                StatusType = EmployeeAttendanceStatusType.FullDayLeave,
                Source = AttendanceRecordSource.Manual,
                Remarks = "Approved leave"
            },
            autoSave: true);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var result = await _teacherAvailabilityAppService.GetListAsync(
                new GetTeacherAvailabilityInput
                {
                    Date = availabilityDate,
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(1);
            result.Items.ShouldHaveSingleItem().SourceType
                .ShouldBe(TeacherAvailabilitySource.Manual.ToString());

            var attendanceRows = await _availabilityRepository.GetListAsync(
                row => row.SourceType == TeacherAvailabilitySource.Attendance);
            attendanceRows.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SyncDateRangeAsync_Should_Deduplicate_Legacy_Attendance_Before_Listing_Availability()
    {
        var teacher = await CreateEmployeeAsync(
            Guid.NewGuid(),
            "Duplicate",
            "Attendance");
        var attendanceDate = new DateOnly(2026, 9, 19);
        var availabilityDate = attendanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        foreach (var remarks in new[] { "First attendance row", "Legacy duplicate attendance row" })
        {
            await _employeeAttendanceRepository.InsertAsync(
                new EmployeeAttendance
                {
                    EmployeeId = teacher.Id,
                    AttendanceDate = attendanceDate,
                    StatusType = EmployeeAttendanceStatusType.FullDayLeave,
                    Source = AttendanceRecordSource.Manual,
                    Remarks = remarks
                },
                autoSave: true);
        }

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            using var unitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var sync = GetRequiredService<TakeAttendanceTeacherAvailabilitySyncService>();
            await sync.SyncDateRangeAsync(availabilityDate, availabilityDate);
            await sync.SyncDateRangeAsync(availabilityDate, availabilityDate);

            var result = await _teacherAvailabilityAppService.GetListAsync(
                new GetTeacherAvailabilityInput
                {
                    Date = availabilityDate,
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(1);
            result.Items.ShouldHaveSingleItem().SourceType
                .ShouldBe(TeacherAvailabilitySource.Attendance.ToString());
            (await _availabilityRepository.GetListAsync(x => !x.IsCancelled && x.TeacherId == teacher.Id))
                .ShouldHaveSingleItem();
            await unitOfWork.CompleteAsync();
        }
    }

    [Theory]
    [InlineData(EmployeeAttendanceStatusType.FullDayLeave, "15:00", true)]
    [InlineData(EmployeeAttendanceStatusType.HalfDayLeave, "11:30", false)]
    public async Task AttendanceSync_Should_Use_Employee_Shift_And_Cancel_When_Present(
        EmployeeAttendanceStatusType status, string expectedEnd, bool isFullDay)
    {
        var employee = await CreateEmployeeAsync(Guid.NewGuid(), "Shift", "Teacher");
        var shift = await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(
            new ShiftManagement { ShiftName = "Teaching shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(15, 0) },
            autoSave: true);
        employee.ShiftId = shift.Id;
        await _employeeRepository.UpdateAsync(employee, autoSave: true);
        var attendance = await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
        {
            EmployeeId = employee.Id,
            AttendanceDate = new DateOnly(2026, 9, 30),
            StatusType = status
        }, autoSave: true);
        var sync = GetRequiredService<TakeAttendanceTeacherAvailabilitySyncService>();

        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
            await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
            var row = (await _availabilityRepository.GetListAsync(x => !x.IsCancelled)).ShouldHaveSingleItem();
            row.StartTime.ToString("HH:mm").ShouldBe("08:00");
            row.EndTime.ToString("HH:mm").ShouldBe(expectedEnd);
            row.IsFullDay.ShouldBe(isFullDay);
            row.SourceEntityId.ShouldBe(attendance.Id);

            attendance.StatusType = EmployeeAttendanceStatusType.Present;
            await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
            (await _availabilityRepository.GetListAsync(x => !x.IsCancelled)).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task GetListAsync_Should_Correct_Legacy_Attendance_Window_And_Recalculate_Early_Lectures()
    {
        var tenantId = Guid.NewGuid();
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
        {
            var employee = await CreateEmployeeAsync(Guid.NewGuid(), "LegacyShift", "Teacher", tenantId: tenantId);
            await GrantCurrentActorScopeAsync(tenantId);
            var shift = await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(
                new ShiftManagement { ShiftName = "Teaching shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(15, 0) },
                autoSave: true);
            employee.ShiftId = shift.Id;
            await _employeeRepository.UpdateAsync(employee, autoSave: true);
            var context = await SeedTimetableContextAsync(
                tenantId, employee.Id, "2026-2027", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31),
                "Early Class", new TimeOnly(8, 0), new TimeOnly(8, 45));
            await SeedTimetableContextAsync(
                tenantId, employee.Id, "2026-2027-late", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31),
                "Late Class", new TimeOnly(14, 30), new TimeOnly(15, 0), existingAcademicYearId: context.AcademicYearId);
            var date = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            var legacy = await _availabilityRepository.InsertAsync(new TeacherAvailability(
                Guid.NewGuid(), tenantId, employee.EmployeeName, date,
                new TimeOnly(9, 0), new TimeOnly(14, 15), TeacherAvailabilityStatus.Unavailable,
                TeacherAvailabilitySource.Attendance, "Marked absent in HR attendance.", true, true,
                academicYearId: context.AcademicYearId, teacherId: employee.Id, affectedLectureCount: 0), autoSave: true);

            var result = await _teacherAvailabilityAppService.GetListAsync(new GetTeacherAvailabilityInput
            {
                Date = date, AcademicYearId = context.AcademicYearId, MaxResultCount = 50
            });
            var row = result.Items.ShouldHaveSingleItem();
            row.Id.ShouldBe(legacy.Id);
            row.StartTime.ShouldBe("08:00");
            row.EndTime.ShouldBe("15:00");
            row.AffectedLectureCount.ShouldBe(2);
            (await _availabilityRepository.GetCountAsync()).ShouldBe(1);
            var fixturePath = Environment.GetEnvironmentVariable("SHIKSHAMITRA_AVAILABILITY_FIXTURE");
            if (!string.IsNullOrWhiteSpace(fixturePath))
            {
                await System.IO.File.WriteAllTextAsync(fixturePath, System.Text.Json.JsonSerializer.Serialize(
                    result, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
            }
        }
    }

    private async Task<Employee> CreateEmployeeAsync(
        Guid userId,
        string firstName,
        string lastName,
        Guid? reportingManagerId = null,
        Guid? tenantId = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        await _identityUserRepository.InsertAsync(
            new IdentityUser(userId, $"availability-{suffix[..8]}", $"{suffix[..12]}@test.local", tenantId),
            autoSave: true);
        await _roleRepository.InsertAsync(
            new IdentityRole(roleId, $"availability-{suffix[..8]}", tenantId),
            autoSave: true);

        var employee = new Employee(
            Guid.NewGuid(),
            tenantId,
            userId: userId,
            employeeCode: $"AVL{suffix[..10]}",
            basicInfo: new EmployeeBasicInfo(
                ProfilePictureBlobName: null,
                LastName: lastName,
                FirstName: firstName,
                MiddleName: null,
                ShortName: firstName,
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
            bankDetails: new BankDetails(null, null, null, null, null),
            workExperiences: null);
        if (reportingManagerId.HasValue)
        {
            employee.AssignReportingManager(reportingManagerId.Value);
        }

        return await _employeeRepository.InsertAsync(employee, autoSave: true);
    }

    private async Task<(Guid AcademicYearId, Guid AcademicMediumId, Guid StandardId, Guid DivisionId, Guid SubjectId)> SeedTimetableContextAsync(
        Guid tenantId,
        Guid teacherId,
        string academicYearName,
        DateTime academicYearStart,
        DateTime academicYearEnd,
        string standardName,
        TimeOnly slotStart,
        TimeOnly slotEnd,
        Guid? existingAcademicYearId = null)
    {
        var academicYearId = existingAcademicYearId ?? Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var mediumId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        if (!existingAcademicYearId.HasValue)
        {
            await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
                new AcademicYear(academicYearId, tenantId, academicYearName, academicYearStart, academicYearEnd),
                autoSave: true);
        }
        await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
            new EducationBoard(
                boardId,
                tenantId,
                EducationBoardType.CBSE,
                academicYearId,
                academicYearStart,
                academicYearEnd),
            autoSave: true);
        await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
            new EducationMedium(mediumId, tenantId, $"English {academicYearName}", boardId),
            autoSave: true);
        await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
            new EducationMediumStage(stageId, tenantId, $"Stage {academicYearName}", null, mediumId),
            autoSave: true);
        await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
            new Standard(standardId, tenantId, standardName, 6, stageId, null),
            autoSave: true);
        await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
            new Division(divisionId, tenantId, "A", standardId, 30, $"R-{academicYearName}"),
            autoSave: true);
        await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(
            new Subject(subjectId, tenantId, $"Subject {academicYearName}", "#00A76F", mediumId),
            autoSave: true);

        var timetable = new ShikshaMitra.Academic.Timetable.Timetables.Timetable(
            Guid.NewGuid(),
            tenantId,
            standardId,
            divisionId);
        var slot = new TimetableSlot(
            Guid.NewGuid(),
            timetable.Id,
            TimetableDayOfWeek.Wednesday,
            1,
            slotStart,
            slotEnd,
            SlotType.Period);
        slot.AddSubject(new TimetableSlotSubject(
            Guid.NewGuid(),
            slot.Id,
            subjectId,
            teacherId));
        timetable.AddSlot(slot);
        timetable.Publish();

        await GetRequiredService<ITimetableRepository>().InsertAsync(timetable, autoSave: true);
        return (academicYearId, mediumId, standardId, divisionId, subjectId);
    }

    private ClaimsPrincipal BuildPrincipal(Guid userId, string roleName)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"user-{userId:N}"),
            new(AbpClaimTypes.Role, roleName)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
