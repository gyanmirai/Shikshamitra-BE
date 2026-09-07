using System.Text.Json.Serialization;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

/// <summary>
/// Lifecycle status of a Payment Term master record.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTermStatus
{
	/// <summary>The term is available for assignment and demand generation.</summary>
	Active = 0,

	/// <summary>The term is retired; preserved for historical resolution.</summary>
	Archived = 1
}

/// <summary>
/// Public lifecycle status presented by Payment Term list and detail contracts.
/// The stored master status and version state are combined into one unambiguous value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTermLifecycleStatus
{
	Draft = 0,
	Active = 1,
	Archived = 2
}

/// <summary>
/// Lifecycle status of an individual Payment Term version snapshot.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTermVersionStatus
{
	/// <summary>Editable working copy — not yet locked.</summary>
	Draft = 0,

	/// <summary>Locked, validated, and available for demand generation.</summary>
	Published = 1,

	/// <summary>Replaced by a newer published version.</summary>
	Superseded = 2,

	/// <summary>No longer available for new assignments or adoptions.</summary>
	Archived = 3
}

/// <summary>
/// Determines how installment demand/due dates shift when they fall on a holiday.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidayShiftingMethod
{
	/// <summary>No shifting — dates remain as configured.</summary>
	None = 0,

	/// <summary>Shift to the next working day.</summary>
	NextWorkingDay = 1,

	/// <summary>Shift to the previous working day.</summary>
	PreviousWorkingDay = 2
}

/// <summary>
/// Controls when an installment's demand is generated relative to its due date.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DemandGenerationRule
{
	/// <summary>Demand is generated when the installment coverage window starts.</summary>
	OnInstallmentStartDate = 0,

	/// <summary>Demand is generated N days before the due date.</summary>
	XDaysBeforeDue = 1,

	/// <summary>Demand is generated on the due date.</summary>
	OnDueDate = 2,

	/// <summary>Demand is generated on the explicit date configured for each installment.</summary>
	SpecificDate = 3
}

/// <summary>
/// User-selectable state when a Payment Term is saved. Archive is deliberately
/// excluded because it is a guarded lifecycle action, not a form value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTermSaveStatus
{
	Draft = 0,
	Active = 1
}

/// <summary>
/// The initial status assigned to a demand record when it is generated.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InitialDemandStatus
{
	/// <summary>Demand starts as a draft pending review.</summary>
	Draft = 0,

	/// <summary>Demand is immediately published/visible.</summary>
	Published = 1
}

/// <summary>
/// How a Fee Structure detail uses its attached Payment Term.
/// </summary>
public enum ScheduleMode
{
	/// <summary>Use the master Payment Term version schedule as-is.</summary>
	Inherited = 0,

	/// <summary>Apply structure-specific date overrides on top of the master schedule.</summary>
	Customized = 1
}

/// <summary>
/// Identifies the source of a Due Fee's schedule information.
/// Used for legacy migration compatibility.
/// </summary>
public enum ScheduleSource
{
	/// <summary>Generated from a Payment Term version schedule.</summary>
	PaymentTerm = 0,

	/// <summary>Pre-existing monthly/one-time record from before Payment Terms.</summary>
	Legacy = 1
}

/// <summary>
/// Mode for demand generation commands.
/// </summary>
public enum DemandGenerationMode
{
	/// <summary>Non-mutating preview — returns what would be generated.</summary>
	Preview = 0,

	/// <summary>Creates draft demand records.</summary>
	Draft = 1,

	/// <summary>Creates published demand records.</summary>
	Publish = 2
}
