using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
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
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public SubstitutionAppService_Tests()
    {
        _substitutionAppService = GetRequiredService<ISubstitutionAppService>();
        _substitutionRepository = GetRequiredService<IRepository<SubstitutionRequest, Guid>>();
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
        _identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
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
    public async Task AcknowledgeAsync_Should_Reject_User_Who_Is_Not_The_Assigned_Substitute()
    {
        var assignedUserId = Guid.NewGuid();
        var assignedSubstitute = await CreateEmployeeAsync(assignedUserId, "Assigned", "Teacher");
        var otherUserId = Guid.NewGuid();
        await CreateEmployeeAsync(otherUserId, "Other", "Teacher");
        var substitution = await CreateAssignedSubstitutionAsync(assignedSubstitute);

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

    private static ClaimsPrincipal BuildPrincipal(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"user-{userId:N}"),
            new(AbpClaimTypes.Role, "teacher")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
