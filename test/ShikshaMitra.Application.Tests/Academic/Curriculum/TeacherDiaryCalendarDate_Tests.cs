using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherDiaryCalendarDate_Tests : LessonPlanProgressTestBase
{
    private static readonly DateTimeOffset SelectedDate = new(2026, 9, 15, 0, 0, 0, TimeSpan.FromHours(5.5));

    [Theory]
    [InlineData(false, "2026-09-15T00:00:00+05:30")]
    [InlineData(false, "2026-09-14T18:30:00Z")]
    [InlineData(true, "2026-09-15T00:00:00+05:30")]
    [InlineData(true, "2026-09-14T18:30:00Z")]
    public async Task Save_And_Submit_Should_Use_Institution_Date_And_Persist_Utc(bool update, string actualDate)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                await AddHolidayAsync(new DateTime(2026, 9, 14), "Ganesh Chaturthi");
                var plan = await CreateApprovedPlanAsync();

                using (AsLessonPlanOwner())
                {
                    var saved = await SaveDiaryAsync(plan.Id, actualDate, update);
                    saved.ActualDate.ShouldBe(SelectedDate.UtcDateTime);
                    saved.ActualDate.Kind.ShouldBe(DateTimeKind.Utc);
                    saved.RescheduledToDate.ShouldBe(SelectedDate.AddDays(1).UtcDateTime);

                    var submitted = await Service.SubmitTeacherDiaryAsync(saved.Id);
                    submitted.Status.ShouldBe(TeacherDiaryStatus.Submitted);
                    submitted.ActualDate.ShouldBe(SelectedDate.UtcDateTime);
                    submitted.RescheduledToDate.ShouldBe(SelectedDate.AddDays(1).UtcDateTime);

                    var persisted = await GetRequiredService<IRepository<TeacherDiary, Guid>>().GetAsync(saved.Id);
                    persisted.ActualDate.ShouldBe(SelectedDate.UtcDateTime);
                    persisted.RescheduledToDate.ShouldBe(SelectedDate.AddDays(1).UtcDateTime);
                }
            }
        });
    }

    [Theory]
    [InlineData(false, "2026-09-15T00:00:00+05:30")]
    [InlineData(false, "2026-09-14T18:30:00Z")]
    [InlineData(true, "2026-09-15T00:00:00+05:30")]
    [InlineData(true, "2026-09-14T18:30:00Z")]
    public async Task Save_Should_Block_An_Active_Holiday_On_The_Selected_Institution_Date(bool update, string actualDate)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                await AddHolidayAsync(SelectedDate.Date, "Selected date holiday");
                var plan = await CreateApprovedPlanAsync();

                using (AsLessonPlanOwner())
                {
                    var error = await Should.ThrowAsync<UserFriendlyException>(
                        () => SaveDiaryAsync(plan.Id, actualDate, update));
                    error.Message.ShouldContain("Holiday: Selected date holiday");
                }
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_And_Submit_Should_Ignore_Deleted_Or_Cancelled_Holidays(bool cancelled)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                await AddHolidayAsync(SelectedDate.Date, "Removed holiday");
                var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
                var holiday = await days.SingleAsync(day => day.Title == "Removed holiday");
                if (cancelled)
                {
                    holiday.Cancel("Holiday cancelled.");
                    await days.UpdateAsync(holiday, autoSave: true);
                }
                else
                {
                    await days.DeleteAsync(holiday.Id, autoSave: true);
                }

                var plan = await CreateApprovedPlanAsync();
                using (AsLessonPlanOwner())
                {
                    var saved = await SaveDiaryAsync(plan.Id, "2026-09-14T18:30:00Z", update: false);
                    (await Service.SubmitTeacherDiaryAsync(saved.Id)).Status.ShouldBe(TeacherDiaryStatus.Submitted);
                }
            }
        });
    }

    private async Task<LessonPlan> CreateApprovedPlanAsync()
    {
        var input = Input();
        input.PlannedDate = SelectedDate;
        var plan = await InsertPlanAsync(input);
        plan.Submit();
        plan.Approve(TeacherUserId, "Approved for diary date regression.");
        return await Plans.UpdateAsync(plan, autoSave: true);
    }

    private async Task<TeacherDiaryDto> SaveDiaryAsync(Guid planId, string actualDate, bool update)
    {
        var input = DiaryInput(planId, actualDate);
        if (!update)
        {
            return await Service.CreateTeacherDiaryAsync(input);
        }

        var existing = await Service.CreateTeacherDiaryAsync(DiaryInput(planId, "2026-09-16T12:00:00Z"));
        return await Service.UpdateTeacherDiaryAsync(existing.Id, input);
    }

    private static CreateUpdateTeacherDiaryDto DiaryInput(Guid planId, string actualDate)
    {
        var json = JsonSerializer.Serialize(new
        {
            lessonPlanId = planId,
            actualDate,
            rescheduledToDate = "2026-09-16T00:00:00+05:30",
            classConducted = true,
            actualTopicCovered = "Calendar date regression",
            actualCompletionPercentage = 30
        });
        return JsonSerializer.Deserialize<CreateUpdateTeacherDiaryDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
