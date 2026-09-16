using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.BackgroundWork;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentScheduledPublication_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Publication_Should_Page_Across_Tenants_Keep_Due_Boundary_And_Be_Idempotent()
	{
		var now = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
		var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
		var repository = GetRequiredService<IRepository<AssignmentEntity, Guid>>();
		var expectedCounts = new Dictionary<Guid, int>();
		foreach (var tenantId in tenants)
		{
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
					var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
					var type = await GetRequiredService<AssignmentTypeManager>().CreateAsync(stage.EducationMediumId,
						"Scheduled publication", defaultSubmissionType: SubmissionType.OnlineText,
						defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
					var count = AcademicBackgroundBatchRunner.BatchSize / 2 + 2;
					expectedCounts[tenantId] = count;
					var assignments = new List<AssignmentEntity>();
					for (var index = 0; index < count + 2; index++)
					{
						var assignment = new AssignmentEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
							"Scheduled work", "Details", type.Id, SubmissionType.OnlineText, 10, tenantId);
						assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id,
							Guid.NewGuid(), now, now.AddDays(1), tenantId));
						assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id,
							Guid.NewGuid(), null, now.AddDays(1), tenantId));
						assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id,
							Guid.NewGuid(), now.AddTicks(1), now.AddDays(1), tenantId));
						if (index != count) assignment.ChangeStatus(AssignmentStatus.Published);
						if (index == count + 1) assignment.ChangeStatus(AssignmentStatus.Closed);
						assignments.Add(assignment);
					}
					await repository.InsertManyAsync(assignments);
				});
			}
		}

		var service = GetRequiredService<AssignmentScheduledPublicationService>();
		(await service.PublishDueSchedulesAsync(now)).ShouldBe(expectedCounts.Values.Sum() * 2);
		(await service.PublishDueSchedulesAsync(now)).ShouldBe(0);
		foreach (var tenantId in tenants)
		{
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var schedules = await GetRequiredService<IRepository<AssignmentDivisionSchedule, Guid>>().GetListAsync();
					schedules.Count(schedule => schedule.IsPublished).ShouldBe(expectedCounts[tenantId] * 2);
					schedules.Where(schedule => schedule.IsPublished).ShouldAllBe(schedule => schedule.PublishedAt == now);
					schedules.Where(schedule => schedule.PublishDate > now).ShouldAllBe(schedule => !schedule.IsPublished);
				});
			}
		}
	}
}
