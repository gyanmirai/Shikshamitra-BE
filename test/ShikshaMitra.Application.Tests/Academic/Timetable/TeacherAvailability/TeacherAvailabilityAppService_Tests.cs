using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Academic.Timetable.Substitutions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class TeacherAvailabilityAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private const string AvailabilityOverlapMessage =
        "Teacher is already marked unavailable for the selected date and time. Please choose a different time slot.";

    private readonly ITeacherAvailabilityAppService _teacherAvailabilityAppService;
    private readonly IRepository<TeacherAvailability, Guid> _availabilityRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    public TeacherAvailabilityAppService_Tests()
    {
        _teacherAvailabilityAppService = GetRequiredService<ITeacherAvailabilityAppService>();
        _availabilityRepository = GetRequiredService<IRepository<TeacherAvailability, Guid>>();
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
                AutoCreateSubstitutions = false
            });

            var persisted = await _availabilityRepository.GetAsync(created.Id);

            persisted.AcademicYearId.ShouldBe(academicYearId);
            persisted.AvailabilityDate.ShouldBe(availabilityDate);
        }
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
