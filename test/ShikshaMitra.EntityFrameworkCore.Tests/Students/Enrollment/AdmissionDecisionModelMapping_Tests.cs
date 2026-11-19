using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionDecisionModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Offer_expiry_maps_through_utc_offset_without_changing_the_instant()
	{
		var property = GetRequiredService<ShikshaMitraDbContext>().Model
			.FindEntityType(typeof(AdmissionApplication))!
			.FindProperty(nameof(AdmissionApplication.OfferExpiresAt))!;
		var converter = property.GetValueConverter();

		converter.ShouldNotBeNull();
		converter.ProviderClrType.ShouldBe(typeof(DateTimeOffset));
		var expiry = new DateTime(2026, 9, 30, 11, 30, 0, DateTimeKind.Utc);
		var provider = (DateTimeOffset)converter.ConvertToProvider(expiry)!;
		provider.Offset.ShouldBe(TimeSpan.Zero);
		provider.UtcDateTime.ShouldBe(expiry);
		((DateTime)converter.ConvertFromProvider(provider)!).ShouldBe(expiry);
	}

	[Fact]
	public void Decision_and_enrollment_models_have_auditable_indexes_and_links()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var application = model.FindEntityType(typeof(AdmissionApplication));
		var history = model.FindEntityType(typeof(AdmissionApplicationDecisionHistory));
		var enrollment = model.FindEntityType(typeof(StudentEnrollment));

		application.ShouldNotBeNull();
		history.ShouldNotBeNull();
		enrollment.ShouldNotBeNull();
		application.FindProperty(nameof(AdmissionApplication.DecisionReason)).ShouldNotBeNull();
		application.FindProperty(nameof(AdmissionApplication.StudentEnrollmentId)).ShouldNotBeNull();
		history.GetIndexes().ShouldContain(index =>
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "ApplicationId", "OccurredAt" }));
		history.GetForeignKeys().ShouldContain(key =>
			key.Properties.Single().Name == nameof(AdmissionApplicationDecisionHistory.ApplicationId) &&
			key.DeleteBehavior == DeleteBehavior.Cascade);
		enrollment.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "StudentId", "AcademicYearId" }));
		enrollment.GetForeignKeys().ShouldContain(key =>
			key.Properties.Single().Name == nameof(StudentEnrollment.StudentId) &&
			key.DeleteBehavior == DeleteBehavior.Restrict);
	}

	[Fact]
	public void Onboarding_models_enforce_case_seat_roll_and_identifier_uniqueness()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var onboardingCase = model.FindEntityType(typeof(StudentOnboardingCase));
		var reservation = model.FindEntityType(typeof(StudentSeatReservation));
		var allocation = model.FindEntityType(typeof(StudentIdentifierAllocation));
		var policy = model.FindEntityType(typeof(StudentOnboardingPolicy));
		var settings = model.FindEntityType(typeof(StudentOnboardingSettings));

		onboardingCase.ShouldNotBeNull();
		onboardingCase.FindProperty(nameof(StudentOnboardingCase.DueAt)).ShouldNotBeNull();
		onboardingCase.FindProperty(nameof(StudentOnboardingCase.GuardianContactVerifiedAt))!.IsNullable.ShouldBeTrue();
		onboardingCase.FindProperty(nameof(StudentOnboardingCase.IsHistoricalBackfill))!.IsNullable.ShouldBeFalse();
		onboardingCase.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "ApplicationId" }));
		onboardingCase.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "AcademicYearId", "DivisionId", "RollNumber" }) &&
			index.GetFilter()!.Contains("Status"));
		reservation!.GetIndexes().ShouldContain(index => index.IsUnique && index.GetFilter()!.Contains("Status"));
		allocation!.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "IdentifierType", "Value" }));
		policy!.GetIndexes().Count(index => index.IsUnique).ShouldBe(2);
		settings.ShouldNotBeNull();
		settings.FindProperty(nameof(StudentOnboardingSettings.ConfigurationJson))!.GetColumnType()
			.ShouldBe("text");
		settings.GetIndexes().Count(index => index.IsUnique).ShouldBe(2);
	}
}
