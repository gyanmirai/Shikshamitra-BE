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
    private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
    private readonly PayrollCalendarResolver _payrollCalendarResolver;
    private readonly ICurrentTenant _currentTenant;

    public DayCalculationSettingAppService_Tests()
    {
        _dayCalculationSettingAppService = GetRequiredService<IDayCalculationSettingAppService>();
        _academicYearManager = GetRequiredService<AcademicYearManager>();
        _academicYearRepository = GetRequiredService<IAcademicYearRepository>();
        _holidayRepository = GetRequiredService<IRepository<Holiday, Guid>>();
        _dayCalculationSettingRepository = GetRequiredService<IDayCalculationSettingRepository>();
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
    public async Task Save_Should_Persist_SaturdayPolicy_And_HolidayOverrides()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Calendar Config AY",
                new DateTime(2026, 5, 1),
                new DateTime(2027, 4, 30));

            var result = await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                SaturdayWorkPolicy = "half-day",
                HolidaySyncMode = "synced-with-overrides",
                HolidayOverrides =
                {
                    new PayrollHolidayOverrideDto
                    {
                        Date = new DateTime(2026, 6, 10),
                        HolidayName = "Payroll only",
                        IncludedInPayroll = true
                    }
                }
            });

            result.Success.ShouldBeTrue();

            var saved = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);
            saved.SaturdayWorkPolicy.ShouldBe("half-day");
            saved.HolidaySyncMode.ShouldBe("synced-with-overrides");
            saved.HolidayOverrides.Count.ShouldBe(1);
            var june = saved.Months.Single(x => x.Year == 2026 && x.Month == 6);
            june.Holidays.ShouldContain(x => x.HolidayName == "Payroll only" && x.Source == "payroll");
            june.PayrollHolidayUnits.ShouldBeGreaterThan(0);
        });
    }

    [Fact]
    public async Task Preview_Should_Show_Tenant_Calendar_Holiday_As_Synced()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Synced Holiday AY",
                new DateTime(2026, 5, 1),
                new DateTime(2027, 4, 30));
            var holidayTypeId = Guid.NewGuid();

            var holiday = await _holidayRepository.InsertAsync(
                Holiday.Create(
                    null,
                    academicYear.Id,
                    "Synced Holiday",
                    holidayTypeId,
                    new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero)),
                autoSave: true);

            var preview = await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                HolidayOverrides =
                {
                    new PayrollHolidayOverrideDto
                    {
                        Date = new DateTime(2026, 6, 11),
                        HolidayName = "Synced Holiday",
                        IncludedInPayroll = false,
                        CalendarHolidayId = holiday.Id
                    }
                }
            });

            var june = preview.Months.Single(x => x.Year == 2026 && x.Month == 6);
            june.Holidays.ShouldContain(x =>
                x.HolidayName == "Synced Holiday" &&
                x.Source == "calendar" &&
                !x.IncludedInPayroll);
        });
    }

    [Fact]
    public async Task Save_Should_Reject_Past_HolidayOverride_Changes()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Past Override Lock AY",
                new DateTime(2026, 4, 1),
                new DateTime(2027, 3, 31));

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "working-days",
                    HolidayOverrides =
                    {
                        new PayrollHolidayOverrideDto
                        {
                            Date = new DateTime(2026, 4, 10),
                            HolidayName = "Past override",
                            IncludedInPayroll = true
                        }
                    }
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:PastPayrollMonthLocked");
        });
    }

    [Fact]
    public async Task Save_Should_Preserve_Past_SaturdayPolicy_When_Changed_For_Future_Months()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Locked Saturday AY",
                new DateTime(2026, 4, 1),
                new DateTime(2026, 6, 30));

            await _dayCalculationSettingRepository.InsertAsync(
                new DayCalculationSetting(
                    Guid.NewGuid(),
                    null,
                    DayCalculationType.WorkingDays,
                    null,
                    academicYear.Id,
                    null,
                    SaturdayWorkPolicy.HalfDay),
                autoSave: true);

            var before = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);
            var aprilBefore = before.Months.Single(x => x.Year == 2026 && x.Month == 4);

            await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                SaturdayWorkPolicy = "off"
            });

            var after = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);
            var aprilAfter = after.Months.Single(x => x.Year == 2026 && x.Month == 4);
            var juneAfter = after.Months.Single(x => x.Year == 2026 && x.Month == 6);

            aprilAfter.Locked.ShouldBeTrue();
            aprilAfter.BasisDays.ShouldBe(aprilBefore.BasisDays);
            aprilAfter.SaturdayUnits.ShouldBe(aprilBefore.SaturdayUnits);
            juneAfter.SaturdayUnits.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task Save_Should_Preserve_Past_Manual_Basis_When_Mode_Changes()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Locked Manual AY",
                new DateTime(2026, 4, 1),
                new DateTime(2026, 6, 30));

            await _dayCalculationSettingRepository.InsertAsync(
                new DayCalculationSetting(
                    Guid.NewGuid(),
                    null,
                    DayCalculationType.ManualDays,
                    null,
                    academicYear.Id,
                    new[]
                    {
                        new DayCalculationManualMonth(2026, 4, 22),
                        new DayCalculationManualMonth(2026, 5, 24),
                        new DayCalculationManualMonth(2026, 6, 25)
                    }),
                autoSave: true);

            await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                SaturdayWorkPolicy = "off"
            });

            var saved = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);
            var april = saved.Months.Single(x => x.Year == 2026 && x.Month == 4);
            var june = saved.Months.Single(x => x.Year == 2026 && x.Month == 6);

            april.Locked.ShouldBeTrue();
            april.DayCalculationType.ShouldBe("manual-days");
            april.ManualDays.ShouldBe(22);
            april.BasisDays.ShouldBe(22m);
            june.DayCalculationType.ShouldBe("working-days");
        });
    }

    [Fact]
    public async Task Save_Should_Preserve_Existing_Past_Overrides_When_Client_Sends_Empty_List()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Preserve Past Override AY",
                new DateTime(2026, 4, 1),
                new DateTime(2026, 6, 30));

            await _dayCalculationSettingRepository.InsertAsync(
                new DayCalculationSetting(
                    Guid.NewGuid(),
                    null,
                    DayCalculationType.WorkingDays,
                    null,
                    academicYear.Id,
                    null,
                    SaturdayWorkPolicy.FullDay,
                    HolidaySyncMode.SyncedWithOverrides,
                    new[] { new PayrollHolidayOverride(new DateTime(2026, 4, 10), "Past override", true) }),
                autoSave: true);

            await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                HolidayOverrides = { }
            });

            var saved = await _dayCalculationSettingAppService.GetAsync(academicYear.Id);

            saved.HolidayOverrides.ShouldContain(x =>
                x.Date.Date == new DateTime(2026, 4, 10) &&
                x.HolidayName == "Past override" &&
                x.IncludedInPayroll);
        });
    }

    [Fact]
    public async Task Save_Should_Reject_Changed_Existing_Past_Override()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Reject Changed Past Override AY",
                new DateTime(2026, 4, 1),
                new DateTime(2026, 6, 30));

            await _dayCalculationSettingRepository.InsertAsync(
                new DayCalculationSetting(
                    Guid.NewGuid(),
                    null,
                    DayCalculationType.WorkingDays,
                    null,
                    academicYear.Id,
                    null,
                    SaturdayWorkPolicy.FullDay,
                    HolidaySyncMode.SyncedWithOverrides,
                    new[] { new PayrollHolidayOverride(new DateTime(2026, 4, 10), "Past override", true) }),
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "working-days",
                    HolidayOverrides =
                    {
                        new PayrollHolidayOverrideDto
                        {
                            Date = new DateTime(2026, 4, 10),
                            HolidayName = "Changed past override",
                            IncludedInPayroll = true
                        }
                    }
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:PastPayrollMonthLocked");
        });
    }

    [Fact]
    public async Task Preview_Should_Not_Count_Holidays_That_Do_Not_Reduce_Working_Units()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Non Working Holiday Count AY",
                new DateTime(2026, 5, 1),
                new DateTime(2026, 6, 30));

            var preview = await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
            {
                AcademicYearId = academicYear.Id,
                DayCalculationType = "working-days",
                SaturdayWorkPolicy = "off",
                HolidayOverrides =
                {
                    new PayrollHolidayOverrideDto
                    {
                        Date = new DateTime(2026, 6, 6),
                        HolidayName = "Off Saturday",
                        IncludedInPayroll = true
                    },
                    new PayrollHolidayOverrideDto
                    {
                        Date = new DateTime(2026, 6, 7),
                        HolidayName = "Sunday Holiday",
                        IncludedInPayroll = true
                    }
                }
            });

            var june = preview.Months.Single(x => x.Year == 2026 && x.Month == 6);

            june.Holidays.ShouldContain(x => x.HolidayName == "Off Saturday");
            june.Holidays.ShouldContain(x => x.HolidayName == "Sunday Holiday");
            june.PayrollHolidayDays.ShouldBe(0);
            june.PayrollHolidayUnits.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task Preview_Should_Reject_Invalid_CalendarHolidayId()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Invalid Holiday Id AY",
                new DateTime(2026, 5, 1),
                new DateTime(2026, 6, 30));

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "working-days",
                    HolidayOverrides =
                    {
                        new PayrollHolidayOverrideDto
                        {
                            Date = new DateTime(2026, 6, 10),
                            HolidayName = "Spoofed",
                            IncludedInPayroll = false,
                            CalendarHolidayId = Guid.NewGuid()
                        }
                    }
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:InvalidPayrollHolidayOverride");
        });
    }

    [Fact]
    public async Task Preview_Should_Reject_CalendarHolidayId_On_Wrong_Date()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var academicYear = await CreateAcademicYearAsync(
                "Payroll Wrong Holiday Date AY",
                new DateTime(2026, 5, 1),
                new DateTime(2026, 6, 30));
            var holidayTypeId = Guid.NewGuid();

            var holiday = await _holidayRepository.InsertAsync(
                Holiday.Create(
                    null,
                    academicYear.Id,
                    "Calendar Holiday",
                    holidayTypeId,
                    new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero)),
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = academicYear.Id,
                    DayCalculationType = "working-days",
                    HolidayOverrides =
                    {
                        new PayrollHolidayOverrideDto
                        {
                            Date = new DateTime(2026, 6, 12),
                            HolidayName = "Wrong date",
                            IncludedInPayroll = false,
                            CalendarHolidayId = holiday.Id
                        }
                    }
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:InvalidPayrollHolidayOverride");
        });
    }

    [Fact]
    public async Task Get_Should_Reject_Missing_AcademicYear_With_Controlled_Error()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.GetAsync(Guid.NewGuid());
            });

            ex.Code.ShouldBe("ShikshaMitra:AcademicYearNotFound");
        });
    }

    [Fact]
    public async Task Preview_Should_Reject_Missing_AcademicYear_With_Controlled_Error()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.PreviewAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = Guid.NewGuid(),
                    DayCalculationType = "working-days"
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:AcademicYearNotFound");
        });
    }

    [Fact]
    public async Task Save_Should_Reject_Missing_AcademicYear_With_Controlled_Error()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _dayCalculationSettingAppService.SaveAsync(new DayCalculationRequestDto
                {
                    AcademicYearId = Guid.NewGuid(),
                    DayCalculationType = "working-days"
                });
            });

            ex.Code.ShouldBe("ShikshaMitra:AcademicYearNotFound");
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
