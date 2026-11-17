using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Inquiry_update_shared_parent_mobile_preserves_family_until_existing_sibling_confirmation(bool configuredForm) => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			target.UpdateDetails(target.ApplicantName, "9876543212", target.Email, target.Source, target.SourceDetail, target.PreferredLanguageCode);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(target, true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var current = await service.GetAsync(target.Id);
		var update = SiblingContactUpdate(current, source.NormalizedPhone);
		if (configuredForm) update.FormValues = PublicSiblingInput(offering, target.ApplicantName).FormValues;
		var saved = await service.UpdateAsync(target.Id, update);
		saved.NormalizedPhone.ShouldBe(source.NormalizedPhone);
		saved.FamilyId.ShouldBe(target.FamilyId); // A contact match is not consent to merge families.
		var confirmed = await service.ConfirmFamilyAsync(saved.Id, new ConfirmAdmissionInquiryFamilyDto
		{
			FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId!.Value,
			ExistingParentApplicantId = source.ApplicantId!.Value, ConcurrencyStamp = saved.ConcurrencyStamp
		});
		confirmed.FamilyId.ShouldBe(source.FamilyId);
		var edited = await service.UpdateAsync(confirmed.Id, SiblingContactUpdate(confirmed, source.NormalizedPhone));
		edited.FamilyId.ShouldBe(source.FamilyId);
		(await service.GetAsync(source.Id)).ApplicantName.ShouldBe(source.ApplicantName);
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(saved.Id, update))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Inquiry_update_blocks_same_student_even_when_mobile_does_not_change(bool missingBirthDate) => RunCertificationScenarioAsync(async () =>
	{
		var (_, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
			{
				foreach (var id in new[] { source.ApplicantId!.Value, target.ApplicantId!.Value })
				{
					var applicant = await _applicantRepository.GetAsync(id);
					applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, missingBirthDate && id == target.ApplicantId ? null : new DateTime(2015, 5, 15), applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
					await _applicantRepository.UpdateAsync(applicant, true);
				}
			});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var current = await service.GetAsync(target.Id);
		var update = SiblingContactUpdate(current, source.NormalizedPhone);
		update.ApplicantName = "  OLDER   pending CHILD  ";
		var error = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(current.Id, update));
		error.Code.ShouldBe("Admissions.InquiryDuplicateStudent");
	});

	private static UpdateAdmissionInquiryDto SiblingContactUpdate(AdmissionInquiryDto inquiry, string phone) => new()
	{
		ApplicantName = inquiry.ApplicantName, Phone = phone, Email = inquiry.Email,
		Source = inquiry.Source, SourceDetail = inquiry.SourceDetail, PreferredLanguageCode = inquiry.PreferredLanguageCode,
		ConcurrencyStamp = inquiry.ConcurrencyStamp
	};

	[Fact]
	public Task Inquiry_update_allows_same_name_with_distinct_known_birth_dates() => RunCertificationScenarioAsync(async () =>
	{
		var (_, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var id in new[] { source.ApplicantId!.Value, target.ApplicantId!.Value })
			{
				var applicant = await _applicantRepository.GetAsync(id);
				applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, new DateTime(id == source.ApplicantId ? 2015 : 2017, 5, 15), applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
				await _applicantRepository.UpdateAsync(applicant, true);
			}
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var current = await service.GetAsync(target.Id);
		var update = SiblingContactUpdate(current, source.NormalizedPhone);
		update.ApplicantName = source.ApplicantName;
		(await service.UpdateAsync(current.Id, update)).ApplicantName.ShouldBe(source.ApplicantName);
	});
}
