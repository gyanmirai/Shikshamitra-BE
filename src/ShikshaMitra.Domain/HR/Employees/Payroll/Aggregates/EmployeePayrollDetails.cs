using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Represents detailed payroll configuration for an employee.
/// </summary>
public class EmployeePayrollDetails : Entity<Guid>
{
	public Guid EmployeeId { get; set; }

	public string? BankName { get; set; }

	public string? BranchName { get; set; }

	public string? AccountHolderName { get; set; }

	public string? AccountNumber { get; set; }

	public string? IfscCode { get; set; }

	public string? PfRule { get; set; }

	public string? EpfNumber { get; set; }

	public string? UanNumber { get; set; }

	public string? PunchId { get; set; }

	public decimal? TotalWorkingHourPerMonth { get; set; }
	public string? QuotedSalaryBasis { get; private set; }
	public decimal? QuotedMonthlyAmount { get; private set; }
	public Guid? SourceSalaryTemplateId { get; private set; }
	public Guid? SourceSalaryTemplateVersionId { get; private set; }
	public int? SourceSalaryTemplateVersion { get; private set; }
	public DateTime? SalaryGeneratedAt { get; private set; }
	public bool WasModifiedAfterGeneration { get; private set; }

	// Navigation property
	public Employee Employee { get; set; } = null!;

	protected EmployeePayrollDetails()
	{
	}

	public EmployeePayrollDetails(Guid id, Guid employeeId) : base(id)
	{
		EmployeeId = employeeId;
	}

	public void UpdateDetails(
		string? bankName,
		string? branchName,
		string? accountHolderName,
		string? accountNumber,
		string? ifscCode,
		string? pfRule,
		string? epfNumber,
		string? uanNumber,
		string? punchId,
		decimal? totalWorkingHourPerMonth)
	{
		BankName = bankName;
		BranchName = branchName;
		AccountHolderName = accountHolderName;
		AccountNumber = accountNumber;
		IfscCode = ifscCode;
		PfRule = pfRule;
		EpfNumber = epfNumber;
		UanNumber = uanNumber;
		PunchId = punchId;
		TotalWorkingHourPerMonth = totalWorkingHourPerMonth;
	}

	public void SetSalaryGeneration(
		string? quotedSalaryBasis,
		decimal? quotedMonthlyAmount,
		Guid? sourceTemplateId,
		Guid? sourceTemplateVersionId,
		int? sourceTemplateVersion,
		DateTime? generatedAt,
		bool wasModified)
	{
		var hasAnyProvenance = sourceTemplateId.HasValue || sourceTemplateVersionId.HasValue ||
			sourceTemplateVersion.HasValue || generatedAt.HasValue;
		if (hasAnyProvenance &&
			(sourceTemplateId is not { } templateId || templateId == Guid.Empty ||
			 sourceTemplateVersionId is not { } versionId || versionId == Guid.Empty ||
			 sourceTemplateVersion is not > 0 || !generatedAt.HasValue ||
			 quotedMonthlyAmount is not > 0m || quotedSalaryBasis is not ("GROSS" or "TAKE_HOME")))
		{
			throw new Volo.Abp.BusinessException("HR:Payroll:SalaryGenerationProvenanceInvalid");
		}

		QuotedSalaryBasis = quotedSalaryBasis;
		QuotedMonthlyAmount = quotedMonthlyAmount;
		SourceSalaryTemplateId = sourceTemplateId;
		SourceSalaryTemplateVersionId = sourceTemplateVersionId;
		SourceSalaryTemplateVersion = sourceTemplateVersion;
		SalaryGeneratedAt = generatedAt;
		WasModifiedAfterGeneration = wasModified;
	}

	public void MarkSalaryComponentsModified()
	{
		if (SourceSalaryTemplateVersionId.HasValue)
		{
			WasModifiedAfterGeneration = true;
		}
	}
}
