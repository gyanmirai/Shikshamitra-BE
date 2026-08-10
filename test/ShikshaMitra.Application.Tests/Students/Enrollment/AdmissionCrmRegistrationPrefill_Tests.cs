using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmRegistrationPrefill_Tests
{
	[Fact]
	public void Should_Prefer_Immutable_Route_Name_Snapshot()
	{
		var definitionId = Guid.NewGuid();
		var lead = CreateLead(
			AdmissionCrmRouteType.Fresh,
			definitionId,
			3,
			"Fresh admission 2026");

		var prefill = AdmissionCrmRegistrationModelMapper.BuildPrefill(lead);

		prefill["admissionRoute"].ShouldBe("Fresh admission 2026");
		prefill["admissionRouteDefinitionId"].ShouldBe(definitionId);
		prefill["admissionRouteDefinitionVersion"].ShouldBe(3);
	}

	[Fact]
	public void Should_Fall_Back_To_Legacy_Route_Type_When_Snapshot_Is_Missing()
	{
		var lead = CreateLead(AdmissionCrmRouteType.SiblingPriority);

		var prefill = AdmissionCrmRegistrationModelMapper.BuildPrefill(lead);

		prefill["admissionRoute"].ShouldBe(nameof(AdmissionCrmRouteType.SiblingPriority));
		prefill["admissionRouteDefinitionId"].ShouldBeNull();
		prefill["admissionRouteDefinitionVersion"].ShouldBeNull();
	}

	[Fact]
	public void Should_Match_Camel_Case_Prefill_To_Pascal_Case_System_Fields()
	{
		var now = DateTime.UtcNow;
		var lead = CreateLead(AdmissionCrmRouteType.Fresh);
		var template = new AdmissionCrmRegistrationTemplateDto
		{
			Id = Guid.NewGuid(),
			AcademicYearId = Guid.NewGuid(),
			Name = "Registration",
			Version = 1,
			Sections =
			[
				new AdmissionCrmRegistrationSectionDto
				{
					Key = "student",
					Title = "Student details",
					Fields =
					[
						new AdmissionCrmRegistrationFieldDto
						{
							Key = "firstName",
							Label = "First name",
							SystemField = "FirstName",
							Required = true
						},
						new AdmissionCrmRegistrationFieldDto
						{
							Key = "parentName",
							Label = "Parent name",
							SystemField = "ParentGuardianName",
							Required = true
						},
						new AdmissionCrmRegistrationFieldDto
						{
							Key = "phone",
							Label = "Phone",
							SystemField = "PrimaryContactNo",
							FieldType = AdmissionCrmFormFieldType.Phone,
							Required = true
						}
					]
				}
			]
		};
		var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var application = new AdmissionCrmRegistrationApplication(
			Guid.NewGuid(),
			null,
			template.AcademicYearId,
			lead.Id,
			template.Id,
			template.Version,
			null,
			"REG-00001",
			JsonSerializer.Serialize(template, jsonOptions),
			JsonSerializer.Serialize(AdmissionCrmRegistrationModelMapper.BuildPrefill(lead), jsonOptions),
			now,
			now.AddDays(14),
			0);

		var completion = AdmissionCrmRegistrationResponseValidator.Validate(
			application,
			template,
			new Dictionary<string, JsonElement>(),
			requireComplete: true,
			allowStaffManagedFields: true);

		completion.ShouldBe(100);
	}

	private static AdmissionInquiryLead CreateLead(
		AdmissionCrmRouteType? routeType,
		Guid? routeDefinitionId = null,
		int? routeDefinitionVersion = null,
		string? routeNameSnapshot = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			null,
			null,
			AdmissionInquiryChannel.ManualBackOffice,
			null,
			null,
			null,
			"Legacy",
			null,
			"Applicant",
			null,
			"Parent",
			"9876543210",
			null,
			"Admission inquiry",
			null,
			"Grade 1",
			null,
			DateTimeOffset.UtcNow,
			routeType,
			routeDefinitionId,
			routeDefinitionVersion,
			routeNameSnapshot);
}
