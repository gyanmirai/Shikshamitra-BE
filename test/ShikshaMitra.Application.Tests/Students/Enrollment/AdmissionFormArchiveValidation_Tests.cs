using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormArchiveValidation_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Archive_impact_hides_dependencies_and_record_counts_without_related_view_access(bool hasRestrictedOfferings)
	{
		var impact = new AdmissionFormArchiveImpactDto
		{
			OfferingCount = 1, WorkflowCount = 1, PublicLinkCount = 1, InquiryCount = 10, ApplicationCount = 5,
			HasRestrictedOfferings = hasRestrictedOfferings,
			Dependencies =
			[
				new() { Kind = "offering", Id = Guid.NewGuid(), Name = "Restricted offering" },
				new() { Kind = "workflow", Id = Guid.NewGuid(), Name = "Restricted workflow" },
				new() { Kind = "publicLink", Id = Guid.NewGuid(), Name = "Restricted link" },
				new() { Kind = "registrationForm", Id = Guid.NewGuid(), Name = "Restricted form" }
			]
		};
		AdmissionFormArchiveImpactService.ApplyVisibility(impact, false, false, false, false, false);
		impact.Dependencies.ShouldBeEmpty();
		impact.OfferingCount.ShouldBe(0);
		impact.WorkflowCount.ShouldBe(0);
		impact.PublicLinkCount.ShouldBe(0);
		impact.InquiryCount.ShouldBe(0);
		impact.ApplicationCount.ShouldBe(0);
		impact.HasRestrictedOfferings.ShouldBeTrue();
		impact.HasRestrictedDependencies.ShouldBeTrue();
	}

	[Fact]
	public void Offering_configuration_failure_exposes_structured_actionable_errors()
	{
		var error = new AdmissionOfferingValidationError(AdmissionOfferingValidationCodes.InquiryFormNotPublished,
			"Inquiry Form 2026 is archived. Restore it or select another published inquiry form.");
		var exception = Should.Throw<BusinessException>(() =>
			AdmissionOfferingConfigurationValidator.EnsureCanSave(new([error]), true));
		exception.Data["ConfigurationErrors"].ShouldNotBeNull();
		exception.Data["ValidationMessages"].ShouldBeOfType<string[]>().ShouldContain(error.Message);
	}
}
