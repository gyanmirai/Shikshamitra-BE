using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly ISubstitutionAppService _substitutionAppService;
    private readonly IRepository<SubstitutionRequest, Guid> _substitutionRepository;
    private readonly IRepository<TeacherAvailability, Guid> _availabilityRepository;
    private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public SubstitutionAppService_Tests()
    {
        _substitutionAppService = GetRequiredService<ISubstitutionAppService>();
        _substitutionRepository = GetRequiredService<IRepository<SubstitutionRequest, Guid>>();
        _availabilityRepository = GetRequiredService<IRepository<TeacherAvailability, Guid>>();
        _employeeAttendanceRepository = GetRequiredService<IEmployeeAttendanceRepository>();
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
        _identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
        _scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Theory]
    [InlineData(true, "09:00", "14:15")]
    [InlineData(false, "16:45", "17:00")]
    public async Task GetSuggestedTeachersAsync_Should_Exclude_Teacher_With_Overlapping_Unavailability(
        bool isFullDay, string unavailableStart, string unavailableEnd)
    {
        var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Unavailable", "Teacher");
        var date = DateTime.UtcNow.Date.AddDays(7);
        await _availabilityRepository.InsertAsync(new TeacherAvailability(
            Guid.NewGuid(), null, teacher.EmployeeName, date,
            TimeOnly.Parse(unavailableStart), TimeOnly.Parse(unavailableEnd),
            TeacherAvailabilityStatus.Unavailable, TeacherAvailabilitySource.Manual,
            "Teacher unavailable", isFullDay, true, teacherId: teacher.Id), autoSave: true);

        var suggestions = await _substitutionAppService.GetSuggestedTeachersAsync(new SuggestedTeacherInput
        {
            Date = new DateTimeOffset(date),
            StartTime = "16:30",
            EndTime = "17:30"
        });

        suggestions.ShouldNotContain(x => x.TeacherId == teacher.Id);
    }

    [Fact]
    public async Task GetSuggestedTeachersAsync_Should_Allow_Teacher_When_Partial_Unavailability_Does_Not_Overlap()
    {
        var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Available", "Teacher");
        var date = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        await _availabilityRepository.InsertAsync(new TeacherAvailability(
            Guid.NewGuid(), null, teacher.EmployeeName, date,
            new TimeOnly(15, 30), new TimeOnly(16, 30),
            TeacherAvailabilityStatus.Unavailable, TeacherAvailabilitySource.Manual,
            "Earlier absence", false, true, teacherId: teacher.Id), autoSave: true);

        var suggestions = await _substitutionAppService.GetSuggestedTeachersAsync(new SuggestedTeacherInput
        {
            Date = new DateTimeOffset(date),
            StartTime = "16:30",
            EndTime = "17:30"
        });

        suggestions.ShouldContain(x => x.TeacherId == teacher.Id);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_Not_Create_Availability_From_Attendance()
    {
        var employee = await CreateEmployeeAsync(Guid.NewGuid(), "Dashboard", "Teacher");
        var attendanceDate = new DateOnly(2026, 9, 18);
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

        await _substitutionAppService.GetDashboardAsync(
            new DateTimeOffset(attendanceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        (await _availabilityRepository.GetCountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(true, "08:00", "08:45", false)]
    [InlineData(true, "14:30", "15:00", false)]
    [InlineData(true, "15:00", "15:45", true)]
    [InlineData(false, "08:00", "08:45", false)]
    [InlineData(false, "11:30", "12:00", true)]
    public async Task GetSuggestedTeachersAsync_Should_Match_Legacy_Attendance_Against_Configured_Shift(
        bool isFullDay, string startTime, string endTime, bool shouldBeSuggested)
    {
        var employee = await CreateEmployeeAsync(Guid.NewGuid(), "Shift", "Teacher");
        var shift = await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(
            new ShiftManagement { ShiftName = "Teaching shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(15, 0) },
            autoSave: true);
        employee.ShiftId = shift.Id;
        await _employeeRepository.UpdateAsync(employee, autoSave: true);
        var date = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        await _availabilityRepository.InsertAsync(new TeacherAvailability(
            Guid.NewGuid(), null, employee.EmployeeName, date,
            new TimeOnly(9, 0), isFullDay ? new TimeOnly(14, 15) : new TimeOnly(12, 0),
            TeacherAvailabilityStatus.Unavailable, TeacherAvailabilitySource.Attendance,
            "HR attendance", isFullDay, true, teacherId: employee.Id), autoSave: true);

        var suggestions = await _substitutionAppService.GetSuggestedTeachersAsync(new SuggestedTeacherInput
        {
            Date = new DateTimeOffset(date), StartTime = startTime, EndTime = endTime
        });

        if (shouldBeSuggested)
        {
            suggestions.ShouldContain(x => x.TeacherId == employee.Id);
        }
        else
        {
            suggestions.ShouldNotContain(x => x.TeacherId == employee.Id);
        }
    }

    [Fact]
    public async Task GetMyDutiesAsync_Should_Return_Assigned_Duty_Outside_The_Substitutes_Class_Scope()
    {
        var substituteUserId = Guid.NewGuid();
        var substitute = await CreateEmployeeAsync(substituteUserId, "Akshay", "Patel");
        var originalTeacherId = Guid.NewGuid();
        var substitution = new SubstitutionRequest(
            Guid.NewGuid(),
            tenantId: null,
            date: new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc),
            period: "Period 2",
            startTime: new TimeOnly(13, 0),
            endTime: new TimeOnly(13, 30),
            className: "LKG",
            divisionName: "A",
            subjectName: "Science",
            originalTeacherName: "Dharmik Jinjala",
            roomName: "B106",
            reason: "Marked absent in HR attendance.",
            priority: SubstitutionPriority.Normal,
            source: SubstitutionSource.HrLeave,
            acknowledgementRequired: true,
            standardId: Guid.NewGuid(),
            divisionId: Guid.NewGuid(),
            subjectId: Guid.NewGuid(),
            originalTeacherId: originalTeacherId);
        substitution.Assign(
            substitute.Id,
            substitute.EmployeeName,
            assignedRoomId: null,
            assignedRoomName: null,
            suggestionScore: null,
            workloadStatus: "Low",
            conflictStatus: SubstitutionConflictStatus.Clear,
            conflictReason: null,
            sendNotification: true);
        await _substitutionRepository.InsertAsync(substitution, autoSave: true);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(substituteUserId)))
        {
            var result = await _substitutionAppService.GetMyDutiesAsync(new GetSubstitutionsInput
            {
                MaxResultCount = 50
            });

            result.TotalCount.ShouldBe(1);
            var duty = result.Items.ShouldHaveSingleItem();
            duty.OriginalTeacherId.ShouldBe(originalTeacherId);
            duty.OriginalTeacherName.ShouldBe("Dharmik Jinjala");
            duty.SubstituteTeacherId.ShouldBe(substitute.Id);
            duty.SubstituteTeacherName.ShouldBe("Akshay Patel");
            duty.IsResponseWindowOpen.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Allow_Assigned_Substitute_Outside_Class_Scope()
    {
        var substituteUserId = Guid.NewGuid();
        var substitute = await CreateEmployeeAsync(substituteUserId, "Assigned", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(substitute);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(substituteUserId)))
        {
            var result = await _substitutionAppService.AcknowledgeAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "I accept this duty." });

            result.Status.ShouldBe("Acknowledged");
            result.AcknowledgementStatus.ShouldBe("Accepted");
            result.Remarks.ShouldBe("I accept this duty.");
        }
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Preserve_Original_Acceptance_When_Retried()
    {
        var substituteUserId = Guid.NewGuid();
        var substitute = await CreateEmployeeAsync(substituteUserId, "Assigned", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(substitute);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(substituteUserId)))
        {
            var firstResult = await _substitutionAppService.AcknowledgeAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "Original acceptance." });
            var firstAudit = firstResult.AuditLog.ShouldHaveSingleItem();
            var original = await _substitutionRepository.GetAsync(substitution.Id);
            var acknowledgedAt = original.AcknowledgedAt;
            var concurrencyStamp = original.ConcurrencyStamp;

            var result = await _substitutionAppService.AcknowledgeAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "Repeated acceptance." });

            result.Status.ShouldBe("Acknowledged");
            result.AcknowledgementStatus.ShouldBe("Accepted");
            result.Remarks.ShouldBe("Original acceptance.");
            var audit = result.AuditLog.ShouldHaveSingleItem();
            audit.Id.ShouldBe(firstAudit.Id);
            audit.Action.ShouldBe(firstAudit.Action);
            audit.Details.ShouldBe(firstAudit.Details);

            var persisted = await _substitutionRepository.GetAsync(substitution.Id);
            persisted.AcknowledgedAt.ShouldBe(acknowledgedAt);
            persisted.ConcurrencyStamp.ShouldBe(concurrencyStamp);
            persisted.Remarks.ShouldBe("Original acceptance.");
        }
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Accept_Again_After_Reassignment()
    {
        var substituteUserId = Guid.NewGuid();
        var substitute = await CreateEmployeeAsync(substituteUserId, "Assigned", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(substitute);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(substituteUserId)))
        {
            await _substitutionAppService.AcknowledgeAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "Original acceptance." });

            var reassigned = await _substitutionRepository.GetAsync(substitution.Id);
            reassigned.Assign(
                substitute.Id,
                substitute.EmployeeName,
                assignedRoomId: null,
                assignedRoomName: null,
                suggestionScore: null,
                workloadStatus: "Low",
                conflictStatus: SubstitutionConflictStatus.Clear,
                conflictReason: null,
                sendNotification: true);
            await _substitutionRepository.UpdateAsync(reassigned, autoSave: true);

            var result = await _substitutionAppService.AcknowledgeAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "Reassigned duty accepted." });

            result.Status.ShouldBe("Acknowledged");
            result.AcknowledgementStatus.ShouldBe("Accepted");
            result.Remarks.ShouldBe("Reassigned duty accepted.");
            result.AuditLog.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task RejectDutyAsync_Should_Return_The_Rejected_Duty_After_Clearing_The_Assignment()
    {
        var substituteUserId = Guid.NewGuid();
        var substitute = await CreateEmployeeAsync(substituteUserId, "Assigned", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(substitute);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(substituteUserId)))
        {
            var result = await _substitutionAppService.RejectDutyAsync(
                substitution.Id,
                new SubstitutionActionDto { Reason = "Unable to cover this period." });

            result.Status.ShouldBe("Rejected");
            result.AcknowledgementStatus.ShouldBe("Rejected");
            result.SubstituteTeacherId.ShouldBeNull();
            result.SubstituteTeacherName.ShouldBeNull();
            result.Remarks.ShouldBe("Unable to cover this period.");

            var persisted = await _substitutionRepository.GetAsync(substitution.Id);
            persisted.Status.ShouldBe(SubstitutionStatus.Rejected);
            persisted.AcknowledgementStatus.ShouldBe(SubstitutionAcknowledgementStatus.Rejected);
            persisted.SubstituteTeacherId.ShouldBeNull();
            persisted.SubstituteTeacherName.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(SubstitutionStatus.Rejected, true)]
    [InlineData(SubstitutionStatus.Cancelled, true)]
    [InlineData(SubstitutionStatus.Completed, true)]
    [InlineData(SubstitutionStatus.WaitingAck, false)]
    public async Task CreateAsync_Should_Use_Previous_Status_For_Slot_Conflict(
        SubstitutionStatus previousStatus, bool canReassign)
    {
        var previousTeacher = await CreateEmployeeAsync(Guid.NewGuid(), "Previous", "Substitute");
        var replacementTeacher = await CreateEmployeeAsync(Guid.NewGuid(), "Replacement", "Substitute");
        var actorId = Guid.NewGuid();
        await CreateEmployeeAsync(actorId, "Administrative", "User");
        await _scopeRepository.InsertAsync(
            new UserScopeAssignment(Guid.NewGuid(), null, actorId), autoSave: true);
        var originalTeacherId = Guid.NewGuid();
        var slotSubjectId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date.AddDays(7);
        var previous = new SubstitutionRequest(
            Guid.NewGuid(), null, date, "Period 2", new TimeOnly(13, 0), new TimeOnly(13, 30),
            "Class 10", "A", "Mathematics", "Original Teacher", "BI01",
            "Original teacher unavailable.", SubstitutionPriority.Normal, SubstitutionSource.Manual,
            true, timetableSlotSubjectId: slotSubjectId, originalTeacherId: originalTeacherId);
        previous.Assign(previousTeacher.Id, previousTeacher.EmployeeName, null, null, null,
            "Low", SubstitutionConflictStatus.Clear, null, true);
        switch (previousStatus)
        {
            case SubstitutionStatus.Rejected:
                previous.Reject("Cannot cover the period.");
                break;
            case SubstitutionStatus.Cancelled:
                previous.Cancel("Assignment cancelled.");
                break;
            case SubstitutionStatus.Completed:
                previous.Complete();
                break;
        }
        await _substitutionRepository.InsertAsync(previous, autoSave: true);

        using (_currentPrincipalAccessor.Change(BuildPrincipal(actorId, "admin")))
        {
            var input = new CreateSubstitutionDto
            {
                Date = new DateTimeOffset(date),
                Period = "Period 2",
                StartTime = "13:00",
                EndTime = "13:30",
                ClassName = "Class 10",
                DivisionName = "A",
                TimetableSlotSubjectId = slotSubjectId,
                SubjectName = "Mathematics",
                OriginalTeacherId = originalTeacherId,
                OriginalTeacherName = "Original Teacher",
                RoomName = "BI01",
                Reason = "Original teacher unavailable.",
                SubstituteTeacherId = replacementTeacher.Id,
                SubstituteTeacherName = replacementTeacher.EmployeeName
            };

            if (!canReassign)
            {
                var error = await Should.ThrowAsync<UserFriendlyException>(() =>
                    _substitutionAppService.CreateAsync(input));
                error.Message.ShouldBe("This slot time already has an active substitution assignment.");
                return;
            }

            var created = await _substitutionAppService.CreateAsync(input);

            created.Id.ShouldNotBe(previous.Id);
            created.SubstituteTeacherId.ShouldBe(replacementTeacher.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcknowledgeAsync_Should_Reject_User_Who_Is_Not_The_Assigned_Substitute(bool alreadyAccepted)
    {
        var assignedUserId = Guid.NewGuid();
        var assignedSubstitute = await CreateEmployeeAsync(assignedUserId, "Assigned", "Teacher");
        var otherUserId = Guid.NewGuid();
        await CreateEmployeeAsync(otherUserId, "Other", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(assignedSubstitute);

        if (alreadyAccepted)
        {
            using (_currentPrincipalAccessor.Change(BuildPrincipal(assignedUserId)))
            {
                await _substitutionAppService.AcknowledgeAsync(substitution.Id);
            }
        }

        using (_currentPrincipalAccessor.Change(BuildPrincipal(otherUserId)))
        {
            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _substitutionAppService.AcknowledgeAsync(substitution.Id));

            exception.Message.ShouldBe("Only the assigned substitute teacher or administrators can perform this action.");
        }
    }

    private async Task<SubstitutionRequest> CreateAssignedSubstitutionAsync(Employee substitute)
    {
        var substitution = new SubstitutionRequest(
            Guid.NewGuid(),
            tenantId: null,
            date: DateTime.UtcNow.Date.AddDays(1),
            period: "Period 2",
            startTime: new TimeOnly(13, 0),
            endTime: new TimeOnly(13, 30),
            className: "LKG",
            divisionName: "A",
            subjectName: "Science",
            originalTeacherName: "Original Teacher",
            roomName: "B106",
            reason: "Original teacher unavailable.",
            priority: SubstitutionPriority.Normal,
            source: SubstitutionSource.HrLeave,
            acknowledgementRequired: true,
            standardId: Guid.NewGuid(),
            divisionId: Guid.NewGuid(),
            subjectId: Guid.NewGuid(),
            originalTeacherId: Guid.NewGuid());
        substitution.Assign(
            substitute.Id,
            substitute.EmployeeName,
            assignedRoomId: null,
            assignedRoomName: null,
            suggestionScore: null,
            workloadStatus: "Low",
            conflictStatus: SubstitutionConflictStatus.Clear,
            conflictReason: null,
            sendNotification: true);

        return await _substitutionRepository.InsertAsync(substitution, autoSave: true);
    }

    private async Task<Employee> CreateEmployeeAsync(Guid userId, string firstName, string lastName)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        await _identityUserRepository.InsertAsync(
            new IdentityUser(userId, $"substitute-{suffix[..8]}", $"{suffix[..12]}@test.local", tenantId: null),
            autoSave: true);
        await _roleRepository.InsertAsync(
            new IdentityRole(roleId, $"substitute-{suffix[..8]}", tenantId: null),
            autoSave: true);

        var employee = new Employee(
            Guid.NewGuid(),
            tenantId: null,
            userId: userId,
            employeeCode: $"SUB{suffix[..10]}",
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

        return await _employeeRepository.InsertAsync(employee, autoSave: true);
    }

    private static ClaimsPrincipal BuildPrincipal(Guid userId, string role = "teacher")
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"user-{userId:N}"),
            new(AbpClaimTypes.Role, role)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
