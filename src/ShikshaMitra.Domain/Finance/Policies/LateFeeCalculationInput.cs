using System;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Inputs to <see cref="LateFeeCalculationService"/> — deliberately plain financial
/// primitives (not a DueFee entity reference) so the same service works identically
/// whether called from a real collection, an impact preview, or the /simulate endpoint,
/// per the "single source of truth for calculation" principle (implementation plan §2).
/// </summary>
public sealed record LateFeeCalculationInput(
	int DaysLate,
	decimal OutstandingAmount,
	decimal OriginalFeeAmount,
	decimal RemainingBalance,
	Guid? FeeHeadId,
	bool IsLateFeeExempt = false,
	int? CustomGracePeriodDays = null,
	/// <summary>Phase 4j — the effect of the active CollectionPolicyExceptionWindow covering the
	/// evaluation date for this due line's scope, resolved by ExceptionWindowResolver (a DB-touching
	/// domain service) before calling Calculate, so this service stays a pure function.</summary>
	ExceptionWindowEffect? ActiveExceptionWindowEffect = null,
	int? ExceptionWindowExtensionDays = null,
	string? ExceptionWindowLabel = null);
