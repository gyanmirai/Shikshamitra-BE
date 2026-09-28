using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize, RemoteService(false)]
public class AdmissionPublicInquiryAppService(
	IRepository<AdmissionPublicInquiryLink, Guid> links,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionIntakeProfile, Guid> profiles,
	IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionFormSectionDefinition, Guid> formSections,
	IRepository<AdmissionFormFieldDefinition, Guid> formFields,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionInquiryPeopleService people,
	AdmissionInquiryAppService inquiryService,
	IDataFilter dataFilter) : ApplicationService, IAdmissionPublicInquiryAppService
{
	private const string LinkUnavailable = "Admissions.PublicInquiry.LinkUnavailable";

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionPublicInquiryLinkDto>> GetLinksAsync() =>
		(await links.GetListAsync()).OrderByDescending(item => item.CreationTime).Select(Map).ToList();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionPublicInquiryLinkCreatedDto> CreateLinkAsync(CreateAdmissionPublicInquiryLinkDto input)
	{
		if (input.AdmissionCycleId == Guid.Empty || input.CounsellorAssignmentProfileId == Guid.Empty ||
			string.IsNullOrWhiteSpace(input.Name) || !Enum.IsDefined(input.Source) ||
			input.ExpiresAtUtc.HasValue && input.ExpiresAtUtc.Value.ToUniversalTime() <= Clock.Now.ToUniversalTime())
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		var cycle = await cycles.FindAsync(input.AdmissionCycleId);
		if (cycle?.Status != AdmissionCycleStatus.Published)
			throw new BusinessException(AdmissionErrorCodes.InquiryCycleNotFound);
		var profile = await profiles.FindAsync(input.CounsellorAssignmentProfileId);
		if (profile?.Kind != AdmissionIntakeProfileKind.CounsellorAssignment)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		if (input.InquiryFormDefinitionId.HasValue)
			await EnsurePublishedInquiryFormAsync(input.InquiryFormDefinitionId.Value);
		AdmissionCounsellorAssignmentRules rules;
		try
		{
			rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(profile.ConfigurationJson,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
			rules.Validate();
		}
		catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
		{
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		}
		var hasEligibleOwner = false;
		foreach (var userId in rules.CounsellorUserIds)
			if (await people.CanAccessCycleAsync(userId, cycle.Id)) { hasEligibleOwner = true; break; }
		if (!hasEligibleOwner)
			throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorInvalid,
				"Choose a counsellor who can access an active Inquiry offering in this period.");
		var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		var link = new AdmissionPublicInquiryLink(GuidGenerator.Create(), CurrentTenant.Id,
			cycle.Id, profile.Id, input.Name, Hash(token), input.Source,
			input.SourceDetail, input.ExpiresAtUtc, input.InquiryFormDefinitionId);
		await links.InsertAsync(link, autoSave: true);
		var result = new AdmissionPublicInquiryLinkCreatedDto
		{
			Token = token, Id = link.Id, AdmissionCycleId = link.AdmissionCycleId,
			CounsellorAssignmentProfileId = link.CounsellorAssignmentProfileId,
			InquiryFormDefinitionId = link.InquiryFormDefinitionId,
			Name = link.Name, Source = link.Source, SourceDetail = link.SourceDetail,
			ExpiresAtUtc = link.ExpiresAtUtc, IsActive = link.IsActive,
			ConcurrencyStamp = link.ConcurrencyStamp
		};
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionPublicInquiryLinkDto> SetLinkFormAsync(Guid id,
		SetAdmissionPublicInquiryLinkFormDto input)
	{
		var link = await links.FindAsync(id) ??
			throw new EntityNotFoundException(typeof(AdmissionPublicInquiryLink), id);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) ||
			link.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.ConcurrencyConflict);
		await EnsurePublishedInquiryFormAsync(input.InquiryFormDefinitionId);
		link.SetInquiryForm(input.InquiryFormDefinitionId);
		await links.UpdateAsync(link, autoSave: true);
		return Map(link);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task RevokeLinkAsync(Guid id, string concurrencyStamp)
	{
		var link = await links.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionPublicInquiryLink), id);
		if (string.IsNullOrWhiteSpace(concurrencyStamp) || link.ConcurrencyStamp != concurrencyStamp)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.ConcurrencyConflict);
		link.Revoke();
		await links.UpdateAsync(link, autoSave: true);
	}

	[AllowAnonymous]
	public async Task<PublicAdmissionInquiryContextDto> GetPublicAsync(string token)
	{
		var (link, cycle) = await ResolveLinkAsync(token);
		var context = new PublicAdmissionInquiryContextDto
		{
			AdmissionCycleName = cycle.Name,
			Source = link.Source
		};
		if (!link.InquiryFormDefinitionId.HasValue) return context;
		using (CurrentTenant.Change(link.TenantId))
		{
			var formId = link.InquiryFormDefinitionId.Value;
			var form = await forms.GetAsync(formId);
			var sections = await formSections.GetListAsync(item => item.FormDefinitionId == formId);
			var fields = await formFields.GetListAsync(item => item.FormDefinitionId == formId);
			context.Form = new PublicAdmissionInquiryFormDto
			{
				Id = form.Id, Name = form.Name, Version = form.Version,
				Sections = sections.OrderBy(item => item.DisplayOrder).Select(section => new PublicAdmissionInquirySectionDto
				{
					Code = section.Code, Name = section.Name, DisplayOrder = section.DisplayOrder,
					VisibilityRuleJson = section.VisibilityRuleJson,
					Fields = fields.Where(field => field.SectionId == section.Id)
						.OrderBy(field => field.DisplayOrder).Select(field => new PublicAdmissionInquiryFieldDto
						{
							Code = field.Code, Label = field.Label, FieldType = field.FieldType,
							Required = field.Required, ReadOnly = field.ReadOnly,
							DisplayOrder = field.DisplayOrder, Placeholder = field.Placeholder,
							HelpText = field.HelpText, DefaultValue = field.DefaultValue,
							OptionsJson = field.OptionsJson, ValidationJson = field.ValidationJson,
							VisibilityRuleJson = field.VisibilityRuleJson,
							RequiredRuleJson = field.RequiredRuleJson
						}).ToList()
				}).ToList()
			};
			AddIdentityFallbacks(context.Form, fields);
		}
		return context;
	}

	[AllowAnonymous]
	public async Task SubmitPublicAsync(string token, SubmitPublicAdmissionInquiryDto input)
	{
		var (link, cycle) = await ResolveLinkAsync(token);
		input.FormValues ??= [];
		if (!input.ConsentToContact)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Consent to be contacted is required.");
		if (!link.InquiryFormDefinitionId.HasValue &&
			(string.IsNullOrWhiteSpace(input.ApplicantFirstName) ||
			 string.IsNullOrWhiteSpace(input.GuardianFirstName) ||
			 string.IsNullOrWhiteSpace(input.GuardianPhone) ||
			 input.FormValues.Count > 0))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Enter the required student and guardian details.");
		using (CurrentTenant.Change(link.TenantId))
		{
			var details = link.SourceDetail ?? link.Name;
			if (!string.IsNullOrWhiteSpace(input.ClassInterest))
				details = $"{details}; Class interest: {input.ClassInterest.Trim()}";
			if (details.Length > 512) details = details[..512];
			try
			{
				var inquiry = await inquiryService.CreateFromPublicLinkAsync(new CreateAdmissionInquiryDto
				{
					AdmissionCycleId = cycle.Id,
					ApplicantFirstName = input.ApplicantFirstName?.Trim() ?? string.Empty,
					ApplicantLastName = input.ApplicantLastName?.Trim(),
					ApplicantName = string.Join(" ", new[] { input.ApplicantFirstName, input.ApplicantLastName }
						.Where(value => !string.IsNullOrWhiteSpace(value))).Trim(),
					GuardianFirstName = input.GuardianFirstName?.Trim() ?? string.Empty,
					GuardianLastName = input.GuardianLastName?.Trim(),
					GuardianPhone = input.GuardianPhone?.Trim() ?? string.Empty,
					GuardianEmail = input.GuardianEmail?.Trim(),
					Phone = input.GuardianPhone?.Trim() ?? string.Empty,
					PreferredLanguageCode = input.PreferredLanguageCode?.Trim() ?? "en",
					Source = link.Source,
					SourceDetail = details,
					FormValues = input.FormValues
				}, link.CounsellorAssignmentProfileId, link.InquiryFormDefinitionId);
				await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
					"PublicInquiryConsent", "Guardian consented to admission inquiry contact.",
					inquiry.ApplicantId, null, inquiry.Id,
					JsonSerializer.Serialize(new { LinkId = link.Id, ConsentToContact = true })), autoSave: true);
			}
			catch (BusinessException error) when (error.Code == AdmissionErrorCodes.InquiryDuplicate)
			{
				// A public caller receives the same acknowledgement for an existing phone.
			}
		}
	}

	private async Task<(AdmissionPublicInquiryLink Link, AdmissionCycle Cycle)> ResolveLinkAsync(string token)
	{
		if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
			throw new BusinessException(LinkUnavailable);
		AdmissionPublicInquiryLink? link;
		using (dataFilter.Disable<IMultiTenant>())
			link = await links.FindAsync(item => item.TokenHash == Hash(token));
		if (link == null || !link.CanAccept(Clock.Now))
			throw new BusinessException(LinkUnavailable);
		using (CurrentTenant.Change(link.TenantId))
		{
			var cycle = await cycles.FindAsync(link.AdmissionCycleId);
			if (cycle?.Status != AdmissionCycleStatus.Published ||
				Clock.Now.Date < cycle.StartDate.Date || Clock.Now.Date > cycle.EndDate.Date)
				throw new BusinessException(LinkUnavailable);
			if (link.InquiryFormDefinitionId.HasValue &&
				await forms.FindAsync(link.InquiryFormDefinitionId.Value) is not
				{ Status: AdmissionFormStatus.Published, Purpose: AdmissionFormPurpose.Inquiry })
				throw new BusinessException(LinkUnavailable);
			return (link, cycle);
		}
	}

	private static string Hash(string token) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

	private static void AddIdentityFallbacks(PublicAdmissionInquiryFormDto form,
		IReadOnlyCollection<AdmissionFormFieldDefinition> fields)
	{
		var fallback = new PublicAdmissionInquirySectionDto
		{
			Code = "PUBLIC_IDENTITY", Name = "Contact details", DisplayOrder = int.MaxValue
		};
		if (!HasIdentityField(fields, "Applicant.FirstName", "APPLICANT_FIRST_NAME", "STUDENT_FIRST_NAME", "FIRST_NAME"))
			fallback.Fields.Add(new PublicAdmissionInquiryFieldDto
			{
				Code = "PUBLIC_APPLICANT_FIRST_NAME", Label = "Applicant first name",
				FieldType = AdmissionFormFieldType.Text, Required = true, IsIdentityFallback = true
			});
		if (!HasIdentityField(fields, "Guardian.FirstName", "GUARDIAN_FIRST_NAME", "PARENT_FIRST_NAME", "GUARDIAN_NAME", "PARENT_NAME"))
			fallback.Fields.Add(new PublicAdmissionInquiryFieldDto
			{
				Code = "PUBLIC_GUARDIAN_FIRST_NAME", Label = "Guardian first name",
				FieldType = AdmissionFormFieldType.Text, Required = true, IsIdentityFallback = true
			});
		if (!HasIdentityField(fields, "Guardian.Mobile", "GUARDIAN_MOBILE", "GUARDIAN_PHONE", "PARENT_MOBILE", "PARENT_PHONE"))
			fallback.Fields.Add(new PublicAdmissionInquiryFieldDto
			{
				Code = "PUBLIC_GUARDIAN_PHONE", Label = "Guardian mobile",
				FieldType = AdmissionFormFieldType.Phone, Required = true, IsIdentityFallback = true,
				ValidationJson = "{\"regex\":\"^[6-9]\\\\d{9}$\"}"
			});
		if (fallback.Fields.Count > 0) form.Sections.Add(fallback);
	}

	private static bool HasIdentityField(IEnumerable<AdmissionFormFieldDefinition> fields,
		string bindingKey, params string[] aliases) =>
		fields.Any(field => string.Equals(field.BindingKey, bindingKey, StringComparison.OrdinalIgnoreCase) ||
			aliases.Contains(field.Code, StringComparer.OrdinalIgnoreCase));

	private async Task EnsurePublishedInquiryFormAsync(Guid formId)
	{
		if (formId == Guid.Empty || await forms.FindAsync(formId) is not
			{ Status: AdmissionFormStatus.Published, Purpose: AdmissionFormPurpose.Inquiry })
			throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished,
				"Choose a published Inquiry form for this link.");
	}

	private static AdmissionPublicInquiryLinkDto Map(AdmissionPublicInquiryLink link) => new()
	{
		Id = link.Id,
		AdmissionCycleId = link.AdmissionCycleId,
		CounsellorAssignmentProfileId = link.CounsellorAssignmentProfileId,
		InquiryFormDefinitionId = link.InquiryFormDefinitionId,
		Name = link.Name,
		Source = link.Source,
		SourceDetail = link.SourceDetail,
		ExpiresAtUtc = link.ExpiresAtUtc,
		IsActive = link.IsActive,
		ConcurrencyStamp = link.ConcurrencyStamp
	};
}
