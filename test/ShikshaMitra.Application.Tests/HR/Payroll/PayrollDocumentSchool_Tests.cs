using System;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.TenantManagement;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollDocumentSchool_Tests
{
	private sealed class TestTenant(Guid id, string name) : Tenant(id, name, name.ToUpperInvariant());
	[Fact]
	public void Uses_the_institute_name_and_complete_address_instead_of_the_school_code()
	{
		var tenant = new TestTenant(Guid.NewGuid(), "SCHOOL-CODE");
		tenant.SetProperty("InstituteName", "  Institute School  ");
		tenant.SetProperty("Address1", "School Road");
		tenant.SetProperty("Address2", "  ");
		tenant.SetProperty("City", new { name = "Ahmedabad" });
		tenant.SetProperty("State", "Gujarat");
		tenant.SetProperty("PostalCode", "380001");
		tenant.SetProperty("Country", "India");
		var result = PayrollDocumentSchoolService.FromTenant(tenant);
		result.Name.ShouldBe("Institute School");
		result.Address.ShouldBe("School Road, Ahmedabad, Gujarat, 380001, India");
	}

	[Fact]
	public void A_country_or_city_alone_does_not_satisfy_the_school_address_requirement()
	{
		var tenant = new TestTenant(Guid.NewGuid(), "School");
		tenant.SetProperty("Country", "India");
		tenant.SetProperty("City", "Ahmedabad");
		PayrollDocumentSchoolService.FromTenant(tenant).Address.ShouldBeEmpty();
	}

}

