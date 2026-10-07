using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Response DTO for salary slip as per OpenAPI contract
/// </summary>
public class SalarySlipResponse
{
	public Guid? DocumentId { get; set; }
	public int? DocumentRevision { get; set; }
	public decimal? FullGrossSalary { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal AdditionalEarnings { get; set; }
    /// <summary>
    /// Employee identifier
    /// </summary>
    [Required]
    public Guid EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public string? EmployeeCode { get; set; }

    public string? Department { get; set; }

    public string? Designation { get; set; }

    public DateOnly? DateOfJoining { get; set; }

    public string? SlipNumber { get; set; }

    /// <summary>
    /// Official salary slip status: Draft, Finalized, or Paid.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Salary period in MMMM-YYYY format (e.g., JANUARY-2025)
    /// </summary>
    [Required]
    [RegularExpression(@"^[A-Z]+-\d{4}$", ErrorMessage = "MonthYear must be in MMMM-YYYY format (e.g., JANUARY-2025)")]
    public string MonthYear { get; set; } = string.Empty;

    /// <summary>
    /// Computed total earnings
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double TotalEarning { get; set; }

    /// <summary>
    /// Computed total deductions
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double TotalDeduction { get; set; }

    /// <summary>
    /// Net payable amount (totalEarning - totalDeduction)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double NetPay { get; set; }

    public double NetSalary { get; set; }

    public string? NetPayInWords { get; set; }

    /// <summary>
    /// Salary earning components
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one earning component is required")]
    public List<SalaryComponent> Earnings { get; set; } = new();

    /// <summary>
    /// Salary deduction components
    /// </summary>
    [Required]
    public List<SalaryComponent> Deductions { get; set; } = new();

    /// <summary>
    /// Salary slip generation timestamp (ISO-8601 with timezone)
    /// </summary>
    [Required]
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Payroll basis days (denominator) when the slip was computed.
    /// </summary>
    public decimal? PayrollBasisDays { get; set; }

    /// <summary>
    /// Payable days from attendance at computation time.
    /// </summary>
    public double? PayableDays { get; set; }

    public double? PresentDays { get; set; }

    public double? HalfWorkingDays { get; set; }

    public double? LopDays { get; set; }

    /// <summary>
    /// LOP proration factor (capped at 1).
    /// </summary>
    public double? ProrationFactor { get; set; }

    /// <summary>
    /// Day calculation mode: working-days, total-month-days, or manual-days.
    /// </summary>
    public string? PayrollDayCalculationMode { get; set; }

    public string? BankName { get; set; }

    public string? MaskedAccountNumber { get; set; }

    public string? IfscCode { get; set; }

    public string? UanNumber { get; set; }

    public string? EpfNumber { get; set; }

    // Values captured when payroll was finalized, not current enrollment or compensation.
    public Guid? EsiConfigurationId { get; set; }
	public Guid? EmployeeStateInsuranceRuleId { get; set; }
    public int? EmployeeStateInsuranceRuleVersion { get; set; }
    public decimal EmployeeStateInsuranceWages { get; set; }
    public string? EmployeeStateInsuranceCoverageDecision { get; set; }
    public string? EmployeeStateInsuranceNumber { get; set; }
    public DateTime? EmployeeStateInsuranceContributionPeriodStart { get; set; }
    public DateTime? EmployeeStateInsuranceContributionPeriodEnd { get; set; }

    public string? Remarks { get; set; }
}

/// <summary>
/// Salary component DTO as per OpenAPI contract
/// </summary>
public class SalaryComponent
{
    /// <summary>
    /// Component name (e.g., Basic, PF, Tax)
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional explanation
    /// </summary>
    [StringLength(255)]
    public string? Description { get; set; }

    /// <summary>
    /// Final calculated amount
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double Amount { get; set; }

    /// <summary>
    /// Fixed for all employees or employee-specific
    /// </summary>
    [Required]
    public bool IsFixed { get; set; }

    /// <summary>
    /// Temporary or one-time component
    /// </summary>
    [Required]
    public bool IsTemp { get; set; }

    /// <summary>
    /// Calculation strategy (FIXED or PERCENTAGE)
    /// </summary>
    [Required]
    public string ComponentType { get; set; } = string.Empty;

    /// <summary>
    /// Required only when componentType is PERCENTAGE (BASIC or GROSS)
    /// </summary>
    public string? DependOn { get; set; }
}
