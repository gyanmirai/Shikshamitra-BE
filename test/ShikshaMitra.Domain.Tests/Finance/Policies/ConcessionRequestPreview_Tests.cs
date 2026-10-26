using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionRequestPreview_Tests
{
	[Fact]
	public async Task Direct_approval_validation_preserves_saved_amount_when_percentage_capacity_increases()
	{
		var (manager, student, type, assignment, policy, version, dues) = BuildManager(percentage: true);
		var due = dues[1];
		due.ApplyPayment(10000m);
		var request = await manager.CreatePendingAsync(null, policy.AcademicYearId, student.Id, policy.PolicyId,
			version.Id, type.Id, null, [], assignment.Id, allowIncomplete: true, enforcePendingUniqueness: false,
			dueFeeConcessions: [new(due.Id, due.FeeHeadId, 10m)]);
		var savedEvidence = request.RequestInputSnapshotJson;
		var savedDueIds = request.SelectedDueFeeIdsJson;
		Assert.Equal(2000m, request.GetTotalRequestedConcession());

		due.ReversePayment(10000m);
		await manager.ValidateDraftApprovalAsync(request);

		Assert.Equal(savedEvidence, request.RequestInputSnapshotJson);
		Assert.Equal(savedDueIds, request.SelectedDueFeeIdsJson);
		Assert.Equal(2000m, request.GetTotalRequestedConcession());
		var evaluation = await manager.EvaluateSubmittedAsync(request);
		Assert.Equal(3000m, evaluation.Mappings.Sum(x => x.FeeHeadConcessionAmount));
		var allocations = manager.BuildSubmittedDueFeeAllocations(request, evaluation.DueFees.ToList(), evaluation.Policy);
		Assert.Equal(2000m, allocations.Sum(x => x.ConcessionAmount));
	}
	[Fact]
	public async Task Preview_and_submission_use_the_same_selected_installments_with_unentered_siblings()
	{
		var (manager, student, type, assignment, policy, version, dues) = BuildManager();
		DueFeeConcessionInput[] inputs = [new(dues[0].Id, dues[0].FeeHeadId, 1m), new(dues[1].Id, dues[1].FeeHeadId, 10500m)];
		var preview = await manager.PreviewAsync(null, policy.AcademicYearId, student.Id, policy.PolicyId,
			version.Id, type.Id, [], assignment.Id, inputs);
		var request = await manager.CreatePendingAsync(null, policy.AcademicYearId, student.Id, policy.PolicyId,
			version.Id, type.Id, null, [], assignment.Id, dueFeeConcessions: inputs);
		var submitted = await manager.EvaluateSubmittedAsync(request);

		Assert.Equal(2, preview.EligibleDueFees.Count);
		Assert.DoesNotContain(preview.EligibleDueFees, due => due.Id == dues[2].Id);
		Assert.Equal(preview.EligibleDueFees.Select(x => x.Id).Order(), submitted.DueFees.Select(x => x.Id).Order());
		Assert.Equal(10501m, preview.Mappings.Sum(x => x.FeeHeadConcessionAmount));
		Assert.Equal(preview.Mappings.Select(x => x.FeeHeadConcessionAmount), submitted.Mappings.Select(x => x.FeeHeadConcessionAmount));
		Assert.Equal(2, request.GetSelectedDueFeeIds().Count);
		request.Submit(DateTime.UtcNow);
		Assert.Equal(ConcessionRequestStatus.Pending, request.Status);
	}

	[Fact]
	public async Task Fractional_installment_is_rejected_with_its_identity_instead_of_rounded()
	{
		var (manager, student, type, assignment, policy, version, dues) = BuildManager();
		var exception = await Assert.ThrowsAsync<BusinessException>(() => manager.PreviewAsync(null,
			policy.AcademicYearId, student.Id, policy.PolicyId, version.Id, type.Id, [], assignment.Id,
			[new DueFeeConcessionInput(dues[0].Id, dues[0].FeeHeadId, 0.1m)]));
		Assert.Equal(FinanceConcessionErrorCodes.WholeRupeeAmountRequired, exception.Code);
		Assert.Equal(dues[0].Id, exception.Data["DueFeeId"]);
		Assert.Equal(dues[0].FeeHeadId, exception.Data["FeeHeadId"]);
	}

	private static (ConcessionRequestManager Manager, Student Student, ConcessionType Type,
		StudentFeeStructure Assignment, ConcessionPolicySnapshot Policy, FinancePolicyVersion Version, List<DueFee> Dues) BuildManager(bool percentage = false)
	{
		var yearId = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), null, "Synthetic", null, "Student", yearId,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var type = new ConcessionType(Guid.NewGuid(), null, yearId, "Test concession");
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, student.Id, Guid.NewGuid(), yearId);
		var policy = new ConcessionPolicySnapshot(Guid.NewGuid(), yearId, "8th test policy", "TEST", ConcessionPolicyStatus.Active,
			false, ConcessionValueSource.RequestEnteredWithinLimits,
			percentage ? ConcessionCalculationMethod.Percentage : ConcessionCalculationMethod.FixedAmount,
			null, null, percentage ? 100m : null, percentage ? null : 11111m,
			ConcessionApplyOn.AllOutstanding, RoundingMethodType.RoundUp, 19);
		var version = new FinancePolicyVersion(Guid.NewGuid(), null, "concession", policy.PolicyId, yearId,
			19, policy.PolicyName, "Active", JsonSerializer.Serialize(policy), "created");
		var head = new FeeHead(Guid.NewGuid(), null, yearId, Guid.NewGuid(), "Tuition", null, true, null, Guid.NewGuid());
		var dues = new[] { 1m, 30000m, 29197m }.Select((amount, index) =>
		{
			var due = new DueFee(Guid.NewGuid(), null, student.Id, head.Id,
				(MonthEnum)(index + 1), 2026, false, amount, new DateTime(2026, index + 1, 1));
			due.BindAssignment(assignment.Id);
			return due;
		}).ToList();
		var studentRepository = Repository(student);
		var typeRepository = Repository(type);
		var dueRepository = Repository(dues.ToArray());
		var assignmentRepository = Repository(assignment);
		var versionRepository = Repository(version);
		var requestRepository = Substitute.For<IConcessionRequestRepository>();
		requestRepository.InsertAsync(Arg.Any<ConcessionRequest>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => Task.FromResult(call.Arg<ConcessionRequest>()));
		var guidGenerator = Substitute.For<IGuidGenerator>();
		guidGenerator.Create().Returns(_ => Guid.NewGuid());
		var manager = new ConcessionRequestManager(requestRepository, studentRepository, typeRepository,
			dueRepository, assignmentRepository, Repository<FeeStructure>(), Repository(head), versionRepository,
			new ConcessionPolicyCalculationService(), guidGenerator, NullLogger<ConcessionRequestManager>.Instance);
		return (manager, student, type, assignment, policy, version, dues);
	}

	private static IRepository<TEntity, Guid> Repository<TEntity>(params TEntity[] items)
		where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetListAsync(Arg.Any<Expression<Func<TEntity, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.Where(call.Arg<Expression<Func<TEntity, bool>>>().Compile()).ToList());
		repository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.SingleOrDefault(item => item.Id == call.Arg<Guid>()));
		repository.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.Single(item => item.Id == call.Arg<Guid>()));
		return repository;
	}
}
