using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Public_request_key_replays_exact_payload_and_rejects_changed_payload_without_losing_another_child() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(service, offering);
		var key = Guid.NewGuid();
		SubmitPublicAdmissionInquiryDto Keyed(string child, Guid requestId)
		{
			var node = JsonSerializer.SerializeToNode(PublicSiblingInput(offering, child))!;
			node["RequestId"] = requestId;
			return node.Deserialize<SubmitPublicAdmissionInquiryDto>()!;
		}
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			await service.SubmitPublicAsync(link.Token, Keyed("First child", key));
			var retry = Keyed("First child", key);
			retry.FormValues.Reverse();
			retry.FormValues.Single(value => value.FieldCode == "EMAIL").ValueJson = "\" AMIT@EXAMPLE.COM \"";
			await service.SubmitPublicAsync(link.Token, retry);
			var conflict = await Should.ThrowAsync<BusinessException>(() => service.SubmitPublicAsync(link.Token, Keyed("Changed child", key)));
			conflict.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
			await service.SubmitPublicAsync(link.Token, Keyed("Second child", Guid.NewGuid()));
			// A new explicit request key is distinct intent, even for coincidentally identical details.
			await service.SubmitPublicAsync(link.Token, Keyed("Second child", Guid.NewGuid()));
		}
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().CountAsync(item => item.AdmissionOfferingId == offering.Id))).ShouldBe(3);
	});

	[Fact]
	public Task Public_sibling_projection_hides_only_owned_questions_and_discards_forged_answers() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		await AddPublicSiblingQuestionsAsync(offering);
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(service, offering);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			var context = await service.GetPublicAsync(link.Token, offering.Id);
			var fields = context.Form!.Sections.SelectMany(section => section.Fields).ToList();
			fields.ShouldNotContain(field => new[] { "SINGLE_CHILD", "AT_SCHOOL", "SIBLING_STUDENT", "DYNAMIC_SIBLINGS" }.Contains(field.Code));
			fields.ShouldContain(field => field.Code == "SINGLE_PARENT");
			fields.ShouldContain(field => field.Code == "CUSTOM_SIBLING_LABEL");
			var input = PublicSiblingInput(offering, "First child");
			input.FormValues.Add(new() { FieldCode = "SIBLING_STUDENT", ValueJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }) });
			input.FormValues.Add(new() { FieldCode = "SINGLE_CHILD", ValueJson = "true" });
			await service.SubmitPublicAsync(link.Token, input);
		}
		var saved = (await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item => item.AdmissionOfferingId == offering.Id)).Single();
		saved.FormResponseSnapshotJson.ShouldNotBeNull().ShouldNotContain("SiblingStudentIds");
		saved.FormResponseSnapshotJson.ShouldNotContain("IsSingleChild");
		AdmissionSiblingParentSelection.ReadConfirmation(await _applicantRepository.GetAsync(saved.ApplicantId!.Value)).ShouldBeNull();
		(await _formFieldRepository.GetListAsync(field => field.FormDefinitionId == offering.InquiryFormDefinitionId)).ShouldContain(field => field.Code == "SIBLING_STUDENT" && field.Required);
	});

	[Fact]
	public Task Public_legacy_exact_retry_retains_a_different_child_with_the_same_phone_without_duplicate_consent() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(service, offering);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "First child"));
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "First child"));
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "Second child"));
		}
		var inquiries = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item => item.AdmissionOfferingId == offering.Id);
		inquiries.Count.ShouldBe(2);
		inquiries.Select(item => item.FamilyId).Distinct().Count().ShouldBe(2);
		inquiries.Select(item => item.ApplicantId).Distinct().Count().ShouldBe(2);
		foreach (var inquiry in inquiries)
		{
			inquiry.Status.ShouldBe(AdmissionInquiryStatus.New);
			AdmissionSiblingParentSelection.ReadConfirmation(await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value)).ShouldBeNull();
			var activity = await GetRequiredService<IRepository<AdmissionActivity, Guid>>().GetListAsync(item => item.InquiryId == inquiry.Id);
			activity.Count(item => item.ActivityType == "PublicInquiryConsent").ShouldBe(1);
			activity.Count(item => item.ActivityType == "InquiryCreated").ShouldBe(1);
			inquiry.Source.ShouldBe(AdmissionInquirySource.QrCode);
			inquiry.SourceDetail.ShouldBe("School QR");
			JsonSerializer.Serialize(inquiry.ExtraProperties).ShouldNotContain(link.Token);
			(await _familyRepository.GetAsync(inquiry.FamilyId!.Value)).GetProperty<string>("AdmissionPublicCaptureTrust").ShouldBe("unconfirmed");
		}
		var recipients = inquiries.Select(item => item.ApplicantId!.Value).ToArray();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ConsentRecord, Guid>>().CountAsync(item => item.RecipientId.HasValue && Enumerable.Contains(recipients, item.RecipientId.Value)))).ShouldBe(2);
		var inquiryIds = inquiries.Select(item => item.Id).ToArray();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<CommunicationCampaign, Guid>>().CountAsync(item => item.SourceEntityId.HasValue && Enumerable.Contains(inquiryIds, item.SourceEntityId.Value)))).ShouldBe(0);
		var staffInput = new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = PublicSiblingInput(offering, "Staff child").FormValues
		};
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(staffInput))).Code.ShouldBe(AdmissionErrorCodes.InquiryDuplicate);
	});

	[Fact]
	public Task Public_replay_still_denies_expired_revoked_and_other_offering_links() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var otherOffering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(service, offering);
		var expiredToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().InsertAsync(new(
			Guid.NewGuid(), TestTenantId, offering.AdmissionCycleId, null, "Expired fixture",
			Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expiredToken))), AdmissionInquirySource.QrCode, null,
			DateTime.UtcNow.AddMinutes(-1), offering.InquiryFormDefinitionId), true));
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitPublicAsync(link.Token, PublicSiblingInput(otherOffering, "Other child")))).Code.ShouldBe(AdmissionApplicationErrorCodes.OfferingNotAvailable);
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitPublicAsync(expiredToken, PublicSiblingInput(offering, "Expired child")))).Code.ShouldBe("Admissions.PublicInquiry.LinkUnavailable");
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "Accepted child"));
		}
		await service.RevokeLinkAsync(link.Id, link.ConcurrencyStamp);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "Accepted child")))).Code.ShouldBe("Admissions.PublicInquiry.LinkUnavailable");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().CountAsync(item => item.AdmissionOfferingId == offering.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Public_source_projection_survives_fresh_registration_inheritance() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateInheritedRegistrationFixtureAsync();
		await AddPublicSiblingQuestionsAsync(offering);
		await WithUnitOfWorkAsync(async () =>
		{
			var registrationId = offering.ApplicationFormDefinitionId!.Value;
			var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == registrationId)).First();
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), registrationId, section.Id, "STAFF_ONLY_SINGLE", "Staff configured single child",
				AdmissionFormFieldType.Checkbox, AdmissionFormBindingType.System, "Family.IsSingleChild", true, false, 20, null, null, null, null, null), true);
		});
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(service, offering);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "Registration child"));
		var inquiry = (await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item => item.AdmissionOfferingId == offering.Id)).Single();
		await MarkInheritedInquiryInterestedAsync(inquiry.Id);
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		var runtimeFields = form.Definition.Sections.Concat(form.InquiryContextSections).SelectMany(section => section.Fields).ToList();
		runtimeFields.ShouldNotContain(field => new[] { "SINGLE_CHILD", "AT_SCHOOL", "SIBLING_STUDENT", "DYNAMIC_SIBLINGS" }.Contains(field.InheritedFrom == null ? field.Code : field.InheritedFrom.SourceFieldCode));
		runtimeFields.ShouldContain(field => field.Code == "STAFF_ONLY_SINGLE" && field.Required);
	});

	private Task<AdmissionPublicInquiryLinkCreatedDto> CreatePublicSiblingLinkAsync(IAdmissionPublicInquiryAppService service, AdmissionOffering offering) =>
		service.CreateLinkAsync(new() { AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId, Name = "Sibling public fixture", Source = AdmissionInquirySource.QrCode, SourceDetail = "School QR" });

	private static SubmitPublicAdmissionInquiryDto PublicSiblingInput(AdmissionOffering offering, string child) => new()
	{
		AdmissionOfferingId = offering.Id, BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
		ConsentToContact = true, FormValues =
		[
			new() { FieldCode = "CHILD", ValueJson = JsonSerializer.Serialize(child) },
			new() { FieldCode = "ADULT", ValueJson = "\"Amit Shah\"" },
			new() { FieldCode = "PHONE", ValueJson = "\"9876543210\"" },
			new() { FieldCode = "EMAIL", ValueJson = "\"amit@example.com\"" }
		]
	};

	private Task AddPublicSiblingQuestionsAsync(AdmissionOffering offering) => WithUnitOfWorkAsync(async () =>
	{
		var id = offering.InquiryFormDefinitionId!.Value;
		var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == id)).First();
		foreach (var (code, binding, type, options) in new[]
		{
			("SINGLE_CHILD", "Family.IsSingleChild", AdmissionFormFieldType.Checkbox, (string?)null),
			("AT_SCHOOL", "Family.SiblingAtSchool", AdmissionFormFieldType.Checkbox, null),
			("SIBLING_STUDENT", "Family.SiblingStudentIds", AdmissionFormFieldType.MultiSelect, "{\"source\":\"StudentSiblings\"}"),
			("DYNAMIC_SIBLINGS", (string?)null, AdmissionFormFieldType.MultiSelect, "{\"source\":\"StudentSiblings\"}"),
			("SINGLE_PARENT", "Family.IsSingleParentChild", AdmissionFormFieldType.Checkbox, null),
			("CUSTOM_SIBLING_LABEL", (string?)null, AdmissionFormFieldType.Text, null)
		})
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), id, section.Id, code, "Sibling custom label", type,
				binding == null ? AdmissionFormBindingType.Custom : AdmissionFormBindingType.System, binding,
				code is "SIBLING_STUDENT" or "SINGLE_CHILD", false, 10, null, null, null, null, null, options), true);
		await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), id, section.Id, "OTHER_FLAG", "Other flag", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, null, false, false, 11, null, null, null, null, null), false);
		await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), id, section.Id, "SIBLING_DETAILS", "Conditional details", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, null, true, false, 12, null, null, null, null,
			"{\"operator\":\"ANY\",\"conditions\":[{\"field\":\"AT_SCHOOL\",\"operator\":\"equals\",\"value\":true},{\"field\":\"OTHER_FLAG\",\"operator\":\"equals\",\"value\":\"yes\"}]}"), false);
		await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), id, section.Id, "CONDITIONAL_REQUIRED", "Conditionally required", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, null, false, false, 13, null, null, null, null, null, null,
			"{\"field\":\"AT_SCHOOL\",\"operator\":\"equals\",\"value\":true}"), false);
		foreach (var step in new[] { false, true })
		{
			const string rule = "{\"field\":\"AT_SCHOOL\",\"operator\":\"equals\",\"value\":true}";
			var hidden = new AdmissionFormSectionDefinition(Guid.NewGuid(), id, step ? "HIDDEN_STEP" : "HIDDEN_SECTION", "Conditional sibling details", 20,
				step ? null : rule, stepVisibilityRuleJson: step ? rule : null);
			await _formSectionRepository.InsertAsync(hidden, false);
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), id, hidden.Id, step ? "STEP_DETAIL" : "SECTION_DETAIL", "Hidden required detail", AdmissionFormFieldType.Text,
				AdmissionFormBindingType.Custom, null, true, false, 1, null, null, null, null, null), true);
		}
	});
}
