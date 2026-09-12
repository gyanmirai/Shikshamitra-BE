using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanProgress_Tests : LessonPlanProgressTestBase
{
    [Fact]
    public async Task Create_Should_Validate_The_Selected_Local_Date_Not_The_Previous_Utc_Date()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await AddHolidayAsync(new DateTime(2026, 9, 14), "Ganesh Chaturthi");

                var input = Input(30);
                input.PlannedDate = new DateTimeOffset(
                    2026,
                    9,
                    15,
                    0,
                    0,
                    0,
                    TimeSpan.FromHours(5.5));

                var created = await Service.CreateAsync(input);

                created.Topic.ShouldBe(input.Topic);
                created.PlannedDate.ShouldBe(input.PlannedDate.UtcDateTime);
            }
        });
    }

    [Fact]
    public async Task Create_Should_Still_Block_The_Exact_Local_Holiday_Date()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await AddHolidayAsync(new DateTime(2026, 9, 15), "School Holiday");

                var input = Input(30);
                input.PlannedDate = new DateTimeOffset(
                    2026,
                    9,
                    15,
                    0,
                    0,
                    0,
                    TimeSpan.FromHours(5.5));

                var error = await Should.ThrowAsync<UserFriendlyException>(
                    () => Service.CreateAsync(input));

                error.Message.ShouldContain("Holiday: School Holiday");
                (await Plans.AnyAsync(plan => plan.Topic == input.Topic)).ShouldBeFalse();
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(33)]
    [InlineData(100)]
    public async Task Progress_Should_Use_Highest_Submitted_Actual_Across_Topics_And_Teachers(int maximum)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                (await Service.GetChapterProgressAsync(ProgressInput())).MinimumCompletionPercentage.ShouldBe(0);
                var earlier = Input();
                earlier.TeacherId = Guid.NewGuid();
                await AddDiaryAsync(await InsertPlanAsync(earlier), maximum);
                await AddDiaryAsync(await InsertPlanAsync(), Math.Max(0, maximum - 10));
                await AddDiaryAsync(await InsertPlanAsync(), 100, submitted: false);
                await AddDiaryAsync(await InsertPlanAsync(), 100, conducted: false);
                (await Service.GetChapterProgressAsync(ProgressInput())).MinimumCompletionPercentage.ShouldBe(maximum);
            }
        });
    }

    [Theory]
    [InlineData("year")]
    [InlineData("class")]
    [InlineData("division")]
    [InlineData("subject")]
    [InlineData("chapter")]
    [InlineData("tenant")]
    public async Task Progress_Should_Not_Leak_Across_Academic_Contexts(string differentContext)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await AddDiaryAsync(await InsertPlanAsync(), 30);
                var input = Input();
                if (differentContext == "year") input.AcademicYearId = Guid.NewGuid();
                if (differentContext == "class") input.StandardId = Guid.NewGuid();
                if (differentContext == "division") input.DivisionId = Guid.NewGuid();
                if (differentContext == "subject") input.SubjectId = Guid.NewGuid();
                if (differentContext == "chapter") input.ChapterId = Guid.NewGuid();
                using (Tenant.Change(differentContext == "tenant" ? Guid.NewGuid() : TenantId))
                    await AddDiaryAsync(await InsertPlanAsync(input), 100);
                (await Service.GetChapterProgressAsync(ProgressInput())).MinimumCompletionPercentage.ShouldBe(30);
            }
        });
    }

    [Theory]
    [InlineData(LessonPlanStatus.Draft)]
    [InlineData(LessonPlanStatus.Submitted)]
    public async Task Create_Should_Reject_Regression_And_Accept_Equal_Or_Higher_Target(LessonPlanStatus status)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await AddDiaryAsync(await InsertPlanAsync(), 30);
                var input = Input(20);
                input.Status = status;
                var exception = await Should.ThrowAsync<UserFriendlyException>(() => Service.CreateAsync(input));
                exception.Message.ShouldContain("30%");
                (await Plans.AnyAsync(plan => plan.Topic == input.Topic)).ShouldBeFalse();
                input.PlannedCompletionPercentage = 30;
                (await Service.CreateAsync(input)).PlannedCompletionPercentage.ShouldBe(30);
                input.Topic = "Higher target";
                input.PlannedCompletionPercentage = 40;
                (await Service.CreateAsync(input)).PlannedCompletionPercentage.ShouldBe(40);
            }
        });
    }

    [Fact]
    public async Task Update_And_Submit_Should_Recheck_Progress_That_Advanced_After_Draft_Creation()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                var draft = await InsertPlanAsync(Input(20));
                await AddDiaryAsync(await InsertPlanAsync(), 30);
                var update = new UpdateLessonPlanDto
                { ChapterId = ChapterId, Topic = draft.Topic, PlannedCompletionPercentage = 20 };
                using (AsLessonPlanOwner())
                {
                    await Should.ThrowAsync<UserFriendlyException>(() => Service.UpdateAsync(draft.Id, update));
                    await Should.ThrowAsync<UserFriendlyException>(() => Service.SubmitAsync(draft.Id));
                    await Should.ThrowAsync<UserFriendlyException>(() => Service.BulkSubmitAsync(
                        new BulkSubmitLessonPlansDto { LessonPlanIds = [draft.Id] }));
                    (await Plans.GetAsync(draft.Id)).Status.ShouldBe(LessonPlanStatus.Draft);
                    update.PlannedCompletionPercentage = 30;
                    (await Service.UpdateAsync(draft.Id, update)).PlannedCompletionPercentage.ShouldBe(30);
                    (await Service.SubmitAsync(draft.Id)).Status.ShouldBe(LessonPlanStatus.Submitted);
                }
            }
        });
    }

    [Fact]
    public async Task NeedsChanges_Plan_Should_Require_A_Meaningful_Edit_Before_Resubmission()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                var plan = await InsertPlanAsync(Input(30));
                using (AsLessonPlanOwner())
                {
                    await Service.SubmitAsync(plan.Id);
                }
                await Service.RequestChangesAsync(plan.Id, new ReviewRemarksDto
                {
                    Comment = "Add a practical example."
                });

                using (AsLessonPlanOwner())
                {
                    var submitError = await Should.ThrowAsync<LessonPlanRevisionRequiredException>(
                        () => Service.SubmitAsync(plan.Id));
                    submitError.Code.ShouldBe(CurriculumErrorCodes.LessonPlanRevisionRequired);

                    var unchanged = new UpdateLessonPlanDto
                    {
                        ChapterId = ChapterId,
                        Topic = $" {plan.Topic} ",
                        PlannedCompletionPercentage = 30
                    };
                    await Should.ThrowAsync<LessonPlanRevisionRequiredException>(
                        () => Service.UpdateAsync(plan.Id, unchanged));
                    (await Plans.GetAsync(plan.Id)).Status.ShouldBe(LessonPlanStatus.NeedsChanges);

                    unchanged.Topic = $"{plan.Topic} with a practical example";
                    (await Service.UpdateAsync(plan.Id, unchanged)).Status.ShouldBe(LessonPlanStatus.Draft);
                    (await Service.SubmitAsync(plan.Id)).Status.ShouldBe(LessonPlanStatus.Submitted);
                }
            }
        });
    }
}
