using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionOfferingFormDefinitionReader_Tests
{
	[Theory]
	[InlineData("AdmissionFormInheritanceV1")]
	[InlineData("AdmissionFormFieldIdentitiesV1")]
	public void Invalid_form_metadata_is_isolated_from_other_offering_definitions(string property)
	{
		var invalid = Form("INVALID");
		invalid.SetProperty(property, "{invalid-json");
		var sectionId = Guid.NewGuid();
		var fields = new List<AdmissionFormFieldDefinition>
		{
			new(Guid.NewGuid(), invalid.Id, sectionId, "CUSTOM", "Question", AdmissionFormFieldType.Text,
				AdmissionFormBindingType.Custom, "CUSTOM", false, false, 0, null, null, null, null, null)
		};
		AdmissionOfferingFormDefinitionReader.Read(invalid, [new(sectionId, invalid.Id, "DETAILS", "Details", 0, null)], fields).ShouldBeNull();
		var valid = Form("VALID");
		AdmissionOfferingFormDefinitionReader.Read(valid, [], [])!.Id.ShouldBe(valid.Id);
	}

	private static AdmissionFormDefinition Form(string code) =>
		new(Guid.NewGuid(), Guid.NewGuid(), code, code, AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Registration);
}
