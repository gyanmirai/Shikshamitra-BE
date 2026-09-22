using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentCopyMarker_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Editing_A_Copied_Assignment_Should_Clear_The_Copy_Marker()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await MarkAssignmentAsCopiedAsync();

			await ChangeTeacherAsync(SubjectStatus.Draft);

			(await GetAssignmentAsync()).IsCopiedFromPreviousYear.ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Publishing_A_Copied_Assignment_By_Status_Should_Clear_The_Copy_Marker()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await MarkAssignmentAsCopiedAsync();

			await Allocations.ChangeStatusAsync(AssignmentId, new ChangeStatusRequestDto
			{
				Status = (int)SubjectStatus.Published
			});

			(await GetAssignmentAsync()).IsCopiedFromPreviousYear.ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Bulk_Publishing_A_Copied_Assignment_Should_Clear_The_Copy_Marker()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await MarkAssignmentAsCopiedAsync();

			await Allocations.PublishAsync([AssignmentId]);

			(await GetAssignmentAsync()).IsCopiedFromPreviousYear.ShouldBeFalse();
		});
	}

	private async Task MarkAssignmentAsCopiedAsync()
	{
		var assignment = await GetAssignmentAsync();
		assignment.IsCopiedFromPreviousYear = true;
		await GetRequiredService<IRepository<AssignedSubject, Guid>>()
			.UpdateAsync(assignment, autoSave: true);
	}

	private Task<AssignedSubject> GetAssignmentAsync() =>
		GetRequiredService<IRepository<AssignedSubject, Guid>>().GetAsync(AssignmentId);
}
