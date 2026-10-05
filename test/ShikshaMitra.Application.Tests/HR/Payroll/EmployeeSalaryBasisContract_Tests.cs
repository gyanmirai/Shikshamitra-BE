using System;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EmployeeSalaryBasisContract_Tests
{
	[Theory]
	[InlineData("GROSS", true)]
	[InlineData("EMPLOYER_COST", true)]
	[InlineData("TAKE_HOME", false)]
	[InlineData("OTHER", false)]
	public void Both_contracts_and_domain_should_accept_only_supported_bases(string basis, bool accepted)
	{
		foreach (var withProvenance in new[] { false, true })
		{
			var salary = new SalaryDetailsContract
			{
				QuotedSalaryBasis = basis, QuotedMonthlyAmount = 10000m,
				SourceSalaryTemplateId = withProvenance ? Guid.NewGuid() : null,
				SourceSalaryTemplateVersionId = withProvenance ? Guid.NewGuid() : null,
				SourceSalaryTemplateVersion = withProvenance ? 1 : null,
				SalaryGeneratedAt = withProvenance ? DateTime.UtcNow : null
			};
			new SalaryDetailsContractValidator().Validate(salary).IsValid.ShouldBe(accepted);
			new UpdateSalaryStructureDtoValidator().Validate(new UpdateSalaryStructureDto
			{
				QuotedSalaryBasis = salary.QuotedSalaryBasis, QuotedMonthlyAmount = salary.QuotedMonthlyAmount,
				SourceSalaryTemplateId = salary.SourceSalaryTemplateId,
				SourceSalaryTemplateVersionId = salary.SourceSalaryTemplateVersionId,
				SourceSalaryTemplateVersion = salary.SourceSalaryTemplateVersion,
				SalaryGeneratedAt = salary.SalaryGeneratedAt
			}).IsValid.ShouldBe(accepted);
			var details = new EmployeePayrollDetails(Guid.NewGuid(), Guid.NewGuid());
			Action save = () => details.SetSalaryGeneration(basis, salary.QuotedMonthlyAmount,
				salary.SourceSalaryTemplateId, salary.SourceSalaryTemplateVersionId,
				salary.SourceSalaryTemplateVersion, salary.SalaryGeneratedAt, false);
			if (accepted)
			{
				save();
				details.QuotedSalaryBasis.ShouldBe(basis);
			}
			else
			{
				Should.Throw<BusinessException>(save);
			}
		}
	}

	[Theory]
	[InlineData("GROSS")]
	[InlineData("EMPLOYER_COST")]
	public void Incomplete_template_provenance_should_still_be_rejected(string basis)
	{
		var templateId = Guid.NewGuid();
		new SalaryDetailsContractValidator().Validate(new SalaryDetailsContract
		{
			QuotedSalaryBasis = basis, QuotedMonthlyAmount = 10000m, SourceSalaryTemplateId = templateId
		}).IsValid.ShouldBeFalse();
		new UpdateSalaryStructureDtoValidator().Validate(new UpdateSalaryStructureDto
		{
			QuotedSalaryBasis = basis, QuotedMonthlyAmount = 10000m, SourceSalaryTemplateId = templateId
		}).IsValid.ShouldBeFalse();
		Should.Throw<BusinessException>(() => new EmployeePayrollDetails(Guid.NewGuid(), Guid.NewGuid())
			.SetSalaryGeneration(basis, 10000m, templateId, null, null, null, false));
	}
}
