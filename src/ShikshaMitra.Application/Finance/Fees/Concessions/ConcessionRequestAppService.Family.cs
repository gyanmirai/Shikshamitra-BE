using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ShikshaMitra.Common;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(ConcessionRequestAppService), typeof(IConcessionRequestAppService), typeof(IFamilyConcessionService))]
public partial class ConcessionRequestAppService : IFamilyConcessionService
{
	private Task<MobileFinanceAccessContext> RequireFamilyAsync(Guid studentId, Guid yearId, string permission = MobileFinancePermissions.ViewConcessions) =>
		LazyServiceProvider.LazyGetRequiredService<MobileFinanceAccessService>().RequireAsync(studentId, yearId,
			guardianOnly: true, permission: permission);
	private MobileFinanceAccessService FamilyAccess => LazyServiceProvider.LazyGetRequiredService<MobileFinanceAccessService>();
	private MobileFinanceCommandStore FamilyCommands => LazyServiceProvider.LazyGetRequiredService<MobileFinanceCommandStore>();

	private async Task RequireAssignedFamilyPolicyAsync(Guid child, Guid year, MobileConcessionInput input)
	{
		var assignment = await studentFeeStructureRepository.GetAsync(input.StudentFeeStructureId);
		if (assignment.StudentId != child || assignment.AcademicYearId != year ||
			assignment.ConcessionPolicyId != input.ConcessionPolicyId ||
			assignment.ConcessionPolicyVersionId != input.ConcessionPolicyVersionId)
			throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
		if (input.FeeHeadConcessions.Count > 100 || input.DueFeeConcessions.Count > 100 || input.Documents.Count > 20)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
	}

	private static List<FeeHeadConcessionInput> FamilyHeads(MobileConcessionInput input) => input.FeeHeadConcessions
		.Select(x => new FeeHeadConcessionInput(x.FeeHeadId, (DiscountBasis)x.BasedOn, MobileFinanceCommandRules.ParseMoney(x.Amount))).ToList();
	private static List<DueFeeConcessionInput> FamilyDues(MobileConcessionInput input) => input.DueFeeConcessions
		.Select(x => new DueFeeConcessionInput(x.DueFeeId, x.FeeHeadId, MobileFinanceCommandRules.ParseMoney(x.Amount))).ToList();

	public virtual async Task<object> FamilyEligibilityAsync(Guid child, Guid year)
	{
		await RequireFamilyAsync(child, year);
		var assignments = await studentFeeStructureRepository.GetListAsync(x => x.StudentId == child && x.AcademicYearId == year);
		var types = await concessionTypeRepository.GetListAsync(x => x.AcademicYearId == year && x.IsActive);
		var results = new List<object>();
		foreach (var assignment in assignments.Where(x => x.ConcessionPolicyId.HasValue && x.ConcessionPolicyVersionId.HasValue))
		{
			var version = await financePolicyVersionRepository.FindAsync(assignment.ConcessionPolicyVersionId!.Value);
			if (version == null || version.PolicyType != "concession" || version.PolicyId != assignment.ConcessionPolicyId || version.AcademicYearId != year) continue;
			if (await financePolicyVersionRepository.FindAsync(x => x.PolicyType == "concession" && x.PolicyId == version.PolicyId && x.VersionNumber > version.VersionNumber) != null) continue;
			var policy = System.Text.Json.JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson);
			if (policy == null || policy.Status != ConcessionPolicyStatus.Active) continue;

            var assignedDues = await dueFeeRepository.GetListAsync(x => x.StudentId == child && x.StudentFeeStructureId == assignment.Id);
            var headIds = assignedDues.Select(x => x.FeeHeadId).Distinct().ToArray();
            var heads = await feeHeadRepository.GetListAsync(x => Enumerable.Contains(headIds, x.Id) && x.AcademicYearId == year && x.IsActive && x.IsConcessionApplied);
            var headNames = heads.ToDictionary(x => x.Id, x => x.Name);
            var permittedHeads = heads.Select(x => x.Id).ToHashSet();
            if (assignment.ConcessionFeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads)
                permittedHeads.IntersectWith(assignment.GetConcessionApplicableFeeHeadIds());
            var eligible = ConcessionRequestManager.GetEligibleDueFees(assignedDues.Where(x => permittedHeads.Contains(x.FeeHeadId)).ToList(), policy.ApplyOn);
			results.Add(new { StudentFeeStructureId = assignment.Id, assignment.ConcessionPolicyId, assignment.ConcessionPolicyVersionId,
				policy.PolicyName,
                EligibleDueFees = eligible.Select(x => new { DueFeeId = x.Id, x.FeeHeadId, FeeHeadName = headNames.GetValueOrDefault(x.FeeHeadId, string.Empty), Label = x.GetDisplayInstallmentLabel(),
                    Amount = MobileFinanceCommandRules.Money(ConcessionRequestManager.GetConcessionCapacity(x, policy)),
                    OutstandingAmount = MobileFinanceCommandRules.Money(x.OutstandingAmount) }).ToArray(),
                PercentageValue = policy.PercentageValue.HasValue ? MobileFinanceCommandRules.Money(policy.PercentageValue.Value) : null,
                FixedAmount = policy.FixedAmount.HasValue ? MobileFinanceCommandRules.Money(policy.FixedAmount.Value) : null,
                ValueSource = policy.ValueSource.ToString(), CalculationMethod = policy.CalculationMethod.ToString(),
				MaximumPercentage = policy.MaximumPercentage.HasValue ? MobileFinanceCommandRules.Money(policy.MaximumPercentage.Value) : null,
				MaximumFixedAmount = policy.MaximumFixedAmount.HasValue ? MobileFinanceCommandRules.Money(policy.MaximumFixedAmount.Value) : null,
				ConcessionTypes = types.Where(x => (policy.ApplicableConcessionTypeIds?.Count ?? 0) == 0 || policy.ApplicableConcessionTypeIds!.Contains(x.Id))
				.Select(x => new { x.Id, x.Name, Documents = x.ConcessionTypeDocuments.Select(d => new { DocumentId = d.Id,
					Name = d.DocumentName, d.IsRequired, MaxFileSizeBytes = Math.Min(d.MaxFileSizeBytes, 10 * 1024 * 1024),
					AcceptedFormats = d.AcceptedFormats.Select(f => f.DocumentType.ToString()).ToArray() }).ToArray() }).ToArray() });
		}
		return new { Currency = "INR", Assignments = results,
			CanCreate = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.RequestConcession),
			CanEdit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession),
			CanSubmit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession),
			CanUploadDocuments = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.UploadConcessionDocument) };
	}

	public virtual async Task<object> FamilyPreviewAsync(Guid child, Guid year, MobileConcessionInput input)
	{
		await RequireFamilyAsync(child, year);
		if (!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.RequestConcession) &&
			!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession) &&
			!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession)) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		await RequireAssignedFamilyPolicyAsync(child, year, input);
		var preview = await concessionRequestManager.PreviewAsync(CurrentTenant.Id, year, child, input.ConcessionPolicyId,
			input.ConcessionPolicyVersionId, input.ConcessionTypeId, FamilyHeads(input), input.StudentFeeStructureId, FamilyDues(input));
		return new { Currency = "INR", PreviewFingerprint = ComputePreviewFingerprint(input.ConcessionPolicyVersionId, preview.EligibleDueFees, preview.Mappings),
			Amount = MobileFinanceCommandRules.Money(preview.Mappings.Sum(x => x.FeeHeadConcessionAmount)), preview.Policy.PolicyName,
			EligibleDueFees = preview.EligibleDueFees.Select(x => new { DueFeeId = x.Id, x.FeeHeadId, Label = x.GetDisplayInstallmentLabel(),
				Amount = MobileFinanceCommandRules.Money(x.OriginalAmount), OutstandingAmount = MobileFinanceCommandRules.Money(x.OutstandingAmount) }).ToArray() };
	}

	private async Task<ConcessionRequest> RequireOwnedFamilyRequestAsync(Guid child, Guid year, Guid requestId)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(requestId);
		MobileFinanceCommandRules.RequireOwner(child, year, currentUser.GetId(), request.StudentId, request.AcademicYearId, request.CreatorId);
		return request;
	}

	private async Task<object> FamilyDetailAsync(ConcessionRequest request)
	{
		var edit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession);
		var submit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession);
		var cancel = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.CancelConcession);
		return new { Currency = "INR", Id = request.Id, Kind = "concession", Status = request.Status.ToString(),
		Amount = MobileFinanceCommandRules.Money(request.GetTotalRequestedConcession()), request.ConcurrencyStamp,
		AvailableActions = new[] { request.Status == ConcessionRequestStatus.Draft && edit ? "edit" : null,
			request.Status == ConcessionRequestStatus.Draft && submit ? "submit" : null,
			request.Status is (ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending) && cancel ? "cancel" : null }
			.Where(x => x != null).Select(x => x!).ToArray(),
		request.Remarks, request.StudentFeeStructureId, request.ConcessionPolicyId, request.ConcessionPolicyVersionId, request.ConcessionTypeId,
		FeeHeadConcessions = request.ConcessionFeeHeadMappings.Select(x => new { x.FeeHeadId, BasedOn = (int)x.UserSpecifiedConcessionAmountType,
			Amount = MobileFinanceCommandRules.Money(x.UserSpecifiedConcessionAmountValue) }).ToArray(),
		DueFeeConcessions = ConcessionRequestManager.ReadDueFeeEvidence(request).Select(x => new { x.DueFeeId, x.FeeHeadId, Amount = MobileFinanceCommandRules.Money(x.Amount) }).ToArray(),
		Documents = request.ConcessionRequestDocuments.Select(x => new { x.DocumentId, Url = BuildDocumentAccessUrl(request.Id, x.DocumentId), x.Status }).ToArray() };
	}

	public virtual async Task<object> FamilyGetAsync(Guid child, Guid year, Guid id)
	{
		await RequireFamilyAsync(child, year);
		return await FamilyDetailAsync(await RequireOwnedFamilyRequestAsync(child, year, id));
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<object> FamilySaveAsync(Guid child, Guid year, Guid? id, MobileConcessionInput input)
	{
		await RequireFamilyAsync(child, year, id.HasValue ? MobileFinancePermissions.EditConcession : MobileFinancePermissions.RequestConcession);
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var payload = new { child, year, id, input };
		var action = id.HasValue ? "concession_edit" : "concession_draft";
		var replay = await FamilyCommands.ReplayAsync(action, input.IdempotencyKey, payload);
		if (replay.HasValue) { var result = await FamilyDetailAsync(await RequireOwnedFamilyRequestAsync(child, year, replay.Value)); await uow.CompleteAsync(); return result; }
		await RequireAssignedFamilyPolicyAsync(child, year, input);
		ConcessionRequest request;
		if (id.HasValue)
		{
			request = await RequireOwnedFamilyRequestAsync(child, year, id.Value);
			EnsureConcurrencyStamp(request, input.ConcurrencyStamp ?? string.Empty);
			request.EnsureDraft();
			if (request.ConcessionTypeId != input.ConcessionTypeId || request.StudentFeeStructureId != input.StudentFeeStructureId)
				throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition);
			await concessionRequestManager.ReplaceFeeHeadMappingsAsync(request, input.ConcessionPolicyId, input.ConcessionPolicyVersionId,
				FamilyHeads(input), allowIncomplete: true, dueFeeConcessions: FamilyDues(input));
			request.UpdateRemarks(input.Remarks);
		}
		else request = await concessionRequestManager.CreatePendingAsync(CurrentTenant.Id, year, child,
			input.ConcessionPolicyId, input.ConcessionPolicyVersionId, input.ConcessionTypeId, input.Remarks, FamilyHeads(input),
			input.StudentFeeStructureId, actorId: currentUser.GetId(), allowIncomplete: true, enforcePendingUniqueness: false,
			dueFeeConcessions: FamilyDues(input));
		request.ExtraProperties[FamilyFinanceOrigin.MarkerProperty] = true;
		var documents = await ValidateAndBuildDocumentsAsync(request, input.Documents.Select(x => new UploadConcessionDocumentEntryDto
			{ DocumentId = x.DocumentId, Status = x.Status, Url = x.Url }));
		await ReleaseRemovedDocumentUploadsAsync(request, documents);
		request.ReplaceDocuments(documents);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await FamilyCommands.RecordAsync(action, input.IdempotencyKey, payload, request.Id, child, year);
		await WriteRequestAuditAsync(request, "create", "Family concession draft saved.");
		await uow.CompleteAsync();
		return await FamilyDetailAsync(request);
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<object> FamilyActionAsync(Guid child, Guid year, Guid id, MobileFinanceRequestAction input, bool submit)
	{
		await RequireFamilyAsync(child, year, submit ? MobileFinancePermissions.SubmitConcession : MobileFinancePermissions.CancelConcession);
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var payload = new { child, year, id, input };
		var action = submit ? "concession_submit" : "concession_cancel";
		var replay = await FamilyCommands.ReplayAsync(action, input.IdempotencyKey, payload);
		var request = await RequireOwnedFamilyRequestAsync(child, year, id);
		if (replay.HasValue) { if (replay != id) throw new BusinessException(FamilyFinanceErrorCodes.IdempotencyConflict); await uow.CompleteAsync(); return await FamilyDetailAsync(request); }
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);
		if (submit)
		{
			request.EnsureDraft();
			await RequireAssignedFamilyPolicyAsync(child, year, new MobileConcessionInput { StudentFeeStructureId = request.StudentFeeStructureId,
				ConcessionPolicyId = request.ConcessionPolicyId, ConcessionPolicyVersionId = request.ConcessionPolicyVersionId,
				ConcessionTypeId = request.ConcessionTypeId });
			await concessionRequestManager.RefreshDraftAsync(request);
			EnsureRequiredDocumentsPresent(request);
			var evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
			MobileFinanceCommandRules.RequireFingerprint(ComputePreviewFingerprint(request.ConcessionPolicyVersionId, evaluation.DueFees, evaluation.Mappings), input.PreviewFingerprint);
			if (await concessionRequestRepository.HasPendingForStudentAndTypeAsync(child, request.ConcessionTypeId, year, id))
				throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
			request.Submit(Clock.Now);
		}
		else request.Cancel(currentUser.GetId(), input.Reason, Clock.Now);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await FamilyCommands.RecordAsync(action, input.IdempotencyKey, payload, id, child, year);
		await WriteRequestAuditAsync(request, submit ? "submit" : "cancel", submit ? "Family concession submitted for staff review." : "Family concession cancelled.");
		await uow.CompleteAsync();
		return await FamilyDetailAsync(request);
	}

	public virtual async Task<FileDownloadResult> FamilyDownloadAsync(Guid child, Guid year, Guid id, Guid documentId)
	{
		await RequireFamilyAsync(child, year, MobileFinancePermissions.ViewConcessionDocument);
		var request = await RequireOwnedFamilyRequestAsync(child, year, id);
		var document = request.ConcessionRequestDocuments.SingleOrDefault(x => x.DocumentId == documentId && x.Status == 1)
			?? throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
		const string prefix = "/api/files/";
		if (!document.Url.StartsWith(prefix, StringComparison.Ordinal)) throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
		return await fileStorageAppService.GetAsync(Uri.UnescapeDataString(document.Url[prefix.Length..]));
	}
	public virtual async Task<ConcessionDocumentFileUploadResponseDto> FamilyUploadAsync(
		Guid child, Guid year,
		Guid concessionTypeId,
		Guid documentId,
		UploadConcessionDocumentFileDto input)
	{
		await RequireFamilyAsync(child, year, MobileFinancePermissions.UploadConcessionDocument);
		if (input.Content == null || input.Content.Length > 10 * 1024 * 1024) throw new BusinessException(ConcessionTypeErrorCodes.FileTooLarge);
		var type = await concessionTypeRepository.GetAsync(concessionTypeId);
		if (type.AcademicYearId != year || !type.IsActive) throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		var requirement = type.ConcessionTypeDocuments.FirstOrDefault(document => document.Id == documentId)
			?? throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);

		if (input.Content.Length == 0 || input.Content.Length > requirement.MaxFileSizeBytes)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.FileTooLarge)
				.WithData("MaxFileSizeBytes", requirement.MaxFileSizeBytes);
		}

		var safeFileName = Path.GetFileName(input.FileName);
		if (!string.Equals(safeFileName, input.FileName, StringComparison.Ordinal))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
		var documentType = ConcessionTypeConsts.GetDocumentType(extension);
		var acceptedTypes = requirement.AcceptedFormats.Select(format => format.DocumentType).ToHashSet();
		if (!documentType.HasValue ||
			!acceptedTypes.Contains(documentType.Value) ||
			!FileValidationService.IsMatchingDocumentContentType(extension, input.ContentType))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidFileFormat)
				.WithData("AcceptedFormats", string.Join(", ", acceptedTypes.Select(ConcessionTypeConsts.GetExtension)));
		}

		try
		{
			FileValidationService.ValidateDocumentFileSignature(input.Content, extension);
		}
		catch (BusinessException)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidFileFormat);
		}

		var storedFileName = await fileStorageAppService.UploadAsync(safeFileName, input.Content);
		var fileUrl = $"/api/files/{Uri.EscapeDataString(storedFileName)}";
		var uploadId = guidGenerator.Create();
		try
		{
			await concessionDocumentUploadRepository.InsertAsync(new ConcessionDocumentUpload(
				uploadId, CurrentTenant.Id, concessionTypeId, documentId,
				currentUser.GetId(), fileUrl,
				Clock.Now.AddHours(ConcessionDocumentUpload.StagingLifetimeHours)), autoSave: true);
		}
		catch
		{
			try
			{
				await fileStorageAppService.DeleteAsync(storedFileName);
			}
			catch (Exception cleanupException)
			{
				Logger.LogWarning(
					cleanupException,
					"Failed to remove concession file {StoredFileName} after staging receipt creation failed.",
					storedFileName);
			}
			throw;
		}
		return new ConcessionDocumentFileUploadResponseDto
		{
			Url = BuildStagedDocumentAccessUrl(uploadId)
		};
	}

}
