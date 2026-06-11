using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotReviewInvalidator_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly PayrollSnapshotReviewInvalidator _invalidator;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _snapshotRepository;
	private readonly IRepository<PayrollRunReview, Guid> _reviewRepository;
	private readonly IGuidGenerator _guidGenerator;

	public PayrollSnapshotReviewInvalidator_Tests()
	{
		_invalidator = GetRequiredService<PayrollSnapshotReviewInvalidator>();
		_snapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_reviewRepository = GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task InvalidateUnlockedForEmployeesAsync_Should_Delete_Unlocked_Snapshots_And_Reviews_But_Preserve_Locked_Snapshots()
	{
		var employeeId = _guidGenerator.Create();
		var reviewerId = _guidGenerator.Create();
		var unlockedMonth = "2026-06";
		var lockedMonth = "2026-07";

		await WithUnitOfWorkAsync(async () =>
		{
			var unlockedSnapshot = new PayrollRunItemSnapshot(
				_guidGenerator.Create(),
				tenantId: null,
				unlockedMonth,
				employeeId);
			unlockedSnapshot.MarkReviewed(reviewerId, DateTime.UtcNow);
			await _snapshotRepository.InsertAsync(unlockedSnapshot, autoSave: false);

			var lockedSnapshot = new PayrollRunItemSnapshot(
				_guidGenerator.Create(),
				tenantId: null,
				lockedMonth,
				employeeId);
			lockedSnapshot.MarkLocked();
			await _snapshotRepository.InsertAsync(lockedSnapshot, autoSave: false);

			await _reviewRepository.InsertAsync(
				new PayrollRunReview(
					_guidGenerator.Create(),
					tenantId: null,
					unlockedMonth,
					employeeId,
					reviewerId,
					DateTime.UtcNow),
				autoSave: false);
		});

		await WithUnitOfWorkAsync(async () =>
			await _invalidator.InvalidateUnlockedForEmployeesAsync(new[] { employeeId }));

		await WithUnitOfWorkAsync(async () =>
		{
			var snapshots = await _snapshotRepository.GetListAsync(x => x.EmployeeId == employeeId);
			snapshots.Any(x => x.MonthYear == unlockedMonth).ShouldBeFalse();
			snapshots.Single(x => x.MonthYear == lockedMonth).IsLocked.ShouldBeTrue();

			var reviews = await _reviewRepository.GetListAsync(x => x.EmployeeId == employeeId);
			reviews.Any(x => x.MonthYear == unlockedMonth).ShouldBeFalse();
		});
	}
}
