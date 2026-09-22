using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class TeacherAvailabilityAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private const string AvailabilityOverlapMessage =
        "Teacher is already marked unavailable for the selected date and time. Please choose a different time slot.";

    private readonly ITeacherAvailabilityAppService _teacherAvailabilityAppService;
    private readonly IRepository<TeacherAvailability, Guid> _availabilityRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public TeacherAvailabilityAppService_Tests()
    {
        _teacherAvailabilityAppService = GetRequiredService<ITeacherAvailabilityAppService>();
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
    public async Task GetListAsync_Should_Not_Fail_When_Attendance_Sync_Encounters_A_Late_Overlap()
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
            var result = await _teacherAvailabilityAppService.GetListAsync(
                new GetTeacherAvailabilityInput
                {
                    Date = availabilityDate,
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(1);
            result.Items.ShouldHaveSingleItem().SourceType
                .ShouldBe(TeacherAvailabilitySource.Attendance.ToString());
        }
    }

    private async Task<Employee> CreateEmployeeAsync(
        Guid userId,
        string firstName,
        string lastName,
        Guid? reportingManagerId = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        await _identityUserRepository.InsertAsync(
            new IdentityUser(userId, $"availability-{suffix[..8]}", $"{suffix[..12]}@test.local", tenantId: null),
            autoSave: true);
        await _roleRepository.InsertAsync(
            new IdentityRole(roleId, $"availability-{suffix[..8]}", tenantId: null),
            autoSave: true);

        var employee = new Employee(
            Guid.NewGuid(),
            tenantId: null,
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
