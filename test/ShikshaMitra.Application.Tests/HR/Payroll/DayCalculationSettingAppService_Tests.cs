using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Day Calculation Setting App Service — Tier 4 Advanced Scenarios
/// Tests upsert workflow, day calculation types, ManualDays validation, and idempotent updates.
/// </summary>
public class DayCalculationSettingAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IDayCalculationSettingAppService _dayCalculationSettingAppService;
    private readonly AcademicYearManager _academicYearManager;
    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IRepository<Holiday, Guid> _holidayRepository;
    private readonly PayrollCalendarResolver _payrollCalendarResolver;
    private readonly ICurrentTenant _currentTenant;

    public DayCalculationSettingAppService_Tests()
    {
        _dayCalculationSettingAppService = GetRequiredService<IDayCalculationSettingAppService>();
        _academicYearManager = GetRequiredService<AcademicYearManager>();
        _academicYearRepository = GetRequiredService<IAcademicYearRepository>();
        _holidayRepository = GetRequiredService<IRepository<Holiday, Guid>>();
        _payrollCalendarResolver = GetRequiredService<PayrollCalendarResolver>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    #region Save — Working Days

    [Fact]
    public async Task Should_Save_WorkingDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Save_TotalMonthDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "total-month-days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    #endregion

    #region Save — Manual Days

    [Fact]
    public async Task Should_Save_ManualDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 22
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_ManualDays_At_Minimum_Boundary()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 1
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_ManualDays_At_Maximum_Boundary()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 31
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Zero()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 0
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Over_31()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 32
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Null_When_Type_Is_ManualDays()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = null
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    #endregion

    #region Save — Invalid Type

    [Fact]
    public async Task Should_Reject_Invalid_DayCalculationType()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "invalid-type"
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Empty_DayCalculationType()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = ""
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    #endregion

    #region Upsert Workflow

    [Fact]
    public async Task Should_Handle_Upsert_Create_Then_Update()
    {
        // Step 1: First save creates the setting
        var createInput = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days"
        };
        var createResult = await _dayCalculationSettingAppService.SaveAsync(createInput);
        createResult.Success.ShouldBeTrue();

        // Step 2: Second save updates the existing setting
        var updateInput = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 25
        };
        var updateResult = await _dayCalculationSettingAppService.SaveAsync(updateInput);
        updateResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Handle_Multiple_Upsert_Cycles()
    {
        // Cycle through all three types — each Save should succeed
        var types = new[]
        {
            ("working-days", (int?)null),
            ("total-month-days", (int?)null),
            ("manual-days", (int?)15),
            ("working-days", (int?)null),
            ("manual-days", (int?)28)
        };

        foreach (var (calcType, manualDays) in types)
        {
            var input = new DayCalculationRequestDto
            {
                DayCalculationType = calcType,
                ManualDays = manualDays
            };

            var result = await _dayCalculationSettingAppService.SaveAsync(input);
            result.Success.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_Accept_Case_Insensitive_DayCalculationType()
    {
        // Arrange — FluentValidation lowercases before comparison
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "Working-Days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_DayCalculationType_With_Whitespace()
    {
        // Arrange — FluentValidation trims before comparison
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "  total-month-days  "
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Ignore_ManualDays_When_Type_Is_Not_ManualDays()
    {
        // Arrange — ManualDays should be ignored when type is working-days
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days",
            ManualDays = 22 // Should be ignored
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert — should succeed; ManualDays is silently ignored by domain
        result.Success.ShouldBeTrue();
    }

    #endregion

    #region Payroll Calendar Resolver

    [Fact]
    public async Task CalendarResolver_Should_Resolve_MidMonth_AcademicYear_By_Month_Overlap()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Mid Month AY",
                new DateTime(2026, 4, 15),
                new DateTime(2027, 3, 31));

            var calendar = await _payrollCalendarResolver.ResolveAsync(2026, 4);

            calendar.AcademicYearId.ShouldBe(academicYear.Id);
        });
    }

    [Fact]
    public async Task CalendarResolver_Should_Filter_Holidays_By_Resolved_AcademicYear()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var targetYear = await CreateAcademicYearAsync(
                "Payroll Holiday Target AY",
                new DateTime(2026, 4, 15),
                new DateTime(2027, 3, 31));
            var otherYear = await CreateAcademicYearAsync(
                "Payroll Holiday Other AY",
                new DateTime(2025, 4, 1),
                new DateTime(2026, 4, 14));
            var holidayTypeId = Guid.NewGuid();

            await _holidayRepository.InsertAsync(
                Holiday.Create(
                    null,
                    targetYear.Id,
                    "Target Holiday",
                    holidayTypeId,
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)),
                autoSave: true);
            await _holidayRepository.InsertAsync(
                Holiday.Create(
                    null,
                    otherYear.Id,
                    "Other Holiday",
                    holidayTypeId,
                    new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero)),
                autoSave: true);

            var calendar = await _payrollCalendarResolver.ResolveAsync(2026, 4);

            calendar.AcademicYearId.ShouldBe(targetYear.Id);
            calendar.Holidays.Select(x => x.HolidayName).ShouldContain("Target Holiday");
            calendar.Holidays.Select(x => x.HolidayName).ShouldNotContain("Other Holiday");
        });
    }

    [Fact]
    public async Task CalendarResolver_Should_Filter_AcademicYears_And_Holidays_By_CurrentTenant()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var otherTenantId = Guid.NewGuid();
            var holidayTypeId = Guid.NewGuid();

            using (_currentTenant.Change(otherTenantId))
            {
                var otherTenantYear = await CreateAcademicYearAsync(
                    "Other Tenant Payroll AY",
                    new DateTime(2026, 4, 20),
                    new DateTime(2027, 3, 31));

                await _holidayRepository.InsertAsync(
                    Holiday.Create(
                        otherTenantId,
                        otherTenantYear.Id,
                        "Other Tenant Holiday",
                        holidayTypeId,
                        new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero)),
                    autoSave: true);
            }

            using (_currentTenant.Change(null))
            {
                var hostYear = await CreateAcademicYearAsync(
                    "Host Payroll AY",
                    new DateTime(2026, 4, 15),
                    new DateTime(2027, 3, 31));

                await _holidayRepository.InsertAsync(
                    Holiday.Create(
                        null,
                        hostYear.Id,
                        "Host Holiday",
                        holidayTypeId,
                        new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero)),
                    autoSave: true);

                var calendar = await _payrollCalendarResolver.ResolveAsync(2026, 4);

                calendar.AcademicYearId.ShouldBe(hostYear.Id);
                calendar.Holidays.Select(x => x.HolidayName).ShouldContain("Host Holiday");
                calendar.Holidays.Select(x => x.HolidayName).ShouldNotContain("Other Tenant Holiday");
            }
        });
    }

    [Fact]
    public async Task ConfigPreview_Should_Use_Same_Calendar_Resolution_As_Payroll()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Preview Consistency AY",
                new DateTime(2026, 4, 15),
                new DateTime(2027, 3, 31));
            var holidayTypeId = Guid.NewGuid();

            await _holidayRepository.InsertAsync(
                Holiday.Create(
                    null,
                    academicYear.Id,
                    "Preview Holiday",
                    holidayTypeId,
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)),
                autoSave: true);

            await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days"
            });

            var preview = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);
            var calendar = await _payrollCalendarResolver.ResolveAsync(2026, 4);
            var april = preview.Months.Single(x => x.Year == 2026 && x.Month == 4);

            april.WorkingDays.ShouldBe(calendar.WorkingDays);
            april.BasisDays.ShouldBe(calendar.WorkingDays);
            april.HolidayNames.ShouldContain("Preview Holiday");
        });
    }

    [Fact]
    public async Task Preview_Should_Reject_Missing_Manual_Months_For_AcademicYear()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Manual Preview AY",
                new DateTime(2026, 4, 1),
                new DateTime(2027, 3, 31));

            await Should.ThrowAsync<AbpValidationException>(async () =>
            {
                await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "manual-days"
                });
            });
        });
    }

    [Fact]
    public async Task Save_Should_Reject_Partial_Manual_Months_For_AcademicYear()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Manual Save AY",
                new DateTime(2026, 4, 1),
                new DateTime(2027, 3, 31));

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "manual-days",
                    ManualMonthDays =
                    {
                        new DayCalculationManualMonthDto
                        {
                            Year = 2026,
                            Month = 4,
                            ManualDays = 26
                        }
                    }
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:ManualMonthDaysRequired");
        });
    }

    private async Task<AcademicYear> CreateAcademicYearAsync(string name, DateTime startDate, DateTime endDate)
    {
        var academicYear = await _academicYearManager.CreateAsync(name, startDate, endDate);
        return await _academicYearRepository.InsertAsync(academicYear, autoSave: true);
    }

    #endregion
}
