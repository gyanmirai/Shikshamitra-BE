using System;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryDocumentAndVariance_Tests
{
	[Theory]
	[InlineData(true, 312)]
	[InlineData(false, -312)]
	public void Only_the_exact_registration_delta_explains_a_saved_CTC_variance(bool registered, decimal delta)
	{
		var profile = new PayrollStatutoryProfile { VersionId = Guid.NewGuid() };
		var pf = PayrollSchemeApplication.Decide("PF", true, "7", true, 1145, 1145);
		var before = PayrollSchemeApplication.Decide("ESI", true, registered ? null : "123456", true, 72, 312);
		var after = PayrollSchemeApplication.Decide("ESI", true, registered ? "123456" : null, true, 72, 312);
		var saved = new SalaryTemplateCalculationDto { StatutoryApplication = new(profile, pf, before) };
		var current = new PayrollStatutoryApplication(profile, pf, after);
		CompensationStatutoryVariance.IsExplained(saved, current, CompensationProtectionPolicy.ProtectEmployerCost, delta).ShouldBeTrue();
		CompensationStatutoryVariance.IsExplained(saved, current, CompensationProtectionPolicy.ProtectEmployerCost, delta + 1).ShouldBeFalse();
		CompensationStatutoryVariance.IsExplained(saved, current with { EmployeeStateInsurance = after with { CalculatedEmployerAmount = 313 } },
			CompensationProtectionPolicy.ProtectEmployerCost, delta).ShouldBeFalse();
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
