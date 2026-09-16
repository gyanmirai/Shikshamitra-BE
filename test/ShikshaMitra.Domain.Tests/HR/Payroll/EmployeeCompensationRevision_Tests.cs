using System;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EmployeeCompensationRevision_Tests
{
	[Fact]
	public void Should_make_a_direct_save_effective_and_preserve_the_snapshot()
	{
		var savedBy = Guid.NewGuid();
		var revision = BuildDraft();

		revision.MakeEffective(savedBy, new DateTime(2026, 8, 24, 9, 5, 0, DateTimeKind.Utc));
		revision.Status.ShouldBe(CompensationRevisionStatus.Approved);
		revision.SubmittedByUserId.ShouldBe(savedBy);
		revision.ApprovedByUserId.ShouldBe(savedBy);
		revision.IdempotencyKey.ShouldBe("save-operation-1");
		revision.CustomizationReason.ShouldBe("Adjusted basic for contract terms");
		Should.Throw<BusinessException>(() => revision.ReplaceDraft(
			new DateTime(2026, 8, 1),
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			20_000m,
			20_000m,
			19_370m,
			19_224m,
			20_000m,
			null,
			null,
			null,
			null,
			"IN-GJ",
			"{}"));
	}

	[Fact]
	public void Should_allow_an_effective_revision_to_be_superseded()
	{
		var revision = BuildDraft();
		revision.MakeEffective(Guid.NewGuid(), new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc));
		revision.Supersede();

		revision.Status.ShouldBe(CompensationRevisionStatus.Superseded);
	}

	[Fact]
	public void Should_mark_a_legacy_migration_effective_without_inventing_an_approver()
	{
		var revision = BuildDraft();
		var migratedAt = new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);

		revision.MakeEffectiveFromLegacy(migratedAt);

		revision.Status.ShouldBe(CompensationRevisionStatus.Approved);
		revision.SubmittedByUserId.ShouldBeNull();
		revision.ApprovedByUserId.ShouldBeNull();
		revision.ApprovedAt.ShouldBe(migratedAt);
	}

	private static EmployeeCompensationRevision BuildDraft()
	{
		var revision = new EmployeeCompensationRevision(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			1,
			new DateTime(2026, 8, 1),
			"save-operation-1");
		revision.ReplaceDraft(
			new DateTime(2026, 8, 1),
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			20_000m,
			20_000m,
			19_370m,
			19_224m,
			20_000m,
			null,
			null,
			null,
			null,
			"IN-GJ",
			"{}",
			"[{\"componentId\":\"basic\"}]",
			"Adjusted basic for contract terms");
		return revision;
	}
}
