using Shouldly;
using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyExceptionWindow_Tests
{
	private static readonly Guid TestTenantId = Guid.NewGuid();
	private static readonly Guid CreatedByUserId = Guid.NewGuid();

	private static CollectionPolicyExceptionWindow BuildSchoolWideWindow(
		DateTime? startDate = null, DateTime? endDate = null,
		ExceptionWindowEffect effect = ExceptionWindowEffect.SuspendLateFeeAccrual, int? extensionDays = null) =>
		new(
			Guid.NewGuid(), TestTenantId, ExceptionWindowScope.SchoolWide, null,
			startDate ?? new DateTime(2026, 6, 1), endDate ?? new DateTime(2026, 6, 10),
			effect, extensionDays, "GATEWAY_OUTAGE", "Payment gateway was down for 10 days.", CreatedByUserId);

	[Fact]
	public void SchoolWide_Scope_Does_Not_Require_A_ScopeId()
	{
		var window = BuildSchoolWideWindow();
		window.ScopeId.ShouldBeNull();
	}

	[Fact]
	public void Non_SchoolWide_Scope_Requires_A_ScopeId()
	{
		var ex = Should.Throw<BusinessException>(() => new CollectionPolicyExceptionWindow(
			Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear, null,
			new DateTime(2026, 6, 1), new DateTime(2026, 6, 10),
			ExceptionWindowEffect.SuspendLateFeeAccrual, null, "REASON", "A real reason.", CreatedByUserId));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowScopeIdRequired);
	}

	[Fact]
	public void EndDate_Before_StartDate_Is_Rejected()
	{
		var ex = Should.Throw<BusinessException>(() =>
			BuildSchoolWideWindow(startDate: new DateTime(2026, 6, 10), endDate: new DateTime(2026, 6, 1)));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowInvalidDateRange);
	}

	[Fact]
	public void ExtendGracePeriod_Without_ExtensionDays_Is_Rejected()
	{
		var ex = Should.Throw<BusinessException>(() =>
			BuildSchoolWideWindow(effect: ExceptionWindowEffect.ExtendGracePeriod, extensionDays: null));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowExtensionDaysRequired);
	}

	[Fact]
	public void SuspendLateFeeAccrual_Ignores_Any_ExtensionDays_Given()
	{
		var window = BuildSchoolWideWindow(effect: ExceptionWindowEffect.SuspendLateFeeAccrual, extensionDays: 99);
		window.ExtensionDays.ShouldBeNull();
	}

	[Fact]
	public void Empty_Reason_Is_Rejected()
	{
		var ex = Should.Throw<BusinessException>(() => new CollectionPolicyExceptionWindow(
			Guid.NewGuid(), TestTenantId, ExceptionWindowScope.SchoolWide, null,
			new DateTime(2026, 6, 1), new DateTime(2026, 6, 10),
			ExceptionWindowEffect.SuspendLateFeeAccrual, null, "REASON", "  ", CreatedByUserId));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowReasonRequired);
	}

	[Fact]
	public void CoversDate_Is_Inclusive_Of_Both_Boundaries()
	{
		var window = BuildSchoolWideWindow(startDate: new DateTime(2026, 6, 1), endDate: new DateTime(2026, 6, 10));

		window.CoversDate(new DateTime(2026, 6, 1)).ShouldBeTrue();
		window.CoversDate(new DateTime(2026, 6, 10)).ShouldBeTrue();
		window.CoversDate(new DateTime(2026, 5, 31)).ShouldBeFalse();
		window.CoversDate(new DateTime(2026, 6, 11)).ShouldBeFalse();
	}

	[Fact]
	public void EnsureDeletable_Allows_Deletion_Before_The_Window_Starts()
	{
		var window = BuildSchoolWideWindow(startDate: new DateTime(2026, 6, 1), endDate: new DateTime(2026, 6, 10));
		Should.NotThrow(() => window.EnsureDeletable(new DateTime(2026, 5, 20)));
	}

	[Fact]
	public void EnsureDeletable_Rejects_Deletion_Once_The_Window_Has_Started()
	{
		var window = BuildSchoolWideWindow(startDate: new DateTime(2026, 6, 1), endDate: new DateTime(2026, 6, 10));

		var ex = Should.Throw<BusinessException>(() => window.EnsureDeletable(new DateTime(2026, 6, 1)));
		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive);
	}
}
