using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmActivation_Tests
{
	[Fact]
	public void Number_series_requires_maker_checker_and_allocates_without_reuse()
	{
		var maker = Guid.NewGuid();
		var series = CreateSeries();
		series.Submit(maker);
		Should.Throw<BusinessException>(() => series.Publish(maker)).Code
			.ShouldBe("AdmissionCrm:DistinctNumberSeriesApproverRequired");
		series.Publish(Guid.NewGuid());
		var first = series.Allocate(DateTime.UtcNow);
		var second = series.Allocate(DateTime.UtcNow);
		first.Value.ShouldBe("GR-000100");
		second.Value.ShouldBe("GR-000101");
		second.Sequence.ShouldBe(first.Sequence + 1);
	}

	[Fact]
	public void Number_series_exhaustion_is_explicit()
	{
		var series = new AdmissionCrmNumberSeries(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"GR-TINY", "Tiny", AdmissionCrmIdentifierType.GrNumber, null, null, null,
			"", "", 2, 1, 1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
		series.Submit(Guid.NewGuid()); series.Publish(Guid.NewGuid());
		series.Allocate(DateTime.UtcNow).Sequence.ShouldBe(1);
		Should.Throw<BusinessException>(() => series.Allocate(DateTime.UtcNow)).Code
			.ShouldBe("AdmissionCrm:NumberSeriesExhausted");
	}

	[Fact]
	public void Activation_requires_distinct_approver_and_preserves_repair_attempts()
	{
		var maker = Guid.NewGuid();
		var activation = CreateActivation(maker);
		Should.Throw<BusinessException>(() => activation.Approve(maker, DateTime.UtcNow)).Code
			.ShouldBe("AdmissionCrm:DistinctActivationApproverRequired");
		activation.Approve(Guid.NewGuid(), DateTime.UtcNow);
		activation.StartAttempt();
		activation.RequireRepair("account", "Account provider was unavailable.");
		activation.Status.ShouldBe(AdmissionCrmActivationStatus.RepairRequired);
		activation.StartAttempt();
		activation.AttemptCount.ShouldBe(2);
		activation.LinkStudent(Guid.NewGuid());
		activation.Complete(DateTime.UtcNow);
		activation.Status.ShouldBe(AdmissionCrmActivationStatus.FirstDayReady);
	}

	[Fact]
	public void Committed_identifier_is_not_silently_reallocated()
	{
		var allocation = new AdmissionCrmIdentifierAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AdmissionCrmIdentifierType.GrNumber,
			100, "GR-000100", DateTime.UtcNow);
		allocation.Commit(Guid.NewGuid(), DateTime.UtcNow);
		Should.Throw<BusinessException>(() => allocation.Commit(Guid.NewGuid(), DateTime.UtcNow)).Code
			.ShouldBe("AdmissionCrm:IdentifierNotAllocatable");
	}

	[Fact]
	public void Activation_cancellation_requires_distinct_approver_and_voids_identifier_with_reason()
	{
		var requester = Guid.NewGuid();
		var activation = CreateActivation(Guid.NewGuid());
		activation.Approve(Guid.NewGuid(), DateTime.UtcNow);
		activation.StartAttempt();
		activation.LinkStudent(Guid.NewGuid());
		activation.Complete(DateTime.UtcNow);
		activation.RequestCancellation("Duplicate same-cycle activation confirmed.", requester, DateTime.UtcNow);
		activation.Status.ShouldBe(AdmissionCrmActivationStatus.CancellationPending);
		Should.Throw<BusinessException>(() => activation.Cancel(requester, DateTime.UtcNow)).Code
			.ShouldBe("AdmissionCrm:DistinctActivationCancellationApproverRequired");
		activation.Cancel(Guid.NewGuid(), DateTime.UtcNow);
		activation.Status.ShouldBe(AdmissionCrmActivationStatus.Cancelled);

		var allocation = new AdmissionCrmIdentifierAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), activation.Id, Guid.NewGuid(), AdmissionCrmIdentifierType.GrNumber,
			101, "GR-000101", DateTime.UtcNow);
		allocation.Commit(activation.StudentId!.Value, DateTime.UtcNow);
		allocation.Void(activation.CancellationReason!);
		allocation.Status.ShouldBe(AdmissionCrmIdentifierStatus.Void);
		allocation.LifecycleReason.ShouldBe("Duplicate same-cycle activation confirmed.");
	}

	private static AdmissionCrmNumberSeries CreateSeries() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"GR-MAIN", "GR main", AdmissionCrmIdentifierType.GrNumber, null, null, null,
		"GR-", "", 6, 100, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddYears(1));

	private static AdmissionCrmActivation CreateActivation(Guid maker) => new(Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), "ACT-TEST", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, ShikshaMitra.Students.GenderEnum.Female,
		"{}", "[]", true, true, maker, DateTime.UtcNow);
}
