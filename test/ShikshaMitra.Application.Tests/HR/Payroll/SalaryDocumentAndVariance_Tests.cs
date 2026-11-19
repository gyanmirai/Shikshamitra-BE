using System;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryDocumentAndVariance_Tests
{
	[Theory]
	[InlineData(true, 312)]
	[InlineData(false, -312)]
	public void Registration_number_change_alone_cannot_explain_a_saved_CTC_variance(bool registered, decimal delta)
	{
		var profile = new PayrollStatutoryProfile { VersionId = Guid.NewGuid() };
		var pf = PayrollSchemeApplication.Decide("PF", true, "7", true, 1145, 1145);
		var before = PayrollSchemeApplication.Decide("ESI", true, registered ? null : "123456", true, 72, 312);
		var after = PayrollSchemeApplication.Decide("ESI", true, registered ? "123456" : null, true, 72, 312);
		var saved = new SalaryTemplateCalculationDto { StatutoryApplication = new(profile, pf, before) };
		var current = new PayrollStatutoryApplication(profile, pf, after);
		before.NumberPresent.ShouldNotBe(after.NumberPresent);
		before.AppliedEmployeeAmount.ShouldBe(72m);
		after.AppliedEmployeeAmount.ShouldBe(72m);
		before.AppliedEmployerAmount.ShouldBe(312m);
		after.AppliedEmployerAmount.ShouldBe(312m);
		CompensationStatutoryVariance.IsExplained(saved, current, CompensationProtectionPolicy.ProtectEmployerCost, delta).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved, current, CompensationProtectionPolicy.ProtectEmployerCost, 0m).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved, current, CompensationProtectionPolicy.ProtectEmployerCost, delta + 1).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved, current with { EmployeeStateInsurance = after with { CalculatedEmployerAmount = 313 } },
			CompensationProtectionPolicy.ProtectEmployerCost, delta).ShouldBeFalse();
	}

	[Theory]
	[InlineData(true, CompensationProtectionPolicy.ProtectEmployerCost, 312)]
	[InlineData(false, CompensationProtectionPolicy.ProtectEmployerCost, -312)]
	[InlineData(true, CompensationProtectionPolicy.ProtectTakeHome, -72)]
	[InlineData(false, CompensationProtectionPolicy.ProtectTakeHome, 72)]
	public void Real_scheme_application_changes_explain_only_the_exact_protected_amount_delta(
		bool enabled, CompensationProtectionPolicy policy, decimal delta)
	{
		var profile = new PayrollStatutoryProfile { VersionId = Guid.NewGuid(), ApplyEsic = !enabled };
		var pf = PayrollSchemeApplication.Decide("PF", true, "7", true, 1145, 1145);
		var before = PayrollSchemeApplication.Decide("ESI", !enabled, null, true, 72, 312, "Synthetic prior enrollment");
		var after = PayrollSchemeApplication.Decide("ESI", enabled, null, true, 72, 312, "Synthetic changed enrollment");
		var saved = new SalaryTemplateCalculationDto { StatutoryApplication = new(profile, pf, before) };
		var current = new PayrollStatutoryApplication(profile with { VersionId = Guid.NewGuid(), ApplyEsic = enabled }, pf, after);
		CompensationStatutoryVariance.IsExplained(saved, current, policy, delta).ShouldBeTrue();
		CompensationStatutoryVariance.IsExplained(saved, current, policy, delta + 1m).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved, current, policy, 0m).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved,
			current with { EmployeeStateInsurance = after with { CalculatedEmployerAmount = 313m } }, policy, delta).ShouldBeFalse();
	}

	[Fact]
	public void PT_uses_frozen_semantics_even_when_the_school_renames_the_component()
	{
		var amounts = SalarySlipProjection.ReadStatutoryDeductions("""
			[{"Name":"School levy","Amount":200,"SemanticType":"PROFESSIONAL_TAX"},
			 {"Name":"PT","Amount":500,"SemanticType":"Ordinary"}, {"Name":"T.D.S.","Amount":100}]
			""");
		amounts["pt"].ShouldBe(200); amounts["tds"].ShouldBe(100);
	}

	[Theory]
	[InlineData("pf")]
	[InlineData("pt")]
	[InlineData("esic")]
	public void Legacy_custom_statutory_labels_are_unavailable_instead_of_fabricated_zero(string scheme)
	{
		var facts = new SalarySlipDto
		{
			ProvidentFundRuleId = scheme == "pf" ? Guid.NewGuid() : null,
			ProfessionalTaxRuleId = scheme == "pt" ? Guid.NewGuid() : null,
			EmployeeStateInsuranceRuleId = scheme == "esic" ? Guid.NewGuid() : null,
			Deductions = [new() { Name = "School levy", Amount = 200m }]
		};
		var row = PayrollSalaryDocumentRowReader.ToRow(new()
		{
			FactsJson = JsonSerializer.Serialize(facts), TotalEarnings = 15000m,
			TotalDeduction = 200m, NetPay = 14800m
		});
		row[scheme].ShouldBeNull();
		row["otherDeductions"].ShouldBeNull();
		row["totalDeduction"].ShouldBe("200.00");
		row["netPay"].ShouldBe("14800.00");
		facts.Deductions[0].Name = scheme switch { "pf" => "PF", "pt" => "Professional Tax", _ => "ESIC" };
		var identified = PayrollSalaryDocumentRowReader.ToRow(new()
		{
			FactsJson = JsonSerializer.Serialize(facts), TotalEarnings = 15000m,
			TotalDeduction = 200m, NetPay = 14800m
		});
		identified[scheme].ShouldBe("200.00");
		identified["otherDeductions"].ShouldBe("0.00");
	}

	[Theory]
	[InlineData("../other")]
	[InlineData("nested/abcdef")]
	[InlineData("00000000-0000-0000-0000-000000000000")]
	public void Logo_references_cannot_escape_the_tenant_blob_namespace(string suffix)
	{
		var tenant = Guid.NewGuid();
		Should.Throw<BusinessException>(() => new SalaryDocumentPresentation { LogoBlobName = $"payroll-logos/{tenant}/{suffix}" }.Validate(tenant));
		Should.Throw<BusinessException>(() => new SalaryDocumentPresentation { LogoBlobName = $"payroll-logos/{Guid.NewGuid()}/{Guid.NewGuid():N}" }.Validate(tenant));
	}

	[Fact]
	public void Logo_validation_rejects_an_image_with_a_valid_header_but_corrupt_pixels()
	{
		var bytes = new byte[24]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(bytes, 0); bytes[19] = 1; bytes[23] = 1;
		Should.Throw<BusinessException>(() => PayslipLogoService.ValidateImage("image/png", bytes));
		Should.Throw<BusinessException>(() => PayslipLogoService.ValidateImage("image/svg+xml", bytes));
	}

	[Fact]
	public void Presentation_rejects_unknown_columns_and_null_column_lists()
	{
		Should.Throw<BusinessException>(() => new SalaryDocumentPresentation { StatementColumns = ["privateTenantData"] }.Validate(Guid.NewGuid()));
		Should.Throw<BusinessException>(() => new SalaryDocumentPresentation { RegisterColumns = null! }.Validate(Guid.NewGuid()));
		new SalaryDocumentPresentation { Layout = "Compact", RegisterColumns = ["pf", "casualLeaveDays"] }.Validate(Guid.NewGuid());
	}
}
