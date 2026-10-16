using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task GetSeatingPlanWorkspaceAsync_Should_Match_Fresh_Readiness_For_Every_Plan(
		bool allStudentsAllocated,
		bool published)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planIds, _, students) = await CreateAllocatedSeparateSessionPlansAsync();
				if (!allStudentsAllocated)
				{
					await _seatAllocationRepository.DeleteAsync(
						allocation => allocation.SeatingPlanId == planIds[0], autoSave: true);
				}
				if (published)
				{
					await _seatAllocationAppService.PublishExaminationSeatingAsync(
						exam.Id, new PublishSeatingPlanInputDto());
				}

				var workspace = await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id, planIds[0]);
				var readiness = await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);

				workspace.SelectedSeatingPlanId.ShouldBe(planIds[0]);
				workspace.Version.ShouldBe((await _seatingPlanRepository.GetAsync(planIds[0])).ConcurrencyStamp);
				workspace.Plans.Count.ShouldBe(2);
				workspace.Plans.ShouldAllBe(plan => plan.TotalStudents == students.Count);
				workspace.Sessions.ShouldAllBe(session => session.TotalStudents == students.Count);
				workspace.Readiness.TotalStudentSeats.ShouldBe(students.Count * 2);
				workspace.Readiness.AllocatedStudentSeats.ShouldBe(students.Count * (allStudentsAllocated ? 2 : 1));
				workspace.Readiness.IsReadyToPublish.ShouldBe(allStudentsAllocated);
				workspace.Readiness.IsPublished.ShouldBe(published);
				JsonSerializer.Serialize(workspace.Readiness).ShouldBe(JsonSerializer.Serialize(readiness));
				if (!allStudentsAllocated)
				{
					workspace.Readiness.Blockers.ShouldContain(issue =>
						issue.Code == "STUDENT_NOT_ALLOCATED" && issue.SeatingPlanId == planIds[0]);
				}
			}
		});
	}
}
