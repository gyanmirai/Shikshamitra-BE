using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormInheritanceContextLayout_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void Context_only_sections_and_steps_keep_their_union_source_order_before_registration(bool stepper, bool sharedSourceStep)
	{
		var source = Form("Inquiry", stepper,
			Section("RELATIONSHIP", 0, Field("ROLE", "Inquiry.ParentRelationship", 4)),
			Section("ORIGIN", 1, Field("SOURCE", "Inquiry.Source", 3)),
			Section("STUDENT", 2, Field("CHILD", "Applicant.FirstName", 2)));
		if (sharedSourceStep)
			foreach (var section in source.Sections) { section.StepId = source.Sections[0].StepId; section.StepDisplayOrder = 0; }
		var registration = Form("Registration", stepper, Section("ADDITIONAL", 0, Field("EMAIL", "Guardian.Email", 0)));
		var sourceBefore = JsonSerializer.Serialize(source);
		var registrationBefore = JsonSerializer.Serialize(registration);
		var result = AdmissionFormInheritanceComposer.Compose(registration, source, new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = source.Id }] });
		result.Issues.ShouldBeEmpty();
		var role = result.InquiryContextSections.Single(section => section.Fields.Any(field => field.Code == "ROLE"));
		var origin = result.InquiryContextSections.Single(section => section.Fields.Any(field => field.Code == "SOURCE"));
		var student = result.Definition.Sections.Single(section => section.Fields.Any(field => field.Code == "CHILD"));
		var extra = result.Definition.Sections.Single(section => section.Fields.Any(field => field.Code == "EMAIL"));
		role.DisplayOrder.ShouldBe(0);
		origin.DisplayOrder.ShouldBe(1);
		student.DisplayOrder.ShouldBe(2);
		extra.DisplayOrder.ShouldBe(3);
		if (stepper)
		{
			role.StepDisplayOrder.ShouldBe(0);
			origin.StepDisplayOrder.ShouldBe(sharedSourceStep ? 0 : 1);
			student.StepDisplayOrder.ShouldBe(sharedSourceStep ? 0 : 2);
			extra.StepDisplayOrder.ShouldBe(sharedSourceStep ? 1 : 3);
		}
		JsonSerializer.Serialize(source).ShouldBe(sourceBefore);
		JsonSerializer.Serialize(registration).ShouldBe(registrationBefore);
	}

	[Fact]
	public void Split_context_and_editable_fields_share_section_layout_and_retain_field_order()
	{
		var source = Form("Inquiry", true, Section("MIXED", 0, Field("ROLE", "Inquiry.ParentRelationship", 5), Field("CHILD", "Applicant.FirstName", 8)));
		source.FieldsPerRow = 3;
		var registration = Form("Registration", true, Section("ADDITIONAL", 0, Field("EMAIL", "Guardian.Email", 0)));
		var result = AdmissionFormInheritanceComposer.Compose(registration, source, new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = source.Id }] });
		result.Issues.ShouldBeEmpty();
		var context = result.InquiryContextSections.Single();
		var editable = result.Definition.Sections.Single(section => section.Fields.Any(field => field.Code == "CHILD"));
		context.Id.ShouldBe(editable.Id);
		context.DisplayOrder.ShouldBe(editable.DisplayOrder);
		context.StepId.ShouldBe(editable.StepId);
		context.StepDisplayOrder.ShouldBe(editable.StepDisplayOrder);
		context.FieldsPerRow.ShouldBe(3);
		context.Fields.Single().DisplayOrder.ShouldBe(5);
		context.Fields.Single().ReadOnly.ShouldBeTrue();
		editable.Fields.Single().DisplayOrder.ShouldBe(8);
	}

	private static AdmissionFormSchema Form(string name, bool stepper, params AdmissionFormSchemaSection[] sections)
	{
		var form = new AdmissionFormSchema { Id = Guid.NewGuid(), Name = name, LayoutType = stepper ? AdmissionFormLayoutType.Stepper : AdmissionFormLayoutType.SinglePage, Sections = sections.ToList() };
		foreach (var section in sections)
		{
			section.FormDefinitionId = form.Id;
			foreach (var field in section.Fields) { field.FormDefinitionId = form.Id; field.SectionId = section.Id; }
		}
		return form;
	}
	private static AdmissionFormSchemaSection Section(string code, int order, params AdmissionFormSchemaField[] fields) => new()
	{
		Id = Guid.NewGuid(), Code = code, Name = code, DisplayOrder = order,
		StepId = Guid.NewGuid(), StepDisplayOrder = order, StepName = code, Fields = fields.ToList()
	};
	private static AdmissionFormSchemaField Field(string code, string binding, int order) => new()
	{
		Id = Guid.NewGuid(), Code = code, Label = code, BindingType = AdmissionFormBindingType.System,
		BindingKey = binding, FieldType = AdmissionFormFieldType.Text, DisplayOrder = order, Required = true
	};
}
