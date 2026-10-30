using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inherited_registration_uses_exact_source_label_and_retains_saved_clears() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateInheritedRegistrationFixtureAsync();
		var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
		var answers = ConfiguredInquiryAnswers();
		answers.Add(new() { FieldCode = "SOURCE_DOB", ValueJson = JsonSerializer.Serialize("2019-05-14") });
		var inquiry = await inquiryService.CreateAsync(new()
		{
			AdmissionCycleId = fixture.AdmissionCycleId, AdmissionOfferingId = fixture.Id,
			BoardId = fixture.BoardId, MediumId = fixture.MediumId, StandardId = fixture.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = answers
		});
		await MarkInheritedInquiryInterestedAsync(inquiry.Id);
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Id, inquiry.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		var birth = form.Definition.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Applicant.DateOfBirth");
		birth.Label.ShouldBe("DOB");
		birth.InheritedFrom!.InquiryFormDefinitionId.ShouldBe(fixture.InquiryFormDefinitionId!.Value);
		form.Values.Single(value => value.FieldCode == birth.Code).ValueJson.ShouldBe(JsonSerializer.Serialize("2019-05-14"));

		await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
			Values = [new() { FieldCode = birth.Code, ValueJson = "null" }]
		});
		var reloaded = await _applicationAppService.GetFormAsync(application.Id);
		reloaded.Values.Single(value => value.FieldCode == birth.Code).ValueJson.ShouldBe("null");
		reloaded.Definition.Sections.SelectMany(section => section.Fields).Single(field => field.Code == birth.Code).Label.ShouldBe("DOB");
		(await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, fixture.Id, inquiry.Id)).Id.ShouldBe(application.Id);
		(await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().GetListAsync(instance => instance.ApplicationId == application.Id)).Count.ShouldBe(1);
	});

	[Fact]
	public Task Inherited_registration_requiredness_uses_effective_fields_and_rejects_unknown_writes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateInheritedRegistrationFixtureAsync(requiredInRegistration: true);
		var inquiry = await GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(new()
		{
			AdmissionCycleId = fixture.AdmissionCycleId, AdmissionOfferingId = fixture.Id,
			BoardId = fixture.BoardId, MediumId = fixture.MediumId, StandardId = fixture.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = ConfiguredInquiryAnswers()
		});
		await MarkInheritedInquiryInterestedAsync(inquiry.Id);
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Id, inquiry.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		var birth = form.Definition.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Applicant.DateOfBirth");
		birth.Required.ShouldBeTrue();
		(await _applicationAppService.SubmitAsync(application.Id)).FieldErrors.ShouldContainKey(birth.Code);
		var stamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp;
		var unknown = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = stamp,
			Values = [new() { FieldCode = "OTHER_INQUIRY_QUESTION", ValueJson = "\"Forged\"" }]
		}));
		unknown.Code.ShouldBe(AdmissionApplicationErrorCodes.UnknownFormField);
	});

	private async Task<AdmissionOffering> CreateInheritedRegistrationFixtureAsync(bool requiredInRegistration = false)
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(requiredIdentity: false);
		await WithUnitOfWorkAsync(async () =>
		{
			var sourceId = offering.InquiryFormDefinitionId!.Value;
			var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == sourceId)).First();
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), sourceId, section.Id, "SOURCE_DOB", "DOB",
				AdmissionFormFieldType.Date, AdmissionFormBindingType.System, "Applicant.DateOfBirth", false, false, 10,
				null, null, null, null, null), false);
			var registration = await _formRepository.GetAsync(offering.ApplicationFormDefinitionId!.Value);
			AdmissionFormInheritanceMetadataStore.Write(registration, new()
			{
				Purpose = AdmissionFormPurpose.Registration,
				Inheritance = new()
				{
					Enabled = true,
					Sources = [new()
					{
						InquiryFormDefinitionId = sourceId,
						RequiredFieldIdentities = requiredInRegistration ? ["system:Applicant.DateOfBirth"] : []
					}]
				}
			});
			await _formRepository.UpdateAsync(registration, true);
		});
		return offering;
	}

	private Task MarkInheritedInquiryInterestedAsync(Guid inquiryId) => WithUnitOfWorkAsync(async () =>
	{
		var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		var inquiry = await inquiries.GetAsync(inquiryId);
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		await inquiries.UpdateAsync(inquiry, true);
	});
}
