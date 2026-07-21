using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>What is being scoped by a CollectionPolicyExceptionWindow.</summary>
public enum ExceptionWindowScope
{
	SchoolWide = 0,
	AcademicYear = 1,
	FeeStructure = 2,
	ClassSection = 3
}

/// <summary>
/// What an active window does to late-fee accrual/collection for the period it covers.
/// ExtensionDays is only meaningful when Effect = ExtendGracePeriod.
/// </summary>
public enum ExceptionWindowEffect
{
	SuspendLateFeeAccrual = 0,
	ExtendGracePeriod = 1,
	DisableOnlineCollection = 2,
	Custom = 3
}

/// <summary>
/// Lets a school proactively suspend late-fee accrual (or extend grace, or disable online
/// collection) for a defined window — e.g. a payment-gateway outage, natural disaster, or bank
/// strike — instead of relying on Bulk Waiver to forgive fees that were already, incorrectly,
/// charged (implementation plan §4j). Standalone aggregate, not nested under CollectionPolicy,
/// since a window can apply across every policy in its scope (e.g. SchoolWide) rather than
/// belonging to one policy.
/// </summary>
public class CollectionPolicyExceptionWindow : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public ExceptionWindowScope Scope { get; protected set; }
	/// <summary>AcademicYearId, FeeStructureId, or ClassSection (Division) Id depending on Scope. Null when Scope = SchoolWide.</summary>
	public Guid? ScopeId { get; protected set; }
	public DateTime StartDate { get; protected set; }
	public DateTime EndDate { get; protected set; }
	public ExceptionWindowEffect Effect { get; protected set; }
	public int? ExtensionDays { get; protected set; }
	public string ReasonCode { get; protected set; } = string.Empty;
	public string Reason { get; protected set; } = string.Empty;
	public Guid CreatedByUserId { get; protected set; }

	protected CollectionPolicyExceptionWindow()
	{
	}

	public CollectionPolicyExceptionWindow(
		Guid id,
		Guid? tenantId,
		ExceptionWindowScope scope,
		Guid? scopeId,
		DateTime startDate,
		DateTime endDate,
		ExceptionWindowEffect effect,
		int? extensionDays,
		string reasonCode,
		string reason,
		Guid createdByUserId) : base(id)
	{
		TenantId = tenantId;

		if (scope != ExceptionWindowScope.SchoolWide && scopeId is null)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowScopeIdRequired)
				.WithData("scope", scope.ToString());
		}

		if (endDate.Date < startDate.Date)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowInvalidDateRange);
		}

		if (effect == ExceptionWindowEffect.ExtendGracePeriod && (extensionDays is null || extensionDays <= 0))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowExtensionDaysRequired);
		}

		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowReasonRequired);
		}

		Scope = scope;
		ScopeId = scope == ExceptionWindowScope.SchoolWide ? null : scopeId;
		StartDate = startDate.Date;
		EndDate = endDate.Date;
		Effect = effect;
		ExtensionDays = effect == ExceptionWindowEffect.ExtendGracePeriod ? extensionDays : null;
		ReasonCode = reasonCode?.Trim() ?? string.Empty;
		Reason = reason.Trim();
		CreatedByUserId = createdByUserId;
	}

	/// <summary>Only permitted before the window has started — once active, history isn't deleted; an admin creates a corrective window instead (implementation plan §4j API bullet).</summary>
	public void EnsureDeletable(DateTime asOf)
	{
		if (asOf.Date >= StartDate)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive)
				.WithData("startDate", StartDate);
		}
	}

	public bool CoversDate(DateTime date) => date.Date >= StartDate && date.Date <= EndDate;
}
