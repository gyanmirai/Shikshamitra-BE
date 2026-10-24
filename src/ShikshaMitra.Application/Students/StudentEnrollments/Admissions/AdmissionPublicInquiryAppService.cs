using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.SchoolIdentity;
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
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionIntakeProfile, Guid> profiles,
	IRepository<AdmissionIntakeSetup, Guid> setups,
	IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionFormSectionDefinition, Guid> formSections,
	IRepository<AdmissionFormFieldDefinition, Guid> formFields,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionInquiryPeopleService people,
	AdmissionInquiryAppService inquiryService,
	IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> academicStages,
	IRepository<Standard, Guid> standards,
	AdmissionInquiryAvailabilityService availabilityService,
	CommunicationAppService communications,
	IDataFilter dataFilter, SchoolIdentityReadService schoolIdentity,
	SchoolIdentityDocumentService identityDocuments) : ApplicationService, IAdmissionPublicInquiryAppService
{
	private const string LinkUnavailable = "Admissions.PublicInquiry.LinkUnavailable";

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionPublicInquiryLinkDto>> GetLinksAsync() =>
		(await links.GetListAsync()).OrderByDescending(item => item.CreationTime).Select(Map).ToList();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionPublicInquiryLinkOptionDto>> GetLinkOptionsAsync(Guid admissionCycleId)
	{
		if ((await cycles.FindAsync(admissionCycleId))?.Status != AdmissionCycleStatus.Published) return [];
		var available = await people.GetCurrentInquiryOfferingsQueryAsync(admissionCycleId);
		var formQuery = await forms.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync((from offering in available
			join form in formQuery on offering.InquiryFormDefinitionId equals form.Id
			where form.Status == AdmissionFormStatus.Published && form.Purpose == AdmissionFormPurpose.Inquiry
			select new AdmissionPublicInquiryLinkOptionDto { CounsellorAssignmentProfileId = null, InquiryFormDefinitionId = form.Id }).Distinct());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionPublicInquiryLinkCreatedDto> CreateLinkAsync(CreateAdmissionPublicInquiryLinkDto input)
	{
		if (input.AdmissionCycleId == Guid.Empty || !input.InquiryFormDefinitionId.HasValue ||
			string.IsNullOrWhiteSpace(input.Name) || !Enum.IsDefined(input.Source))
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		if (input.ExpiresAtUtc.HasValue && input.ExpiresAtUtc.Value.ToUniversalTime() <= Clock.Now.ToUniversalTime())
			throw new BusinessException(AdmissionErrorCodes.PublicInquiryExpiryInvalid);
		var cycle = await cycles.FindAsync(input.AdmissionCycleId);
		if (cycle?.Status != AdmissionCycleStatus.Published)
			throw new BusinessException(AdmissionErrorCodes.InquiryCycleNotFound);
		await EnsurePublishedInquiryFormAsync(input.InquiryFormDefinitionId!.Value);
		var activeOfferings = await people.GetCurrentInquiryOfferingsQueryAsync(cycle.Id);
		if (!await AsyncExecuter.AnyAsync(activeOfferings.Where(offering => offering.InquiryFormDefinitionId == input.InquiryFormDefinitionId)))
			throw new BusinessException(AdmissionErrorCodes.PublicInquiryOfferingRequired,
				"Choose a published inquiry form linked to an accessible active Offering in this period.").WithData("PeriodName", cycle.Name);
		var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		var link = new AdmissionPublicInquiryLink(GuidGenerator.Create(), CurrentTenant.Id,
			cycle.Id, null, input.Name, Hash(token), input.Source,
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
	public async Task<PublicAdmissionInquiryContextDto> GetPublicAsync(string token, Guid? offeringId = null)
	{
		var (link, cycle) = await ResolveLinkAsync(token);
		var context = new PublicAdmissionInquiryContextDto
		{
			AdmissionCycleName = cycle.Name,
			Source = link.Source
		};
		using (CurrentTenant.Change(link.TenantId))
		{
			context.AcademicOptions = await GetAcademicOptionsAsync(cycle);
			await SetBrandingAsync(context, token, null, null, null, cycle.AcademicYearId);
			var choices = await GetLinkOfferingsAsync(link);
			foreach (var offering in choices.OrderBy(item => item.Name))
			{
				var academic = context.AcademicOptions.FirstOrDefault(item => item.StandardId == offering.StandardId &&
					item.MediumId == offering.MediumId && item.BoardId == offering.BoardId);
				if (academic == null) continue;
				var available = await availabilityService.ReadForPublicAsync(offering);
				context.Offerings.Add(new PublicAdmissionInquiryOfferingDto
				{
					Id = offering.Id, AdmissionCycleId = cycle.Id, AdmissionCycleName = cycle.Name,
					BoardId = academic.BoardId, BoardName = academic.BoardName,
					MediumId = academic.MediumId, MediumName = academic.MediumName,
					StandardId = academic.StandardId, StandardName = academic.StandardName,
					Name = offering.Name, Code = offering.Code, Track = offering.Track,
					CanCreateInquiry = available.CanCreateInquiry, UnavailableReason = available.Reason
				});
			}
			if (!offeringId.HasValue) return context;
			var selected = choices.FirstOrDefault(item => item.Id == offeringId.Value) ??
				throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable,
					"Admission is not open for this class.");
			await availabilityService.EnsureAvailableForPublicAsync(selected);
			await SetBrandingAsync(context, token, offeringId, selected.BoardId, selected.MediumId, cycle.AcademicYearId);
			var formId = selected.InquiryFormDefinitionId ??
				throw new BusinessException(AdmissionErrorCodes.OfferingConfigurationIncomplete);
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

	private async Task SetBrandingAsync(PublicAdmissionInquiryContextDto context, string token, Guid? offeringId,
		Guid? boardId, Guid? mediumId, Guid? yearId)
	{
		var branding = await schoolIdentity.ReadAsync(boardId, mediumId, yearId, signature: false);
		var prefix = $"/api/public/admission-inquiries/{token}/branding/";
		var query = offeringId.HasValue ? $"?offeringId={offeringId}" : "";
		context.LogoUrl = branding.Effective["logo"].Asset == null ? null : prefix + "0" + query;
		context.BannerUrl = branding.Effective["banner"].Asset == null ? null : prefix + "1" + query;
	}

	[AllowAnonymous]
	public async Task<SchoolIdentityAssetDownloadDto> GetPublicBrandingAssetAsync(string token, SchoolIdentityAssetKind kind, Guid? offeringId = null)
	{
		if (kind is not (SchoolIdentityAssetKind.Logo or SchoolIdentityAssetKind.Banner)) throw new BusinessException(LinkUnavailable);
		var (link, cycle) = await ResolveLinkAsync(token);
		using var tenantScope = CurrentTenant.Change(link.TenantId);
		AdmissionOffering? offering = null;
		if (offeringId.HasValue)
		{
			offering = (await GetLinkOfferingsAsync(link)).FirstOrDefault(item => item.Id == offeringId.Value)
				?? throw new BusinessException(LinkUnavailable);
			await availabilityService.EnsureAvailableForPublicAsync(offering);
		}
		var branding = await schoolIdentity.ReadAsync(offering?.BoardId, offering?.MediumId, cycle.AcademicYearId, signature: false);
		var file = branding.Effective[kind.ToString().ToLowerInvariant()].Asset ?? throw new BusinessException(LinkUnavailable);
		var snapshot = SchoolIdentityDocumentService.Snapshot(branding, link.TenantId!.Value);
		var bytes = await identityDocuments.LoadAsync(snapshot);
		return new() { Content = kind == SchoolIdentityAssetKind.Logo ? bytes.Logo! : bytes.Banner!, ContentType = file.ContentType, FileName = file.FileName };
	}

	[AllowAnonymous]
	public async Task SubmitPublicAsync(string token, SubmitPublicAdmissionInquiryDto input)
	{
		var (link, cycle) = await ResolveLinkAsync(token);
		input.FormValues ??= [];
		if (!input.ConsentToContact)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Consent to be contacted is required.");
		using (CurrentTenant.Change(link.TenantId))
		{
			var offering = (await GetLinkOfferingsAsync(link)).FirstOrDefault(item => item.Id == input.AdmissionOfferingId) ??
				throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable,
					"Admission is not open for this class.");
			if (input.BoardId != offering.BoardId || input.MediumId != offering.MediumId || input.StandardId != offering.StandardId)
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
					"Choose the board, medium and class belonging to this admission option.");
			await availabilityService.EnsureAvailableForPublicAsync(offering);
			var details = link.SourceDetail ?? link.Name;
			if (!string.IsNullOrWhiteSpace(input.ClassInterest))
				details = $"{details}; Class interest: {input.ClassInterest.Trim()}";
			if (details.Length > 512) details = details[..512];
			try
			{
				var inquiry = await inquiryService.CreateFromPublicLinkAsync(new CreateAdmissionInquiryDto
				{
					AdmissionCycleId = cycle.Id,
					AdmissionOfferingId = offering.Id,
					BoardId = input.BoardId, MediumId = input.MediumId, StandardId = input.StandardId,
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
				}, null, offering.InquiryFormDefinitionId);
				await communications.RecordPublicInquiryConsentAsync(
					await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id), link.Id);
				await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("PublicInquiryConsent", "Guardian consented to admission inquiry contact.",
					inquiry.ApplicantId, null, inquiry.Id,
					JsonSerializer.Serialize(new { LinkId = link.Id, ConsentToContact = true })), autoSave: true);
			}
			catch (BusinessException error) when (error.Code == AdmissionErrorCodes.InquiryDuplicate)
			{
				// A public caller receives the same acknowledgement for an existing phone.
			}
		}
	}

	private Task<List<AdmissionOffering>> GetLinkOfferingsAsync(AdmissionPublicInquiryLink link) =>
		offerings.GetListAsync(item => item.AdmissionCycleId == link.AdmissionCycleId &&
			item.EntryMode != AdmissionEntryMode.DirectRegistration &&
			(!link.InquiryFormDefinitionId.HasValue || item.InquiryFormDefinitionId == link.InquiryFormDefinitionId));

	private async Task<List<PublicAdmissionInquiryAcademicOptionDto>> GetAcademicOptionsAsync(AdmissionCycle cycle)
	{
		var query = from board in await boards.GetQueryableAsync()
			join medium in await mediums.GetQueryableAsync() on board.Id equals medium.EducationBoardId
			join stage in await academicStages.GetQueryableAsync() on medium.Id equals stage.EducationMediumId
			join standard in await standards.GetQueryableAsync() on stage.Id equals standard.EducationMediumStageId
			where board.AcademicYearId == cycle.AcademicYearId
			select new { BoardId = board.Id, board.Type, MediumId = medium.Id, MediumName = medium.Name,
				StandardId = standard.Id, StandardName = standard.Name };
		return (await AsyncExecuter.ToListAsync(query)).Select(item => new PublicAdmissionInquiryAcademicOptionDto
		{
			BoardId = item.BoardId, BoardName = item.Type.ToString(), MediumId = item.MediumId,
			MediumName = item.MediumName, StandardId = item.StandardId, StandardName = item.StandardName
		}).ToList();
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
