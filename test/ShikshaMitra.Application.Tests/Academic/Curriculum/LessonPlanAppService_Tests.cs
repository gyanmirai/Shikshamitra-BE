using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid ManagerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid ManagerEmployeeId = Guid.Parse("11111111-1111-1111-1111-222222222222");
	private static readonly Guid TeacherUserId = Guid.Parse("22222222-2222-2222-2222-111111111111");
	private static readonly Guid TeacherEmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid AdminUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

	private readonly ILessonPlanAppService _lessonPlanAppService;
	private readonly IRepository<LessonPlan, Guid> _lessonPlanRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IIdentityUserRepository _identityUserRepository;
	private readonly ICurrentTenant _currentTenant;

	public LessonPlanAppService_Tests()
	{
		_lessonPlanAppService = GetRequiredService<ILessonPlanAppService>();
		_lessonPlanRepository = GetRequiredService<IRepository<LessonPlan, Guid>>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_identityUserRepository = GetRequiredService<IIdentityUserRepository>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Reportee_LessonPlans_For_Admin()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var reporteePlan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Photosynthesis Basics");

				var result = await _lessonPlanAppService.GetListAsync(new GetLessonPlansInput
				{
					MaxResultCount = 100
				});

				result.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
				result.Items.ShouldContain(item => item.Id == reporteePlan.Id);
			}
		});
	}

	[Fact]
	public async Task ApproveAsync_Should_Approve_Submitted_LessonPlan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Gravity and Motion");

				var approved = await _lessonPlanAppService.ApproveAsync(plan.Id, new ReviewRemarksDto
				{
					Comment = "Well planned lesson."
				});

				approved.Status.ShouldBe(LessonPlanStatus.Approved);
				approved.ReviewRemarks.ShouldBe("Well planned lesson.");

				var persisted = await _lessonPlanRepository.GetAsync(plan.Id);
				persisted.Status.ShouldBe(LessonPlanStatus.Approved);
			}
		});
	}

	[Fact]
	public async Task RequestChangesAsync_Should_Set_NeedsChanges()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Cell Division");

				var result = await _lessonPlanAppService.RequestChangesAsync(plan.Id, new ReviewRemarksDto
				{
					Comment = "Please add teaching aids details."
				});

				result.Status.ShouldBe(LessonPlanStatus.NeedsChanges);
				result.ReviewRemarks.ShouldBe("Please add teaching aids details.");
			}
		});
	}

	[Fact]
	public async Task RequestChangesAsync_Should_Return_Admin_Identity_When_Reviewer_Has_No_Employee_Record()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var adminUser = new IdentityUser(
					AdminUserId,
					"academic-admin",
					"academic-admin@example.test",
					TestTenantId)
				{
					Name = "Academic",
					Surname = "Admin"
				};
				await _identityUserRepository.InsertAsync(adminUser, autoSave: true);

				var plan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Cell Division");
				var result = await _lessonPlanAppService.RequestChangesAsync(plan.Id, new ReviewRemarksDto
				{
					Comment = "Please add teaching aids details."
				});

				result.ReviewedBy.ShouldBe(AdminUserId);
				result.ReviewedByName.ShouldBe("Academic Admin");
				(await _employeeRepository.AnyAsync(employee => employee.UserId == AdminUserId)).ShouldBeFalse();
			}
		});
	}

	[Fact]
	public async Task Admin_Reviewer_Should_Not_Revise_Or_Submit_A_Teacher_Lesson_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Teacher-owned topic");
				plan.RequestChanges(AdminUserId, "Please revise the learning activity.");
				await _lessonPlanRepository.UpdateAsync(plan, autoSave: true);

				var update = new UpdateLessonPlanDto
				{
					ChapterId = plan.ChapterId,
					Topic = "Admin-authored replacement",
					PlannedCompletionPercentage = plan.PlannedCompletionPercentage
				};

				var updateError = await Should.ThrowAsync<AbpAuthorizationException>(
					() => _lessonPlanAppService.UpdateAsync(plan.Id, update));
				updateError.Message.ShouldContain("Only the assigned teacher");

				var submitError = await Should.ThrowAsync<AbpAuthorizationException>(
					() => _lessonPlanAppService.SubmitAsync(plan.Id));
				submitError.Message.ShouldContain("Only the assigned teacher");

				var persisted = await _lessonPlanRepository.GetAsync(plan.Id);
				persisted.Topic.ShouldBe("Teacher-owned topic");
				persisted.Status.ShouldBe(LessonPlanStatus.NeedsChanges);
			}
		});
	}

	[Fact]
	public async Task GetTimelineAsync_Should_Return_A_Plan_For_The_Requested_Offset_Date()
	{
		var requestedDate = new DateTimeOffset(
			2026,
			8,
			29,
			0,
			0,
			0,
			TimeSpan.FromHours(5.5));

		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = new LessonPlan(
					Guid.NewGuid(),
					TestTenantId,
					academicYearId: Guid.NewGuid(),
					teacherId: TeacherEmployeeId,
					standardId: Guid.NewGuid(),
					divisionId: Guid.NewGuid(),
					subjectId: Guid.NewGuid(),
					chapterId: Guid.NewGuid(),
					plannedDate: requestedDate.UtcDateTime,
					topic: "Offset-safe timeline plan",
					plannedCompletionPercentage: 100);
				await _lessonPlanRepository.InsertAsync(plan, autoSave: true);

				var result = await _lessonPlanAppService.GetTimelineAsync(
					requestedDate,
					TeacherEmployeeId);

				result.Slots.ShouldContain(slot => slot.LessonPlan != null && slot.LessonPlan.Id == plan.Id);
			}
		});
	}

	[Fact]
	public async Task CreateHomeworkFromLessonPlanAsync_Should_Create_Homework_From_Approved_LessonPlan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = await CreateSubmittedLessonPlanAsync(TeacherEmployeeId, "Algebra Foundations");
				await _lessonPlanAppService.ApproveAsync(plan.Id, new ReviewRemarksDto { Comment = "Approved" });

				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
				var stage = await stageRepository.GetAsync(stageId);
				var academicMediumId = stage.EducationMediumId;

				var homeworkTypeRepository = GetRequiredService<IRepository<HomeworkType, Guid>>();
				var homeworkType = await homeworkTypeRepository.InsertAsync(
					new HomeworkType(Guid.NewGuid(), "Daily Homework", academicMediumId, TestTenantId, "Standard daily homework"),
					autoSave: true);

				var response = await _lessonPlanAppService.CreateHomeworkFromLessonPlanAsync(plan.Id, new CreateHomeworkFromLessonPlanDto
				{
					AcademicMediumId = academicMediumId,
					HomeworkTypeId = homeworkType.Id,
					Title = "Algebra Homework 1",
					Description = "Solve practice problems",
					PublishAt = DateTime.UtcNow,
					DueAt = DateTime.UtcNow.AddDays(2)
				});

				response.ShouldNotBeNull();
				response.Data.ShouldNotBeNull();
				response.Data.Title.ShouldBe("Algebra Homework 1");
			}
		});
	}

	private async Task<LessonPlan> CreateSubmittedLessonPlanAsync(Guid teacherId, string topic)
	{
		var plan = new LessonPlan(
			Guid.NewGuid(),
			TestTenantId,
			academicYearId: Guid.NewGuid(),
			teacherId: teacherId,
			standardId: Guid.NewGuid(),
			divisionId: Guid.NewGuid(),
			subjectId: Guid.NewGuid(),
			chapterId: Guid.NewGuid(),
			plannedDate: DateTime.Today,
			topic: topic,
			plannedCompletionPercentage: 100);
		plan.Submit();

		return await _lessonPlanRepository.InsertAsync(plan, autoSave: true);
	}
}
