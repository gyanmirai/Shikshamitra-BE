using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollSectionReviewLockGuard_Tests
{
	private const string Month = "2026-04";
	private readonly Guid tenantId = Guid.NewGuid();
	private readonly Guid academicYearId = Guid.NewGuid();
	private readonly Guid previewRunId = Guid.NewGuid();
	private readonly Guid employeeId = Guid.NewGuid();
	private readonly DateTime submittedAt = new(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc);

	[Fact]
	public async Task Current_Returned_Review_Requires_Override_Reason()
	{
		var batch = CreateBatch(previewRunId, PayrollSubmission.Returned, submittedAt);
		var guard = CreateGuard(batch);
		var request = new RunPayrollRequest { PreviewRunId = previewRunId, SectionReviewOverrideReason = "   " };

		var failure = await Should.ThrowAsync<BusinessException>(() => guard.PrepareAsync(request, Month, [employeeId]));
		failure.Code.ShouldBe("PAYROLL_SECTION_REVIEW_OVERRIDE_REQUIRED");

		request.SectionReviewOverrideReason = "  HR verified the correction  ";
		var plan = await guard.PrepareAsync(request, Month, [employeeId]);
		plan.ReturnedBatches.ShouldBe([batch]);
		plan.OverrideReason.ShouldBe("HR verified the correction");
	}

	[Fact]
	public async Task Reviewed_Replacement_Supersedes_Returned_Review()
	{
		var guard = CreateGuard(CreateBatch(Guid.NewGuid(), PayrollSubmission.Returned, submittedAt),
			CreateBatch(previewRunId, PayrollSubmission.Reviewed, submittedAt.AddMinutes(1)));

		var plan = await guard.PrepareAsync(new() { PreviewRunId = previewRunId }, Month, [employeeId]);
		plan.OverrideBatchCount.ShouldBe(0);
	}

	[Fact]
	public async Task Recalculating_Does_Not_Clear_A_Returned_Review()
	{
		var guard = CreateGuard(CreateBatch(Guid.NewGuid(), PayrollSubmission.Returned, submittedAt));

		var failure = await Should.ThrowAsync<BusinessException>(() =>
			guard.PrepareAsync(new() { PreviewRunId = previewRunId }, Month, [employeeId]));
		failure.Code.ShouldBe("PAYROLL_SECTION_REVIEW_OVERRIDE_REQUIRED");
	}

	private PayrollSubmission CreateBatch(Guid runId, string state, DateTime at)
	{
		var batch = new PayrollSubmission(Guid.NewGuid(), tenantId, academicYearId, Month, runId, Guid.NewGuid(),
			Guid.NewGuid(), Guid.Empty, false, 1, null, 1000m, 1, at, Guid.NewGuid(), Guid.NewGuid());
		batch.Resolve(batch.Version, state, at.AddSeconds(30), "test");
		return batch;
	}

	private PayrollSectionReviewLockGuard CreateGuard(params PayrollSubmission[] batches)
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var access = new PayrollApprovalAccess(null!, null!, null!, null!, null!, null!, null!, tenant, null!, null!);
		var rows = batches.Select(batch => new PayrollSubmissionEmployee(Guid.NewGuid(), tenantId, batch.Id,
			employeeId, Guid.NewGuid(), "test", batch.NetPay)).ToArray();
		return new PayrollSectionReviewLockGuard(access, Repository(batches), Repository(rows),
			Repository<PayrollEmployeeClaim>(), new AsyncQueryableExecuter([]), null!, null!);
	}

	private static IRepository<T, Guid> Repository<T>(params T[] values) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(Task.FromResult(values.AsQueryable()));
		return repository;
	}
}
