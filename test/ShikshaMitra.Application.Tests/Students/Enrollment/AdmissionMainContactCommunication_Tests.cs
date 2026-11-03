using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using ShikshaMitra.Students.ValueObjects;
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
	public Task Main_school_contact_choice_preserves_non_parent_audience_mode(bool includeStudents) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "DirectMessage", "Student");
		await WithUnitOfWorkAsync(async () =>
		{
			var current = (await _studentRepository.GetWithDetailsAsync(student.Id))!;
			current.UpdateFamilyInfo(ParentDetails("Father", "father@example.com"), null,
				ParentDetails("Aunt", "aunt@example.com"), []);
			StudentFamilyContactSelection.Select(current, "guardian");
			current.ContactDetail!.UpdateContactInfo("9876543299", null, "student@example.com", null, null, null, null, null, null, null);
			await _studentRepository.UpdateAsync(current, true);
		});
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<ICommunicationAudienceResolver>().ResolveAsync(Guid.NewGuid(), new()
		{
			StudentIds = [student.Id], ParentsFirst = false, IncludeStudentsWhenEligible = includeStudents
		}));
		result.Recipients.ShouldNotBeEmpty();
		result.Recipients.ShouldAllBe(recipient => recipient.Email == "student@example.com" && recipient.MobileNumber == "9876543299");
	});

	[Theory]
	[InlineData("father", "father@example.com", "9876543210")]
	[InlineData("guardian", null, "9876543212")]
	[InlineData(null, "father@example.com,mother@example.com", null)]
	public Task Main_school_parent_audience_respects_explicit_contact_and_keeps_optional_guardian_out_of_parent_defaults(string? selected, string? expectedEmails, string? expectedMobile) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "MessageContact", "Student");
		await WithUnitOfWorkAsync(async () =>
		{
			var current = (await _studentRepository.GetWithDetailsAsync(student.Id))!;
			current.UpdateFamilyInfo(
				new ParentGuardianDetails("Rajesh", null, null, "father@example.com", "9876543210", null, null, null, null, null, null, null, null),
				new ParentGuardianDetails("Meena", null, null, "mother@example.com", "9876543211", null, null, null, null, null, null, null, null),
				new ParentGuardianDetails("Anita", null, null, null, "9876543212", null, null, null, null, null, null, null, null), []);
			if (selected != null) StudentFamilyContactSelection.Select(current, selected);
			await _studentRepository.UpdateAsync(current, true);
		});
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<ICommunicationAudienceResolver>().ResolveAsync(Guid.NewGuid(), new()
		{
			StudentIds = [student.Id], ParentsFirst = true, IncludeStudentsWhenEligible = false
		}));
		if (selected == null)
		{
			result.Recipients.Count.ShouldBe(2);
			string.Join(",", result.Recipients.Select(recipient => recipient.Email).OrderBy(value => value)).ShouldBe(expectedEmails);
		}
		else
		{
			var contact = result.Recipients.ShouldHaveSingleItem();
			contact.Email.ShouldBe(expectedEmails);
			contact.MobileNumber.ShouldBe(expectedMobile);
		}
	});

	[Fact]
	public Task Main_school_contact_communication_uses_selected_guardian_without_borrowing_parent_email() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering);
		input.ParentRelationship = "father";
		input.GuardianResponsibilityConfirmed = true;
		input.GuardianEmail = "father@example.com";
		input.FamilyContacts = new()
		{
			MainContactRelationship = "guardian",
			OtherGuardian = new() { FullName = "Anita Sharma", Relationship = "Aunt", Mobile = "9876543212" }
		};
		var inquiry = await service.CreateAsync(input);
		inquiry.MainSchoolContact.ShouldNotBeNull().FullName.ShouldBe("Anita Sharma");
		inquiry.MainSchoolContact!.Email.ShouldBeNull();
		inquiry.NormalizedPhone.ShouldBe(input.GuardianPhone);
		var result = await service.CreateCommunicationAsync(inquiry.Id, new()
		{
			Title = "School contact follow-up", Body = "Please contact the school office.",
			Channels = [CommunicationChannelType.Sms], PublishImmediately = false
		});
		var campaign = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<CommunicationCampaign, Guid>>().GetAsync(result.CampaignId));
		var audience = JsonSerializer.Deserialize<CommunicationAudienceDefinitionDto>(campaign.AudienceDefinitionJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		var recipient = audience.ExplicitRecipients.Single();
		recipient.DisplayName.ShouldBe("Anita Sharma");
		recipient.MobileNumber.ShouldBe("9876543212");
		recipient.Email.ShouldBeNull();
		(await Should.ThrowAsync<BusinessException>(() => service.CreateCommunicationAsync(inquiry.Id, new()
		{
			Title = "Email follow-up", Subject = "School message", Body = "Please contact the school office.",
			Channels = [CommunicationChannelType.Email], PublishImmediately = false
		}))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});
}
