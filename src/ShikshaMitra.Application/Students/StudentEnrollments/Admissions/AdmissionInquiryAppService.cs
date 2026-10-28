using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Communication;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize, RemoteService(false)]
public class AdmissionInquiryAppService(
	IRepository<AdmissionInquiry, Guid> inquiryRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	AdmissionOfferingRuntimeService offeringRuntimeService,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> formFieldRepository,
	IRepository<AdmissionFormSectionDefinition, Guid> formSectionRepository,
	AdmissionApplicationFormValidator formValidator,
	IRepository<AdmissionActivity, Guid> activityRepository,
	IRepository<AdmissionInquiryManagement, Guid> managementRepository,
	IRepository<AdmissionInquiryFollowUp, Guid> followUpRepository,
	IAdmissionReferenceSequenceRepository referenceSequenceRepository,
	AdmissionInquiryScopeAccessService scopeAccess,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionDirectFamilyMatchService familyMatchService,
	AdmissionInquiryPolicyResolver policyResolver,
	AdmissionInquiryRuntimeService inquiryRuntime,
	AdmissionInquiryAssignmentService assignmentService,
	AdmissionIntakeAssignmentService intakeAssignmentService,
	AdmissionInquiryPeopleService peopleService,
	AdmissionInquiryFormEditService formEditService,
	AdmissionInquiryAvailabilityService inquiryAvailability,
	ICommunicationAppService communicationService) : ApplicationService, IAdmissionInquiryAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<PagedResultDto<AdmissionInquiryDto>> GetListAsync(AdmissionInquiryListInput input)
	{
		var query = await BuildFilteredQueryAsync(input);
		var now = Clock.Now.ToUniversalTime();
		var count = await AsyncExecuter.CountAsync(query);
		query = ApplySorting(query, input.Sorting);
		var rows = await AsyncExecuter.ToListAsync(query.Skip(input.SkipCount).Take(input.MaxResultCount));
		if (rows.Count == 0) return new PagedResultDto<AdmissionInquiryDto>(count, []);

		var inquiryIds = rows.Select(row => row.Id).ToList();
		var linkedApplications = (await applicationRepository.GetListAsync(application => application.InquiryId.HasValue && inquiryIds.Contains(application.InquiryId.Value)))
			.GroupBy(application => application.InquiryId!.Value).ToDictionary(group => group.Key, group => group.OrderByDescending(application => application.CreationTime).First().Id);
		var managementRows = await managementRepository.GetListAsync(state => inquiryIds.Contains(state.InquiryId));
		var openFollowUps = await followUpRepository.GetListAsync(followUp =>
			inquiryIds.Contains(followUp.InquiryId) && followUp.Status == AdmissionInquiryFollowUpStatus.Scheduled);
		var cycleIds = rows.Select(row => row.AdmissionCycleId).Distinct().ToList();
		var offeringIds = rows.Where(row => row.AdmissionOfferingId.HasValue)
			.Select(row => row.AdmissionOfferingId!.Value).Distinct().ToList();
		var cycleNames = (await cycleRepository.GetListAsync(cycle => cycleIds.Contains(cycle.Id)))
			.ToDictionary(cycle => cycle.Id, cycle => cycle.Name);
		var offerings = (await offeringRepository.GetListAsync(offering => offeringIds.Contains(offering.Id)))
			.ToDictionary(offering => offering.Id);
		var standardIds = offerings.Values.Select(offering => offering.StandardId).Distinct().ToArray();
		var standardNames = (await standardRepository.GetListAsync(standard => Enumerable.Contains(standardIds, standard.Id)))
			.ToDictionary(standard => standard.Id, standard => standard.Name);
		var managementByInquiry = managementRows.ToDictionary(state => state.InquiryId);
		var followUpsByInquiry = openFollowUps.GroupBy(followUp => followUp.InquiryId)
			.ToDictionary(group => group.Key, group => group.OrderBy(followUp => followUp.ScheduledAt).ToList());

		return new PagedResultDto<AdmissionInquiryDto>(count, rows.Select(row =>
		{
			var offering = row.AdmissionOfferingId.HasValue ? offerings.GetValueOrDefault(row.AdmissionOfferingId.Value) : null;
			var dto = Map(row,
			managementByInquiry.GetValueOrDefault(row.Id), followUpsByInquiry.GetValueOrDefault(row.Id) ?? [],
			cycleNames.GetValueOrDefault(row.AdmissionCycleId),
			offering?.Code,
			null,
				now);
			dto.StandardId = offering?.StandardId;
			dto.StandardName = offering != null ? standardNames.GetValueOrDefault(offering.StandardId) : null;
			dto.ApplicationId = linkedApplications.GetValueOrDefault(row.Id) is var applicationId && applicationId != Guid.Empty ? applicationId : null;
			dto.IsTransferred |= dto.ApplicationId.HasValue;
			return dto;
		}).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<AdmissionInquiryDto> GetAsync(Guid id)
	{
		var inquiry = await GetInquiryAsync(id);
		var result = Map(inquiry);
		result.ApplicationId = (await applicationRepository.FindAsync(application => application.InquiryId == inquiry.Id))?.Id;
		result.IsTransferred |= result.ApplicationId.HasValue;
		if (inquiry.AdmissionOfferingId.HasValue && await offeringRepository.FindAsync(inquiry.AdmissionOfferingId.Value) is { } offering)
		{
			result.BoardId = offering.BoardId; result.MediumId = offering.MediumId; result.StandardId = offering.StandardId;
			result.StandardName = (await standardRepository.FindAsync(offering.StandardId))?.Name;
		}
		var applicant = inquiry.ApplicantId.HasValue ? await applicantRepository.FindAsync(inquiry.ApplicantId.Value) : null;
		var family = inquiry.FamilyId.HasValue ? await familyRepository.FindAsync(inquiry.FamilyId.Value) : null;
		var guardian = family?.PrimaryGuardianId is Guid guardianId ? await guardianRepository.FindAsync(guardianId) : null;
		if (applicant != null)
		{
			result.ApplicantFirstName = applicant.FirstName; result.ApplicantLastName = applicant.LastName; result.ApplicantDateOfBirth = applicant.DateOfBirth;
		}
		if (guardian != null)
		{
			result.GuardianFirstName = guardian.FirstName; result.GuardianLastName = guardian.LastName;
		}
		if (inquiry.FormResponseDefinitionId is not Guid formId ||
			string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson)) return result;
		var snapshot = JsonSerializer.Deserialize<Dictionary<string, string>>(
			inquiry.FormResponseSnapshotJson);
		if (snapshot == null) return result;
		var answers = new Dictionary<string, string>(snapshot, StringComparer.OrdinalIgnoreCase);
		var fields = await formFieldRepository.GetListAsync(field => field.FormDefinitionId == formId);
		// Older intake forms captured mandatory identity outside the form response.
		foreach (var field in fields.Where(field => field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)))
			if (!answers.ContainsKey(field.BindingKey!) && applicant != null)
				answers[field.BindingKey!] = JsonSerializer.Serialize(AdmissionFormBindingRegistry.GetValue(field.BindingKey!, applicant, guardian));
		var sections = await formSectionRepository.GetListAsync(section => section.FormDefinitionId == formId);
		if (await formRepository.FindAsync(formId) is { } formDefinition)
			result.FormDefinition = AdmissionApplicationFormService.MapDefinition(formDefinition, sections, fields);
		var sectionOrder = sections.ToDictionary(section => section.Id, section => section.DisplayOrder);
		result.FormResponseDefinitionId = formId;
		result.FormValues = fields.Select(field => new AdmissionFormValueDto
		{
			FieldCode = field.Code,
			ValueJson = answers.GetValueOrDefault(string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!)
		}).ToList();
		result.FormAnswers = fields
			.OrderBy(field => sectionOrder.GetValueOrDefault(field.SectionId))
			.ThenBy(field => field.DisplayOrder)
			.Select(field =>
			{
				var key = string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey;
				answers.TryGetValue(key!, out var raw);
				return new AdmissionInquiryFormAnswerDto
				{
					FieldCode = field.Code,
					Label = field.Label,
					Value = DisplayInquiryAnswer(field, raw)
				};
			}).ToList();
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<List<AdmissionInquiryTimelineItemDto>> GetTimelineAsync(Guid id)
	{
		await GetInquiryAsync(id);
		var rows = await activityRepository.GetListAsync(activity => activity.InquiryId == id);
		var actors = await LazyServiceProvider.LazyGetRequiredService<StudentJourneyActorReader>().ReadAsync(rows.Select(item => item.CreatorId));
		return rows
			.OrderByDescending(activity => activity.OccurredAt)
			.Select(activity => new AdmissionInquiryTimelineItemDto
			{
				Id = activity.Id,
				ActivityType = activity.ActivityType,
				Description = activity.Description,
				OccurredAt = activity.OccurredAt,
				CreatorId = activity.CreatorId,
				ActorName = StudentJourneyActorMetadata.Name(activity.MetadataJson) ?? StudentJourneyActorReader.Name(actors, activity.CreatorId),
				MetadataJson = activity.MetadataJson
			})
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<byte[]> ExportCsvAsync(AdmissionInquiryListInput input)
	{
		var query = ApplySorting(await BuildFilteredQueryAsync(input), input.Sorting);
		var count = await AsyncExecuter.CountAsync(query);
		if (count > 50000)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Narrow the filters before exporting more than 50,000 inquiries.");
		var rows = await AsyncExecuter.ToListAsync(query);
		var counsellorNames = await LazyServiceProvider.LazyGetRequiredService<StudentJourneyActorReader>().ReadAsync(rows.Select(row => row.CreatorId));
		var builder = new StringBuilder("Inquiry number,Applicant,Phone,Email,Language,Source,Source details,Status,Captured by,Created at\r\n");
		foreach (var row in rows)
			builder.AppendLine(string.Join(",", new[]
			{
				Csv(row.InquiryNumber), Csv(row.ApplicantName), Csv(row.NormalizedPhone), Csv(row.Email),
				Csv(row.PreferredLanguageCode), Csv(row.Source.ToString()), Csv(row.SourceDetail),
				Csv(AdmissionInquiryStatusRules.Display(row.Status) switch { AdmissionInquiryStatus.New => "Pending", AdmissionInquiryStatus.Interested => "Interested", _ => "Not interested" }), Csv(StudentJourneyActorReader.Name(counsellorNames, row.CreatorId)),
				Csv(row.CreationTime.ToString("O"))
			}));
		return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create)]
	public Task<AdmissionInquiryDto> CreateAsync(CreateAdmissionInquiryDto input) =>
		CreateCoreAsync(input, isPublic: false, publicAssignmentProfileId: null);

	internal Task<AdmissionInquiryDto> CreateForReturnAsync(CreateAdmissionInquiryDto input, Student student) =>
		CreateCoreAsync(input, isPublic: false, publicAssignmentProfileId: null, returningStudent: student);

	[AllowAnonymous, RemoteService(false)]
	public Task<AdmissionInquiryDto> CreateFromPublicLinkAsync(CreateAdmissionInquiryDto input,
		Guid? assignmentProfileId, Guid? inquiryFormDefinitionId = null) =>
		CreateCoreAsync(input, isPublic: true, assignmentProfileId, inquiryFormDefinitionId);

	private async Task<AdmissionInquiryDto> CreateCoreAsync(CreateAdmissionInquiryDto input, bool isPublic,
		Guid? publicAssignmentProfileId, Guid? publicFormDefinitionId = null, Student? returningStudent = null)
	{
		if (isPublic && input.ExistingParentApplicantId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Only authorized school staff can confirm an existing parent.");
		if (!Enum.IsDefined(input.Source))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid inquiry source.");
		if (!input.AdmissionOfferingId.HasValue)
			throw new BusinessException(AdmissionErrorCodes.InquiryOfferingRequired,
				"Select a class with an open admission offering before capturing an inquiry.");
		var runtime = isPublic
			? await offeringRuntimeService.GetAvailableForPublicAsync(input.AdmissionOfferingId.Value, input.AdmissionCycleId)
			: await offeringRuntimeService.GetAvailableAsync(input.AdmissionOfferingId.Value);
		var offering = runtime.Offering;
		var cycle = runtime.Cycle;
		if (offering.EntryMode is not (AdmissionEntryMode.Inquiry or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.InquiryEntryNotAllowed,
				"This Admission Offering starts directly with Registration.");
		if (isPublic) await inquiryAvailability.EnsureAvailableForPublicAsync(offering);
		else await inquiryAvailability.EnsureAvailableAsync(offering);
		if (!isPublic) await scopeAccess.EnsureOfferingAccessAsync(offering.Id);
		AdmissionInquiryAcademicSelectionValidator.EnsureMatches(input, offering);
		if (isPublic && (publicAssignmentProfileId.HasValue && offering.CounsellorAssignmentProfileId != publicAssignmentProfileId ||
			(publicFormDefinitionId.HasValue && offering.InquiryFormDefinitionId != publicFormDefinitionId)))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose an admission option configured for this public inquiry link.");

		if (input.FormValues.GroupBy(item => item.FieldCode, StringComparer.OrdinalIgnoreCase)
			.Any(group => group.Count() > 1))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Submit each inquiry field once.");
		var values = input.FormValues.ToDictionary(x => x.FieldCode, x => UnwrapValue(x.ValueJson), StringComparer.OrdinalIgnoreCase);
		var returningContact = returningStudent == null ? null : new { input.GuardianFirstName, input.GuardianLastName, input.GuardianPhone, input.GuardianEmail };
		var inquiryFields = new List<AdmissionFormFieldDefinition>();
		var managedPolicy = await policyResolver.ResolveAsync(offering?.Id);
		var inquiryFormId = offering?.InquiryFormDefinitionId ?? publicFormDefinitionId;
		if (inquiryFormId.HasValue)
		{
			var form = await formRepository.FindAsync(inquiryFormId.Value);
			var fields = await formFieldRepository.GetListAsync(x => x.FormDefinitionId == inquiryFormId.Value);
			inquiryFields = fields;
			foreach (var answer in input.FormValues)
			{
				var field = fields.FirstOrDefault(field => string.Equals(field.Code, answer.FieldCode, StringComparison.OrdinalIgnoreCase));
				if (field == null) continue;
				answer.ValueJson = AdmissionFormEmailValueNormalizer.Normalize(field, answer.ValueJson);
				values[answer.FieldCode] = UnwrapValue(answer.ValueJson);
			}
			if (form == null || form.Status != AdmissionFormStatus.Published ||
				(isPublic && form.Purpose != AdmissionFormPurpose.Inquiry))
				throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
			{
				var fieldCodes = fields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
				if (values.Keys.Any(code => !fieldCodes.Contains(code)))
					throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
						"The inquiry contains a field outside its published form.");
				var sections = await formSectionRepository.GetListAsync(section => section.FormDefinitionId == inquiryFormId.Value);
				var validation = formValidator.Validate(fields, sections, values);
				await LazyServiceProvider.LazyGetRequiredService<AdmissionInquiryAcademicOptionsService>()
					.ValidateAsync(fields, values, offering!, cycle.AcademicYearId, isPublic);
				if (validation.Errors.Count > 0)
					throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed)
						.WithData("Fields", string.Join(", ", validation.FieldErrors.Keys));
			}
		}
		input.ApplicantName = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Applicant.Name", input.ApplicantName) ?? input.ApplicantName;
		input.ApplicantFirstName = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Applicant.FirstName", input.ApplicantFirstName) ?? input.ApplicantFirstName;
		input.ApplicantLastName = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Applicant.LastName", input.ApplicantLastName);
		input.ApplicantDateOfBirth = AdmissionInquiryIdentityFieldResolver.ResolveDate(values, inquiryFields,
			"Applicant.DateOfBirth", input.ApplicantDateOfBirth);
		input.GuardianFirstName = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Guardian.FirstName", input.GuardianFirstName) ?? input.GuardianFirstName;
		input.GuardianLastName = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Guardian.LastName", input.GuardianLastName);
		input.GuardianPhone = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Guardian.Mobile", input.GuardianPhone) ?? input.GuardianPhone;
		input.GuardianEmail = AdmissionInquiryIdentityFieldResolver.Resolve(values, inquiryFields,
			"Guardian.Email", input.GuardianEmail);
		input.Email ??= input.GuardianEmail;
		if (returningContact != null)
		{
			input.GuardianFirstName = returningContact.GuardianFirstName; input.GuardianLastName = returningContact.GuardianLastName;
			input.GuardianPhone = returningContact.GuardianPhone; input.GuardianEmail = returningContact.GuardianEmail;
			input.Email = returningContact.GuardianEmail;
		}
		if (string.IsNullOrWhiteSpace(input.ApplicantName))
			input.ApplicantName = string.Join(" ", new[] { input.ApplicantFirstName, input.ApplicantLastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		if (string.IsNullOrWhiteSpace(input.ApplicantFirstName) && !string.IsNullOrWhiteSpace(input.ApplicantName))
			input.ApplicantFirstName = input.ApplicantName;
		var missingIdentity = new[]
		{
			(string.IsNullOrWhiteSpace(input.ApplicantFirstName), "Applicant.FirstName"),
			(string.IsNullOrWhiteSpace(input.GuardianFirstName), "Guardian.FirstName"),
			(string.IsNullOrWhiteSpace(input.GuardianPhone), "Guardian.Mobile")
		}.Where(item => item.Item1).Select(item => item.Item2).ToList();
		if (missingIdentity.Count > 0)
			throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete,
				"Applicant and primary guardian identity is incomplete.")
				.WithData("Fields", string.Join(", ", missingIdentity));
		var guardianPhone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(
			input.GuardianPhone);
		if (managedPolicy != null)
		{
			var policy = managedPolicy.Policy;
			if (policy.AllowedSources.Count > 0 && !policy.AllowedSources.Contains(input.Source))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.SourceNotAllowed);
		}
		var phone = guardianPhone;
		if (input.ParentRelationship != null && !AdmissionSiblingParentSelection.IsRelationship(input.ParentRelationship) ||
			input.ExistingParentApplicantId.HasValue && (!input.ExistingFamilyId.HasValue || input.ParentRelationship == null) ||
			input.ExistingFamilyId.HasValue && input.ParentRelationship != null && !input.ExistingParentApplicantId.HasValue ||
			isPublic && input.ExistingParentApplicantId.HasValue || input.ExistingFamilyId.HasValue && input.CreateSeparateFamily)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Only authorized school staff can confirm an existing parent. Choose Father or Mother.");
		var query = await inquiryRepository.GetQueryableAsync();
		var duplicateExists = !input.ExistingFamilyId.HasValue && await AsyncExecuter.AnyAsync(query.Where(x =>
			x.AdmissionCycleId == cycle.Id && x.NormalizedPhone == phone));
		if (duplicateExists)
		{
			if (!input.CreateSeparateFamily) throw new BusinessException(AdmissionErrorCodes.InquiryDuplicate);
			if (string.IsNullOrWhiteSpace(input.DuplicateOverrideReason))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.DuplicateReasonRequired);
		}
		AdmissionFamily? existingFamily = null;
		AdmissionSiblingParentCandidateDto? selectedParent = null;
		if (input.ExistingFamilyId.HasValue)
		{
			existingFamily = await familyRepository.FindAsync(input.ExistingFamilyId.Value);
			if (existingFamily is not { Status: AdmissionFamilyStatus.Active })
				throw new BusinessException(AdmissionApplicantErrorCodes.ApplicantFamilyNotFound);
			var primaryGuardian = existingFamily.PrimaryGuardianId.HasValue
				? await guardianRepository.FindAsync(existingFamily.PrimaryGuardianId.Value)
				: null;
			if (input.ExistingParentApplicantId.HasValue)
			{
				if (!CurrentUser.Id.HasValue) throw new BusinessException(AdmissionErrorCodes.InquiryFamilyMismatch);
				selectedParent = await familyMatchService.ResolveParentAsync(existingFamily.Id, input.ExistingParentApplicantId.Value,
					input.ParentRelationship, phone, input.GuardianEmail, AdmissionErrorCodes.InquiryFamilyMismatch);
			}
			if (selectedParent == null && (primaryGuardian == null || primaryGuardian.FamilyId != existingFamily.Id ||
				primaryGuardian.Mobile != phone))
				throw new BusinessException(AdmissionErrorCodes.InquiryFamilyMismatch,
					"The selected family's primary guardian does not match this mobile number.");
			if (selectedParent == null) await familyMatchService.EnsureCanLinkAsync(existingFamily.Id, phone,
				AdmissionErrorCodes.InquiryFamilyMismatch);
		}
		if (input.ParentRelationship != null && existingFamily == null && !isPublic)
		{
			var matches = await familyMatchService.FindAccessibleMatchesAsync(phone, input.GuardianEmail);
			if (matches.Count > 0 && !input.CreateSeparateFamily) throw new BusinessException(AdmissionErrorCodes.InquiryDuplicate);
			if (matches.Count > 0 && string.IsNullOrWhiteSpace(input.DuplicateOverrideReason))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.DuplicateReasonRequired);
		}
		var referenceYear = Clock.Now.ToUniversalTime().Year;
		var inquiryNumber = AdmissionReferenceNumber.Format(
			AdmissionReferenceKind.Inquiry,
			referenceYear,
			await referenceSequenceRepository.IncrementAndGetAsync(referenceYear, AdmissionReferenceKind.Inquiry));
		var familyNumber = input.ExistingFamilyId.HasValue
			? null
			: AdmissionReferenceNumber.Format(
				AdmissionReferenceKind.Family,
				referenceYear,
				await referenceSequenceRepository.IncrementAndGetAsync(referenceYear, AdmissionReferenceKind.Family));
		var family = existingFamily;
		family ??= new AdmissionFamily(GuidGenerator.Create(), CurrentTenant.Id, familyNumber!);
		var guardian = family.PrimaryGuardianId.HasValue ? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var newGuardian = guardian == null;
		if (newGuardian) { guardian = new AdmissionGuardian(GuidGenerator.Create(), CurrentTenant.Id, family.Id, input.GuardianFirstName, input.GuardianLastName, input.GuardianPhone, input.GuardianEmail, AdmissionGuardianRelationship.Parent, true); family.SetPrimaryGuardian(guardian.Id); }
		var applicant = new AdmissionApplicant(GuidGenerator.Create(), CurrentTenant.Id, family.Id, input.ApplicantFirstName, returningStudent?.MiddleName, input.ApplicantLastName, input.ApplicantDateOfBirth, returningStudent?.AdditionalDetail?.Gender?.ToString(), returningStudent?.Id, GuidGenerator.Create());
		foreach (var field in inquiryFields.Where(field =>
			field.BindingType == AdmissionFormBindingType.System &&
			!string.IsNullOrWhiteSpace(field.BindingKey) &&
			values.TryGetValue(field.Code, out var value) &&
			!string.IsNullOrWhiteSpace(value)))
		{
			// A return captures fresh answers without modifying shared family or permanent identity.
			if ((returningStudent != null || selectedParent != null) && field.BindingKey is "Guardian.FirstName" or "Guardian.LastName" or "Guardian.Mobile" or "Guardian.Email") continue;
			if (field.BindingKey == "Family.SiblingStudentIds")
			{
				var siblingIds = AdmissionStudentProfileMapping.ReadSiblings(values[field.Code]) ?? [];
				if (siblingIds.Contains(returningStudent?.Id ?? applicant.ReservedStudentId))
					throw new BusinessException("Admissions:InvalidSiblingSelection");
				await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>().EnsureCanAccessStudentsAsync(siblingIds, nameof(CreateAsync));
			}
			AdmissionFormBindingRegistry.ApplyValue(field.BindingKey!, values[field.Code], applicant, guardian);
		}
		if (selectedParent != null)
		{
			AdmissionSiblingParentSelection.Confirm(applicant, selectedParent, CurrentUser.Id!.Value, Clock.Now.ToUniversalTime(), phone, input.GuardianEmail);
			var prefix = selectedParent.Relationship == "father" ? "Father" : "Mother";
			foreach (var field in inquiryFields.Where(field => field.BindingKey == prefix + ".Name" || field.BindingKey == prefix + ".Mobile" || field.BindingKey == prefix + ".Email"))
			{
				var answer = input.FormValues.FirstOrDefault(value => string.Equals(value.FieldCode, field.Code, StringComparison.OrdinalIgnoreCase));
				if (answer != null) answer.ValueJson = JsonSerializer.Serialize(AdmissionStudentProfileMapping.Read(applicant, field.BindingKey!));
			}
		}
		else if (input.ParentRelationship != null)
		{
			var prefix = input.ParentRelationship == "father" ? "Father" : "Mother";
			AdmissionSiblingParentSelection.CaptureParent(applicant, input.ParentRelationship,
				AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name") ?? string.Join(" ", new[] { input.GuardianFirstName, input.GuardianLastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
				AdmissionStudentProfileMapping.Read(applicant, prefix + ".Mobile") ?? phone,
				AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email") ?? input.GuardianEmail);
			AdmissionFirstParentContactMapping.Apply(applicant, inquiryFields
				.Where(field => field.BindingType == AdmissionFormBindingType.System && field.BindingKey != null && values.ContainsKey(field.Code))
				.GroupBy(field => field.BindingKey!, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => values[group.Last().Code], StringComparer.OrdinalIgnoreCase), guardian);
		}
		AdmissionFamilyContactCapture.Capture(applicant, input.FamilyContacts, input.ParentRelationship);
		if (!input.ExistingFamilyId.HasValue) await familyRepository.InsertAsync(family, autoSave: false);
		if (newGuardian) await guardianRepository.InsertAsync(guardian!, autoSave: false);
		await applicantRepository.InsertAsync(applicant, autoSave: false);
		var inquiry = new AdmissionInquiry(GuidGenerator.Create(), CurrentTenant.Id, inquiryNumber, cycle.Id, offering?.Id,
			input.ApplicantName, phone, input.Email, input.Source, input.SourceDetail);
		inquiry.SetPreferredLanguage(input.PreferredLanguageCode);
		inquiry.LinkIdentity(family.Id, applicant.Id);
		AdmissionFamilyContactCapture.CopyToInquiry(applicant, inquiry);
		if (inquiryFormId.HasValue)
		{
			var snapshot = input.FormValues
				.Join(inquiryFields, value => value.FieldCode, field => field.Code,
					(value, field) => new
					{
						Key = string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!,
						value.ValueJson
					}, StringComparer.OrdinalIgnoreCase)
				.GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.Last().ValueJson,
					StringComparer.OrdinalIgnoreCase);
			inquiry.RecordFormResponseSnapshot(JsonSerializer.Serialize(snapshot), inquiryFormId);
		}
		await inquiryRepository.InsertAsync(inquiry, autoSave: false);
		await inquiryRuntime.EnsureManagementAsync(inquiry);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryCreated", "Inquiry created", applicant.Id, null, inquiry.Id,
			JsonSerializer.Serialize(new { Managed = managedPolicy != null, IsPublic = isPublic, input.DuplicateOverrideReason, input.ExistingParentApplicantId, input.ParentRelationship })), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> ConfirmOfferingAsync(Guid id, ConfirmAdmissionInquiryOfferingDto input)
	{
		var inquiry = await GetInquiryAsync(id);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) ||
			!string.Equals(inquiry.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		if (inquiry.AdmissionOfferingId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState,
				"This inquiry already has a confirmed offering.");
		var offering = (await offeringRuntimeService.GetAvailableAsync(input.OfferingId)).Offering;
		await inquiryAvailability.EnsureAvailableAsync(offering);
		await scopeAccess.EnsureOfferingAccessAsync(offering.Id);
		if (offering.AdmissionCycleId != inquiry.AdmissionCycleId ||
			offering.EntryMode is not (AdmissionEntryMode.Inquiry or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.InquiryEntryNotAllowed);
		var policyContext = await policyResolver.ResolveAsync(offering.Id);
		if (policyContext != null)
		{
			var policy = policyContext.Policy;
			if (policy.AllowedSources.Count > 0 && !policy.AllowedSources.Contains(inquiry.Source))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.SourceNotAllowed);
		}
		var previousCounsellorId = inquiry.AssignedCounsellorId;
		inquiry.ConfirmOffering(offering.Id);

		await inquiryRepository.UpdateAsync(inquiry, false);
		await inquiryRuntime.EnsureManagementAsync(inquiry);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryOfferingConfirmed", "Admission offering confirmed after inquiry capture",
			inquiry.ApplicantId, null, inquiry.Id,
			JsonSerializer.Serialize(new { OfferingId = offering.Id, PreviousCounsellorId = previousCounsellorId,
				inquiry.AssignedCounsellorId,
				ConfirmedBy = CurrentUser.Id })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> UpdateAsync(Guid id, UpdateAdmissionInquiryDto input)
	{
		var inquiry = await GetInquiryAsync(id);
		if (AdmissionInquiryStatusRules.IsTransferred(inquiry.Status) || await applicationRepository.AnyAsync(application => application.InquiryId == inquiry.Id))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState,
				"Inquiry details are locked after the application has started.");
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) ||
			!string.Equals(inquiry.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict,
				"This inquiry changed after it was opened. Reload it and try again.");
		if (!Enum.IsDefined(input.Source))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid inquiry source.");
		var offeringId = input.AdmissionOfferingId ?? inquiry.AdmissionOfferingId ?? throw new BusinessException(AdmissionErrorCodes.InquiryOfferingRequired);
		if (input.Status.HasValue) AdmissionInquiryRuntimeService.EnsureValidStatusChange(inquiry, input.Status.Value, input.StatusReason);
		var offering = offeringId != inquiry.AdmissionOfferingId
			? (await offeringRuntimeService.GetAvailableAsync(offeringId)).Offering : await offeringRepository.GetAsync(offeringId);
		if (offeringId == inquiry.AdmissionOfferingId && (input.BoardId.HasValue || input.MediumId.HasValue || input.StandardId.HasValue))
			AdmissionInquiryAcademicSelectionValidator.EnsureMatches(new CreateAdmissionInquiryDto
			{
				AdmissionCycleId = inquiry.AdmissionCycleId, BoardId = input.BoardId ?? offering.BoardId,
				MediumId = input.MediumId ?? offering.MediumId, StandardId = input.StandardId ?? offering.StandardId
			}, offering);
		if (offeringId != inquiry.AdmissionOfferingId)
		{
			await inquiryAvailability.EnsureAvailableAsync(offering);
			await scopeAccess.EnsureOfferingAccessAsync(offering.Id);
			if (offering.AdmissionCycleId != inquiry.AdmissionCycleId || offering.EntryMode == AdmissionEntryMode.DirectRegistration)
				throw new BusinessException(AdmissionErrorCodes.InquiryEntryNotAllowed);
			var newPolicy = await policyResolver.ResolveAsync(offering.Id);
			if (newPolicy?.Policy.AllowedSources.Count > 0 && !newPolicy.Policy.AllowedSources.Contains(input.Source))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.SourceNotAllowed);
			AdmissionInquiryAcademicSelectionValidator.EnsureMatches(new CreateAdmissionInquiryDto
			{
				AdmissionCycleId = inquiry.AdmissionCycleId, BoardId = input.BoardId ?? Guid.Empty,
				MediumId = input.MediumId ?? Guid.Empty, StandardId = input.StandardId ?? Guid.Empty
			}, offering);
		}
		await formEditService.ApplyAsync(inquiry, input, offering);
		var normalizedPhone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(input.Phone);
		var duplicate = await AsyncExecuter.AnyAsync((await inquiryRepository.GetQueryableAsync()).Where(candidate =>
			candidate.Id != inquiry.Id && candidate.AdmissionCycleId == inquiry.AdmissionCycleId &&
			candidate.NormalizedPhone == normalizedPhone));
		if (duplicate && normalizedPhone != inquiry.NormalizedPhone)
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicate,
				"Another inquiry in this admission cycle already uses this mobile number.");
		var before = new
		{
			inquiry.ApplicantName,
			inquiry.NormalizedPhone,
			inquiry.Email,
			inquiry.Source,
			inquiry.SourceDetail,
			inquiry.PreferredLanguageCode
		};
		inquiry.UpdateDetails(input.ApplicantName, normalizedPhone, input.Email, input.Source,
			input.SourceDetail, input.PreferredLanguageCode);
		if (offering.Id != inquiry.AdmissionOfferingId)
		{
			inquiry.ConfirmOffering(offering.Id);
			await inquiryRuntime.RefreshPolicyAsync(inquiry);
		}
		if (input.Status.HasValue)
		{
			var previousStatus = inquiry.Status;
			await inquiryRuntime.UpdateStatusAsync(inquiry, input.Status.Value, input.StatusReason);
			if (previousStatus != inquiry.Status)
				await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryStatusChanged", $"Inquiry status changed to {(input.Status == AdmissionInquiryStatus.New ? "Pending" : input.Status == AdmissionInquiryStatus.Interested ? "Interested" : "Not interested")}", inquiry.ApplicantId, null, inquiry.Id,
					JsonSerializer.Serialize(new { PreviousStatus = previousStatus, Status = input.Status, input.StatusReason, Actor = CurrentUser.Id })), false);
		}
		await inquiryRepository.UpdateAsync(inquiry, autoSave: false);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryDetailsUpdated", "Inquiry details updated", inquiry.ApplicantId, null, inquiry.Id,
			JsonSerializer.Serialize(new { Before = before, After = input, Actor = CurrentUser.Id })), autoSave: true);
		return await GetAsync(inquiry.Id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> SetStatusAsync(Guid id, AdmissionInquiryStatus status, Guid? duplicateOfId = null)
	{
		var inquiry = await GetInquiryAsync(id);
		if (await applicationRepository.AnyAsync(application => application.InquiryId == inquiry.Id))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		await inquiryRuntime.UpdateStatusAsync(inquiry, status, null);
		await inquiryRepository.UpdateAsync(inquiry, autoSave: true);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryStatusChanged", $"Inquiry status changed to {status}", inquiry.ApplicantId, null, inquiry.Id), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> MarkContactedAsync(Guid id)
	{
		var inquiry = await GetInquiryAsync(id);
		var contactedAt = Clock.Now;
		inquiry.MarkContacted(contactedAt);
		if (await inquiryRuntime.EnsureManagementAsync(inquiry) is { } state)
		{
			state.RecordContact(contactedAt);
		}
		await inquiryRepository.UpdateAsync(inquiry, autoSave: true);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("InquiryContacted", "Inquiry marked as contacted", inquiry.ApplicantId, null, inquiry.Id), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<System.Collections.Generic.List<AdmissionInquiryDuplicateMatchDto>> DuplicateCheckAsync(AdmissionInquiryDuplicateCheckInput input)
	{
		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		if (!await AsyncExecuter.AnyAsync(scopedOfferings.Where(offering =>
			offering.AdmissionCycleId == input.AdmissionCycleId &&
			(offering.EntryMode == AdmissionEntryMode.Inquiry || offering.EntryMode == AdmissionEntryMode.Both))))
			throw new EntityNotFoundException(typeof(AdmissionCycle), input.AdmissionCycleId);
		var phone = string.Concat((input.GuardianPhone ?? string.Empty).Where(char.IsDigit));
		var parentMatches = await familyMatchService.FindAccessibleMatchesAsync(phone, input.GuardianEmail, input.ParentContacts);
		var guardians = await guardianRepository.GetListAsync(x => x.Mobile == phone || (!string.IsNullOrWhiteSpace(input.GuardianEmail) && x.Email == input.GuardianEmail!.Trim().ToLowerInvariant()));
		var applicants = await applicantRepository.GetListAsync(x => (!string.IsNullOrWhiteSpace(input.ApplicantName) && (x.FirstName + " " + (x.LastName ?? string.Empty)).Trim().Contains(input.ApplicantName)) && (!input.ApplicantDateOfBirth.HasValue || x.DateOfBirth == input.ApplicantDateOfBirth.Value.Date));
		var familyIds = guardians.Select(x => x.FamilyId).Concat(applicants.Select(x => x.FamilyId)).Concat(parentMatches.Select(match => match.FamilyId)).Distinct().ToList();
		if (familyIds.Count == 0) return [];
		var familyApplicants = await applicantRepository.GetListAsync(x => familyIds.Contains(x.FamilyId));
		if (familyApplicants.Count == 0) return [];
		var applicantIds = familyApplicants.Select(x => x.Id).ToList();
		var scopedInquiries = await AsyncExecuter.ToListAsync((await scopeAccess.GetScopedQueryAsync())
			.Where(x => x.ApplicantId.HasValue && applicantIds.Contains(x.ApplicantId.Value)));
		var candidateApplications = await AsyncExecuter.ToListAsync(
			(await applicationScope.GetScopedApplicationsAsync()).Where(application =>
				applicantIds.Contains(application.ApplicantId))
			.Select(application => new { application.ApplicantId, application.AdmissionOfferingId }));
		var candidateOfferingIds = candidateApplications.Select(application => application.AdmissionOfferingId)
			.Distinct().ToList();
		var visibleOfferingIds = candidateOfferingIds.Count == 0 ? new List<Guid>() : await AsyncExecuter.ToListAsync(
			scopedOfferings.Where(offering => candidateOfferingIds.Contains(offering.Id))
				.Select(offering => offering.Id));
		var visibleOfferingIdSet = visibleOfferingIds.ToHashSet();
		var scopedApplicationApplicantIds = candidateApplications.Where(application =>
			visibleOfferingIdSet.Contains(application.AdmissionOfferingId))
			.Select(application => application.ApplicantId);
		var visibleApplicantIds = scopedInquiries.Select(x => x.ApplicantId!.Value)
			.Concat(scopedApplicationApplicantIds).ToHashSet();
		var currentCycleApplicantIds = scopedInquiries.Where(x => x.AdmissionCycleId == input.AdmissionCycleId)
			.Select(x => x.ApplicantId!.Value).ToHashSet();
		var matchingPhoneFamilyIds = guardians.Where(x => x.Mobile == phone)
			.Select(x => x.FamilyId).Concat(parentMatches.Select(match => match.FamilyId)).ToHashSet();
		var allApplicants = familyApplicants.Where(x => visibleApplicantIds.Contains(x.Id) &&
			(currentCycleApplicantIds.Contains(x.Id) || matchingPhoneFamilyIds.Contains(x.FamilyId))).ToList();
		var familyGuardians = await guardianRepository.GetListAsync(x => familyIds.Contains(x.FamilyId) && x.IsPrimaryContact);
		var activeFamilies = (await familyRepository.GetListAsync(x =>
			familyIds.Contains(x.Id) && x.Status == AdmissionFamilyStatus.Active))
			.ToDictionary(x => x.Id);
		return allApplicants.Select(x =>
		{
			var guardian = familyGuardians.FirstOrDefault(g => g.FamilyId == x.FamilyId);
			var parentMatch = parentMatches.FirstOrDefault(match => match.FamilyId == x.FamilyId);
			var canLinkFamily = activeFamilies.TryGetValue(x.FamilyId, out var family) &&
				(parentMatch?.Parents.Count > 0 || guardian != null && family!.PrimaryGuardianId == guardian.Id && guardian.Mobile == phone);
			var existingInquiry = scopedInquiries.OrderByDescending(item => item.CreationTime)
				.FirstOrDefault(item => item.ApplicantId == x.Id);
			return new AdmissionInquiryDuplicateMatchDto
			{
				ApplicantId = x.Id, FamilyId = x.FamilyId, ExistingInquiryId = existingInquiry?.Id,
				ExistingInquiryNumber = existingInquiry?.InquiryNumber,
				ApplicantName = string.Join(" ", new[] { x.FirstName, x.LastName }
					.Where(y => !string.IsNullOrWhiteSpace(y))),
				GuardianPhone = guardian?.Mobile, GuardianEmail = guardian?.Email, DateOfBirth = x.DateOfBirth,
				CanLinkFamily = canLinkFamily, Parents = parentMatch?.Parents ?? [],
				MatchByEmail = parentMatch?.MatchByEmail ?? false, MatchByMobile = parentMatch?.MatchByMobile ?? false
			};
		}).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public Task<List<AdmissionInquiryCounsellorDto>> GetAssignableCounsellorsAsync(
		Guid? admissionOfferingId = null,
		string? filter = null)
	{
		return Task.FromResult(new List<AdmissionInquiryCounsellorDto>());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public Task<AdmissionInquiryDto> AssignCounsellorAsync(Guid id, AssignAdmissionCounsellorDto input)
	{
		throw new Volo.Abp.BusinessException("Admissions.StaffAssignmentRetired", "Staff handling is recorded automatically in the activity history.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public Task<BulkAdmissionInquiryResultDto> BulkAssignCounsellorAsync(BulkAssignAdmissionInquiriesDto input)
	{
		throw new Volo.Abp.BusinessException("Admissions.StaffAssignmentRetired", "Staff handling is recorded automatically in the activity history.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryCommunicationResultDto> CreateCommunicationAsync(
		Guid id,
		CreateAdmissionInquiryCommunicationDto input)
	{
		var inquiry = await GetInquiryAsync(id);
		var title = input.Title?.Trim() ?? string.Empty;
		var schoolContact = AdmissionFamilyContactCapture.ReadMainContact(inquiry);
		var recipientEmail = schoolContact != null ? schoolContact.Email : inquiry.Email;
		var recipientMobile = schoolContact != null ? schoolContact.Mobile : inquiry.NormalizedPhone;
		var subject = input.Subject?.Trim();
		var body = input.Body?.Trim() ?? string.Empty;
		var channels = (input.Channels ?? []).Distinct().ToList();

		if (title.Length is 0 or > 256 || body.Length is 0 or > 4000 || subject?.Length > 256)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Enter a title and message within the allowed length.");
		if (!Enum.IsDefined(input.ImportanceLevel))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid communication importance level.");
		if (channels.Count == 0 || channels.Any(channel =>
			channel is not (CommunicationChannelType.WhatsApp or CommunicationChannelType.Sms or CommunicationChannelType.Email)))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose at least one family communication channel: WhatsApp, SMS, or Email.");
		if (channels.Contains(CommunicationChannelType.Email) && string.IsNullOrWhiteSpace(recipientEmail))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Add an email address to this inquiry before selecting Email.");
		if (channels.Contains(CommunicationChannelType.Email) && string.IsNullOrWhiteSpace(subject))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Enter an email subject before selecting Email.");
		if (channels.Any(channel => channel is CommunicationChannelType.WhatsApp or CommunicationChannelType.Sms) &&
			string.IsNullOrWhiteSpace(recipientMobile))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Add a mobile number to this inquiry before selecting WhatsApp or SMS.");

		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Communication.Campaigns.Add);
		if (input.PublishImmediately)
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Communication.Campaigns.Send);

		var cycle = await cycleRepository.GetAsync(inquiry.AdmissionCycleId);
		var campaign = await communicationService.CreateCampaignAsync(new CreateUpdateCommunicationCampaignDto
		{
			AcademicYearId = cycle.AcademicYearId,
			SourceModule = CommunicationSourceModule.Student,
			SourceEntityType = "AdmissionInquiry",
			SourceEntityId = inquiry.Id,
			SourceReference = inquiry.InquiryNumber,
			CampaignType = "Notification",
			Title = title,
			Subject = string.IsNullOrWhiteSpace(subject) ? null : subject,
			Body = body,
			ImportanceLevel = input.ImportanceLevel,
			AudienceDefinition = new CommunicationAudienceDefinitionDto
			{
				ExplicitRecipients =
				[
					new CommunicationExplicitRecipientDto
					{
						RecipientType = "AdmissionInquiry",
						RecipientId = inquiry.ApplicantId ?? inquiry.Id,
						DisplayName = schoolContact?.FullName ?? inquiry.ApplicantName,
						Email = recipientEmail,
						MobileNumber = recipientMobile,
						PreferredLanguageCode = inquiry.PreferredLanguageCode,
						IsParentDefault = true
					}
				]
			},
			Channels = channels,
			FallbackOrder = channels,
			LanguageCode = inquiry.PreferredLanguageCode,
			RequiresAcknowledgement = input.RequiresAcknowledgement
		});

		string? deliveryErrorCode = null;
		var message = campaign.Status == CommunicationCampaignStatus.PendingApproval
			? "Communication created and sent for approval."
			: "Communication draft created.";
		if (input.PublishImmediately && campaign.Status != CommunicationCampaignStatus.PendingApproval)
		{
			try
			{
				campaign = await communicationService.PublishCampaignAsync(campaign.Id);
				(message, deliveryErrorCode) = campaign.Status switch
				{
					CommunicationCampaignStatus.PartiallyFailed =>
						("Communication delivery started, but one or more channel attempts failed or were skipped.",
							"Communication:DeliveryPartiallyFailed"),
					CommunicationCampaignStatus.Failed =>
						("Communication was published, but delivery failed or was skipped. Review recipient consent and channel destinations.",
							"Communication:DeliveryFailed"),
					_ => ("Communication delivery started.", null)
				};
			}
			catch (BusinessException exception)
			{
				deliveryErrorCode = exception.Code;
				message = "Communication draft created, but delivery could not start. Review provider, consent, and approval settings.";
			}
		}

		var deliveryStarted = campaign.PublishedAt.HasValue || campaign.Status is
			CommunicationCampaignStatus.Queued or
			CommunicationCampaignStatus.Sending or
			CommunicationCampaignStatus.Sent or
			CommunicationCampaignStatus.PartiallyFailed or
			CommunicationCampaignStatus.Failed;
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(deliveryStarted ? "InquiryCommunicationPublished" : "InquiryCommunicationDraftCreated",
			message,
			inquiry.ApplicantId,
			null,
			inquiry.Id,
			JsonSerializer.Serialize(new
			{
				CampaignId = campaign.Id,
				campaign.Status,
				Channels = channels,
				DeliveryErrorCode = deliveryErrorCode
			})), autoSave: true);

		return new AdmissionInquiryCommunicationResultDto
		{
			CampaignId = campaign.Id,
			Status = campaign.Status,
			DeliveryStarted = deliveryStarted,
			RecipientCount = campaign.RecipientCount,
			EligibleRecipientCount = campaign.EligibleRecipientCount,
			BlockedRecipientCount = campaign.BlockedRecipientCount,
			Message = message,
			DeliveryErrorCode = deliveryErrorCode
		};
	}

	private Task<AdmissionInquiry> GetInquiryAsync(Guid id) => scopeAccess.GetAsync(id);
	private async Task<IQueryable<AdmissionInquiry>> BuildFilteredQueryAsync(AdmissionInquiryListInput input)
	{
		var query = await scopeAccess.GetScopedQueryAsync();
		if (input.AcademicYearId.HasValue)
		{
			var cycles = await cycleRepository.GetQueryableAsync();
			query = query.Where(inquiry => cycles.Any(cycle => cycle.Id == inquiry.AdmissionCycleId &&
				cycle.AcademicYearId == input.AcademicYearId.Value));
		}
		if (input.BoardId.HasValue || input.MediumId.HasValue || input.StandardId.HasValue || input.StandardIds.Count > 0)
		{
			var offerings = await offeringRepository.GetQueryableAsync();
			query = query.Where(inquiry => inquiry.AdmissionOfferingId.HasValue &&
				offerings.Any(offering => offering.Id == inquiry.AdmissionOfferingId.Value &&
					(!input.BoardId.HasValue || offering.BoardId == input.BoardId.Value) &&
					(!input.MediumId.HasValue || offering.MediumId == input.MediumId.Value) &&
					(!input.StandardId.HasValue || offering.StandardId == input.StandardId.Value) &&
					(input.StandardIds.Count == 0 || Enumerable.Contains(input.StandardIds, offering.StandardId))));
		}
		if (!string.IsNullOrWhiteSpace(input.StandardName))
		{
			var classQuery = AdmissionQueueColumnFilters.Text(await standardRepository.GetQueryableAsync(), standard => standard.Name, input.StandardName, NormalizeTextFilterType(input.StandardNameFilterType));
			var offeringQuery = await offeringRepository.GetQueryableAsync();
			query = query.Where(inquiry => inquiry.AdmissionOfferingId.HasValue && offeringQuery.Any(offering => offering.Id == inquiry.AdmissionOfferingId && classQuery.Any(standard => standard.Id == offering.StandardId)));
		}
		var managementQuery = await managementRepository.GetQueryableAsync();
		var followUpQuery = await followUpRepository.GetQueryableAsync();
		var now = Clock.Now.ToUniversalTime();
		var normalizedSearch = input.Search?.Trim();
		var pipelineGroup = NormalizePipelineGroup(input.PipelineGroup);
		var applications = await applicationRepository.GetQueryableAsync();
		if (input.IsTransferred.HasValue || input.ExcludePostApplication || pipelineGroup != null)
		{
			var transferred = input.IsTransferred ?? (!input.ExcludePostApplication && pipelineGroup == "handoff");
			query = transferred
				? query.Where(inquiry => inquiry.Status == AdmissionInquiryStatus.ApplicationStarted || inquiry.Status == AdmissionInquiryStatus.Converted || applications.Any(application => application.InquiryId == inquiry.Id))
				: query.Where(inquiry => inquiry.Status != AdmissionInquiryStatus.ApplicationStarted && inquiry.Status != AdmissionInquiryStatus.Converted && !applications.Any(application => application.InquiryId == inquiry.Id));
		}
		if (input.Status.HasValue)
		{
			query = input.Status.Value switch
			{
				AdmissionInquiryStatus.New => query.Where(inquiry => inquiry.Status == AdmissionInquiryStatus.New || inquiry.Status == AdmissionInquiryStatus.Contacted || inquiry.Status == AdmissionInquiryStatus.FollowUp),
				AdmissionInquiryStatus.Interested => query.Where(inquiry => inquiry.Status == AdmissionInquiryStatus.Interested || inquiry.Status == AdmissionInquiryStatus.ApplicationStarted || inquiry.Status == AdmissionInquiryStatus.Converted),
				AdmissionInquiryStatus.NotInterested => query.Where(inquiry => inquiry.Status == AdmissionInquiryStatus.NotInterested || inquiry.Status == AdmissionInquiryStatus.Lost || inquiry.Status == AdmissionInquiryStatus.Duplicate),
				_ => query.Where(inquiry => inquiry.Status == input.Status.Value)
			};
		}
		query = query.Where(x => (!input.AdmissionCycleId.HasValue || x.AdmissionCycleId == input.AdmissionCycleId) &&
			(!input.AdmissionOfferingId.HasValue || x.AdmissionOfferingId == input.AdmissionOfferingId) &&
			(!input.ExcludePostApplication ||
				(x.Status != AdmissionInquiryStatus.ApplicationStarted && x.Status != AdmissionInquiryStatus.Converted)) &&
			(!input.Source.HasValue || x.Source == input.Source) &&
			(string.IsNullOrWhiteSpace(normalizedSearch) || x.ApplicantName.Contains(normalizedSearch) ||
				x.NormalizedPhone.Contains(normalizedSearch) || x.InquiryNumber.Contains(normalizedSearch) ||
				(x.Email != null && x.Email.Contains(normalizedSearch))));
		var inquiryNumber = input.InquiryNumber?.Trim();
		if (!string.IsNullOrWhiteSpace(inquiryNumber))
		{
			query = NormalizeTextFilterType(input.InquiryNumberFilterType) switch
			{
				"equals" => query.Where(x => x.InquiryNumber == inquiryNumber),
				"startsWith" => query.Where(x => x.InquiryNumber.StartsWith(inquiryNumber)),
				"endsWith" => query.Where(x => x.InquiryNumber.EndsWith(inquiryNumber)),
				_ => query.Where(x => x.InquiryNumber.Contains(inquiryNumber))
			};
		}
		var applicantName = input.ApplicantName?.Trim();
		if (!string.IsNullOrWhiteSpace(applicantName))
		{
			query = NormalizeTextFilterType(input.ApplicantNameFilterType) switch
			{
				"equals" => query.Where(x => x.ApplicantName == applicantName),
				"startsWith" => query.Where(x => x.ApplicantName.StartsWith(applicantName)),
				"endsWith" => query.Where(x => x.ApplicantName.EndsWith(applicantName)),
				_ => query.Where(x => x.ApplicantName.Contains(applicantName))
			};
		}
		var phone = string.Concat((input.Phone ?? string.Empty).Where(char.IsDigit));
		if (!string.IsNullOrWhiteSpace(phone))
		{
			query = NormalizeTextFilterType(input.PhoneFilterType) switch
			{
				"equals" => query.Where(x => x.NormalizedPhone == phone),
				"startsWith" => query.Where(x => x.NormalizedPhone.StartsWith(phone)),
				"endsWith" => query.Where(x => x.NormalizedPhone.EndsWith(phone)),
				_ => query.Where(x => x.NormalizedPhone.Contains(phone))
			};
		}
		var email = input.Email?.Trim();
		if (!string.IsNullOrWhiteSpace(email))
		{
			query = NormalizeTextFilterType(input.EmailFilterType) switch
			{
				"equals" => query.Where(x => x.Email == email),
				"startsWith" => query.Where(x => x.Email != null && x.Email.StartsWith(email)),
				"endsWith" => query.Where(x => x.Email != null && x.Email.EndsWith(email)),
				_ => query.Where(x => x.Email != null && x.Email.Contains(email))
			};
		}
		if (input.DashboardFollowUpsDue) query = query.Where(inquiry => followUpQuery.Any(f => f.InquiryId == inquiry.Id && f.Status == AdmissionInquiryFollowUpStatus.Scheduled && f.ScheduledAt <= now));
		if (input.DivisionId.HasValue)
		{
			var cases = await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingCase, Guid>>().GetQueryableAsync();
			query = query.Where(inquiry => applications.Any(a => a.InquiryId == inquiry.Id && cases.Any(c => c.ApplicationId == a.Id && c.DivisionId == input.DivisionId)));
		}
		if (input.WorkQueue.HasValue && !Enum.IsDefined(input.WorkQueue.Value))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid Inquiry work queue.");
		return input.WorkQueue switch
		{
			AdmissionInquiryWorkQueue.Overdue => query.Where(inquiry =>
				managementQuery.Any(state => state.InquiryId == inquiry.Id &&
					((!state.LastContactAt.HasValue && state.FirstContactDueAt < now) ||
					 (!state.Outcome.HasValue && state.QualificationDueAt < now))) ||
				followUpQuery.Any(followUp => followUp.InquiryId == inquiry.Id &&
					followUp.Status == AdmissionInquiryFollowUpStatus.Scheduled && followUp.ScheduledAt < now)),
			AdmissionInquiryWorkQueue.NeedsFirstContact => query.Where(inquiry =>
				managementQuery.Any(state => state.InquiryId == inquiry.Id && !state.LastContactAt.HasValue)),
			AdmissionInquiryWorkQueue.NeedsQualification => query.Where(inquiry =>
				managementQuery.Any(state => state.InquiryId == inquiry.Id && !state.Outcome.HasValue)),
			AdmissionInquiryWorkQueue.OpenFollowUps => query.Where(inquiry =>
				followUpQuery.Any(followUp => followUp.InquiryId == inquiry.Id &&
					followUp.Status == AdmissionInquiryFollowUpStatus.Scheduled)),
			_ => query
		};
	}

	private static string NormalizeTextFilterType(string? value) => value?.Trim() switch
	{
		null or "" => "contains",
		"contains" or "equals" or "startsWith" or "endsWith" => value.Trim(),
		_ => throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
			"Choose a valid Inquiry column filter type.")
	};
	private static string? NormalizePipelineGroup(string? value) => value?.Trim().ToLowerInvariant() switch
	{
		null or "" => null,
		"active" => "active",
		"handoff" => "handoff",
		_ => throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
			"Choose a valid Inquiry pipeline group.")
	};
	private static AdmissionInquiryDto Map(AdmissionInquiry x, AdmissionInquiryManagement? management = null,
		IReadOnlyList<AdmissionInquiryFollowUp>? openFollowUps = null, string? cycleName = null,
		string? offeringCode = null, string? counsellorName = null, DateTime? now = null)
	{
		var currentTime = now ?? DateTime.UtcNow;
		openFollowUps ??= [];
		var firstContactOverdue = management != null && !management.LastContactAt.HasValue &&
			management.FirstContactDueAt < currentTime;
		var qualificationOverdue = management != null && !management.Outcome.HasValue &&
			management.QualificationDueAt < currentTime;
		return new AdmissionInquiryDto
		{
		MainSchoolContact = AdmissionFamilyContactCapture.ReadMainContact(x),
		Id = x.Id, ConcurrencyStamp = x.ConcurrencyStamp, InquiryNumber = x.InquiryNumber, AdmissionCycleId = x.AdmissionCycleId, AdmissionOfferingId = x.AdmissionOfferingId, FamilyId = x.FamilyId, ApplicantId = x.ApplicantId, AssignedCounsellorId = null,
		AssignedCounsellorName = null, AdmissionCycleName = cycleName ?? string.Empty,
		AdmissionOfferingCode = offeringCode,
		ApplicantName = x.ApplicantName, NormalizedPhone = x.NormalizedPhone, Email = x.Email,
		PreferredLanguageCode = x.PreferredLanguageCode,
			Source = x.Source, SourceDetail = x.SourceDetail, Status = AdmissionInquiryStatusRules.Display(x.Status), LegacyStatus = x.Status,
			IsTransferred = AdmissionInquiryStatusRules.IsTransferred(x.Status), IsDuplicate = x.Status == AdmissionInquiryStatus.Duplicate || x.DuplicateOfId.HasValue,
			FormResponseDefinitionId = x.FormResponseDefinitionId, DuplicateOfId = x.DuplicateOfId,
		ContactedAt = x.ContactedAt, LastActivityAt = x.LastActivityAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId,
		LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId,
		Managed = management != null,
		NeedsFirstContact = management != null && !management.LastContactAt.HasValue,
		NeedsQualification = management != null && !management.Outcome.HasValue,
		LastContactAt = management?.LastContactAt,
		FirstContactDueAt = management?.FirstContactDueAt,
		QualificationDueAt = management?.QualificationDueAt,
		QualificationOutcome = management?.Outcome,
		NextFollowUpAt = openFollowUps.FirstOrDefault()?.ScheduledAt,
		OpenFollowUpCount = openFollowUps.Count,
		IsFirstContactOverdue = firstContactOverdue,
		IsQualificationOverdue = qualificationOverdue,
		HasOverdueFollowUp = openFollowUps.Any(followUp => followUp.ScheduledAt < currentTime)
		};
	}
	private static IQueryable<AdmissionInquiry> ApplySorting(IQueryable<AdmissionInquiry> query, string? sorting) =>
		(sorting?.Trim().ToLowerInvariant()) switch
		{
			"inquirynumber asc" => query.OrderBy(item => item.InquiryNumber),
			"inquirynumber desc" => query.OrderByDescending(item => item.InquiryNumber),
			"applicantname asc" => query.OrderBy(item => item.ApplicantName),
			"applicantname desc" => query.OrderByDescending(item => item.ApplicantName),
			"status asc" => query.OrderBy(item => item.Status).ThenByDescending(item => item.CreationTime),
			"status desc" => query.OrderByDescending(item => item.Status).ThenByDescending(item => item.CreationTime),
			"source asc" => query.OrderBy(item => item.Source).ThenByDescending(item => item.CreationTime),
			"source desc" => query.OrderByDescending(item => item.Source).ThenByDescending(item => item.CreationTime),
			"lastactivityat asc" => query.OrderBy(item => item.LastActivityAt),
			"lastactivityat desc" => query.OrderByDescending(item => item.LastActivityAt),
			"creationtime asc" => query.OrderBy(item => item.CreationTime),
			_ => query.OrderByDescending(item => item.CreationTime)
		};
	private static string Csv(string? value)
	{
		var safe = value ?? string.Empty;
		if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@') safe = $"'{safe}";
		return $"\"{safe.Replace("\"", "\"\"")}\"";
	}
	private static string DisplayInquiryAnswer(AdmissionFormFieldDefinition field, string? raw)
	{
		var value = UnwrapValue(raw);
		if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(field.OptionsJson)) return value;
		try
		{
			using var options = JsonDocument.Parse(field.OptionsJson);
			if (options.RootElement.ValueKind != JsonValueKind.Array) return value;
			var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var option in options.RootElement.EnumerateArray())
			{
				if (option.ValueKind == JsonValueKind.String)
				{
					var text = option.GetString();
					if (!string.IsNullOrWhiteSpace(text)) labels[text] = text;
				}
				else if (option.ValueKind == JsonValueKind.Object &&
					option.TryGetProperty("value", out var optionValue) &&
					optionValue.ValueKind == JsonValueKind.String)
				{
					var key = optionValue.GetString();
					if (!string.IsNullOrWhiteSpace(key))
						labels[key] = option.TryGetProperty("label", out var label) &&
							label.ValueKind == JsonValueKind.String ? label.GetString() ?? key : key;
				}
			}
			if (labels.TryGetValue(value, out var display)) return display;
			if (value.StartsWith('['))
			{
				using var selected = JsonDocument.Parse(value);
				if (selected.RootElement.ValueKind == JsonValueKind.Array)
					return string.Join(", ", selected.RootElement.EnumerateArray()
						.Select(item => item.GetString() ?? string.Empty)
						.Select(item => labels.GetValueOrDefault(item, item)));
			}
		}
		catch (JsonException) { /* Keep the stored value when legacy options are malformed. */ }
		return value;
	}
	private static string UnwrapValue(string? value) { if (string.IsNullOrWhiteSpace(value) || value == "null") return string.Empty; try { using var json = System.Text.Json.JsonDocument.Parse(value); return json.RootElement.ValueKind == System.Text.Json.JsonValueKind.String ? json.RootElement.GetString() ?? string.Empty : json.RootElement.ToString(); } catch { return value; } }
	private static bool IsVisible(string? ruleJson, IReadOnlyDictionary<string, string> values) { if (string.IsNullOrWhiteSpace(ruleJson)) return true; try { using var document = System.Text.Json.JsonDocument.Parse(ruleJson); return EvaluateRule(document.RootElement, values); } catch { return false; } }
	private static bool EvaluateRule(System.Text.Json.JsonElement rule, IReadOnlyDictionary<string, string> values) { if (rule.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == System.Text.Json.JsonValueKind.Array) { var results = conditions.EnumerateArray().Select(x => EvaluateRule(x, values)); return string.Equals(rule.GetProperty("operator").GetString(), "ANY", StringComparison.OrdinalIgnoreCase) ? results.Any(x => x) : results.All(x => x); } var field = rule.TryGetProperty("field", out var fieldProperty) ? fieldProperty.GetString() : null; var actual = field != null && values.TryGetValue(field, out var value) ? value : string.Empty; var expected = rule.TryGetProperty("value", out var expectedProperty) ? expectedProperty.ToString().Trim('"') : string.Empty; return (rule.TryGetProperty("operator", out var operatorProperty) ? operatorProperty.GetString() : string.Empty)?.ToLowerInvariant() switch { "equals" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), "isnotempty" => !string.IsNullOrWhiteSpace(actual), "isempty" => string.IsNullOrWhiteSpace(actual), "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase), "notequals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), _ => true };
}
}
