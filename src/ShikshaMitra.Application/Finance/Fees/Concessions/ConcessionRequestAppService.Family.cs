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

	public virtual async Task<MobileConcessionRequestContext> FamilyEligibilityAsync(Guid child, Guid year, Guid? studentFeeStructureId = null)
 {
  var access = await RequireFamilyAsync(child, year);
  var assignments = await studentFeeStructureRepository.GetListAsync(x => x.StudentId == child && x.AcademicYearId == year);
  if(studentFeeStructureId.HasValue && !assignments.Any(x=>x.Id==studentFeeStructureId)) throw new Volo.Abp.Authorization.AbpAuthorizationException();
  var structureIds=assignments.Select(x=>x.FeeStructureStandardId).ToArray();
  var structures=await feeStructureRepository.GetListAsync(x=>Enumerable.Contains(structureIds,x.Id));
  var result = new MobileConcessionRequestContext {
   StudentId=child,AcademicYearId=year,StudentName=access.StudentName,
   AssignmentSelectionRequired=!studentFeeStructureId.HasValue&&assignments.Count>1,
   Assignments=assignments.OrderBy(x=>x.ChargeStartDate).ThenBy(x=>x.Id).Select(x=>new MobileConcessionAssignmentOption {
    StudentFeeStructureId=x.Id,FeeStructureId=x.FeeStructureStandardId,FeeStructureName=structures.FirstOrDefault(f=>f.Id==x.FeeStructureStandardId)?.Name??string.Empty,
    ChargeStartDate=x.ChargeStartDate,ChargeEndDate=x.ChargeEndDate }).ToList(),
   CanCreate=await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.RequestConcession),
   CanEdit=await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession),
   CanSubmit=await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession),
   CanUploadDocuments=await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.UploadConcessionDocument) };
  if(result.AssignmentSelectionRequired){result.Blockers.Add("Select the fee assignment for this concession request.");return result;}
  var assignment=assignments.SingleOrDefault(x=>x.Id==studentFeeStructureId)??assignments.SingleOrDefault();
  if(assignment==null){result.Blockers.Add("No fee assignment is available.");return result;}
  result.StudentFeeStructureId=assignment.Id;
  result.FeeStructureName=structures.FirstOrDefault(x=>x.Id==assignment.FeeStructureStandardId)?.Name??string.Empty;
  if(!assignment.ConcessionPolicyId.HasValue||!assignment.ConcessionPolicyVersionId.HasValue){result.Blockers.Add("No concession policy is assigned.");return result;}
  var version=await financePolicyVersionRepository.FindAsync(assignment.ConcessionPolicyVersionId.Value);
  if(version==null||version.PolicyType!="concession"||version.PolicyId!=assignment.ConcessionPolicyId||version.AcademicYearId!=year||
   await financePolicyVersionRepository.FindAsync(x=>x.PolicyType=="concession"&&x.PolicyId==version.PolicyId&&x.VersionNumber>version.VersionNumber)!=null)
  {result.Blockers.Add("The assigned concession policy is unavailable.");return result;}
  var policy=System.Text.Json.JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson);
  if(policy==null||policy.Status!=ConcessionPolicyStatus.Active){result.Blockers.Add("The assigned concession policy is inactive.");return result;}
  var assignedDues=await dueFeeRepository.GetListAsync(x=>x.StudentId==child&&x.StudentFeeStructureId==assignment.Id);
  var headIds=assignedDues.Select(x=>x.FeeHeadId).Distinct().ToArray();
  var heads=await feeHeadRepository.GetListAsync(x=>Enumerable.Contains(headIds,x.Id)&&x.AcademicYearId==year&&x.IsActive&&x.IsConcessionApplied);
  var headNames=heads.ToDictionary(x=>x.Id,x=>x.Name); var permitted=heads.Select(x=>x.Id).ToHashSet();
  if(assignment.ConcessionFeeHeadScope==ConcessionFeeHeadScope.SelectedFeeHeads)permitted.IntersectWith(assignment.GetConcessionApplicableFeeHeadIds());
  var eligible=ConcessionRequestManager.GetEligibleDueFees(assignedDues.Where(x=>permitted.Contains(x.FeeHeadId)).ToList(),policy.ApplyOn);
  result.EligibleDueFees=eligible.Select(x=>FamilyEligibleDue(x,headNames.GetValueOrDefault(x.FeeHeadId,string.Empty),policy)).ToList();
  result.FeeHeads=result.EligibleDueFees.GroupBy(x=>new{x.FeeHeadId,x.FeeHeadName}).Select(g=>new MobileConcessionEligibleFeeHead {
   FeeHeadId=g.Key.FeeHeadId,FeeHeadName=g.Key.FeeHeadName,EligibleOutstanding=MobileFinanceProjection.Money(eligible.Where(d=>d.FeeHeadId==g.Key.FeeHeadId).Sum(d=>ConcessionRequestManager.GetConcessionCapacity(d,policy))) }).ToList();
  result.ConcessionPolicies=[new MobileConcessionPolicyOption {
   ConcessionPolicyId=version.PolicyId,ConcessionPolicyVersionId=version.Id,PolicyName=policy.PolicyName,PolicyCode=policy.PolicyCode,Version=version.VersionNumber,IsDefault=policy.IsDefault,AutoCreateRefund=policy.ShouldAutoCreateRefund,
   ValueSource=policy.ValueSource.ToString(),CalculationMethod=policy.CalculationMethod.ToString(),ApplyOn=policy.ApplyOn.ToString(),
   PercentageValue=policy.PercentageValue.HasValue?MobileFinanceProjection.Money(policy.PercentageValue.Value):null,
   FixedAmount=policy.FixedAmount.HasValue?MobileFinanceProjection.Money(policy.FixedAmount.Value):null,
   MaximumPercentage=policy.MaximumPercentage.HasValue?MobileFinanceProjection.Money(policy.MaximumPercentage.Value):null,
   MaximumFixedAmount=policy.MaximumFixedAmount.HasValue?MobileFinanceProjection.Money(policy.MaximumFixedAmount.Value):null,
   ApplicableConcessionTypeIds=policy.ApplicableConcessionTypeIds?.ToList()??[],FeeHeadScope=policy.FeeHeadScope.ToString(),ApplicableFeeHeadIds=policy.ApplicableFeeHeadIds?.ToList()??[],RoundingMethod=policy.RoundingMethod.ToString() }];
  var types=await concessionTypeRepository.GetListAsync(x=>x.AcademicYearId==year&&x.IsActive);
  result.ConcessionTypes=types.Where(x=>(policy.ApplicableConcessionTypeIds?.Count??0)==0||policy.ApplicableConcessionTypeIds!.Contains(x.Id)).Select(x=>new MobileConcessionTypeOption {
   ConcessionTypeId=x.Id,Name=x.Name,Description=x.Description,Documents=x.ConcessionTypeDocuments.Select(d=>new MobileConcessionDocumentRequirement {
    DocumentId=d.Id,DocumentName=d.DocumentName,IsRequired=d.IsRequired,MaxFileSizeBytes=Math.Min(d.MaxFileSizeBytes,10*1024*1024),AcceptedFormats=d.AcceptedFormats.Select(f=>f.DocumentType.ToString()).ToList() }).ToList() }).ToList();
  if(eligible.Count==0)result.Blockers.Add("No installments are eligible for the assigned policy.");
  return result;
 }
 private static MobileConcessionEligibleDueFee FamilyEligibleDue(ShikshaMitra.Finance.Fees.FeeCollection.DueFee x,string name,ConcessionPolicySnapshot policy)=>new(){
  DueFeeId=x.Id,FeeHeadId=x.FeeHeadId,FeeHeadName=name,Month=(int)x.Month,Year=x.Year,InstallmentLabel=x.GetDisplayInstallmentLabel(),
  OriginalAmount=MobileFinanceProjection.Money(x.OriginalAmount),PaidAmount=MobileFinanceProjection.Money(x.PaidAmount),ConcessionAmount=MobileFinanceProjection.Money(x.ConcessionAmount),
  AdjustmentCreditAmount=MobileFinanceProjection.Money(x.AdjustmentCreditAmount),OutstandingAmount=MobileFinanceProjection.Money(x.OutstandingAmount),
  EligibleConcessionAmount=MobileFinanceProjection.Money(ConcessionRequestManager.GetConcessionCapacity(x,policy)),Status=x.Status.ToString(),ConcurrencyStamp=x.ConcurrencyStamp };

	public virtual async Task<MobileConcessionRequestPreview> FamilyPreviewAsync(Guid child, Guid year, MobileConcessionInput input)
	{
		await RequireFamilyAsync(child, year);
		if (!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.RequestConcession) &&
			!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession) &&
			!await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession)) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		await RequireAssignedFamilyPolicyAsync(child, year, input);
		var preview = await concessionRequestManager.PreviewAsync(CurrentTenant.Id, year, child, input.ConcessionPolicyId,
			input.ConcessionPolicyVersionId, input.ConcessionTypeId, FamilyHeads(input), input.StudentFeeStructureId, FamilyDues(input));
  var headIds=preview.EligibleDueFees.Select(x=>x.FeeHeadId).Distinct().ToArray();
  var names=(await feeHeadRepository.GetListAsync(x=>Enumerable.Contains(headIds,x.Id))).ToDictionary(x=>x.Id,x=>x.Name);
  var total=preview.Mappings.Sum(x=>x.FeeHeadConcessionAmount);
  var eligibleBase=preview.Mappings.Sum(x=>x.PreConcessionFeeHeadAmount);
  return new MobileConcessionRequestPreview { StudentFeeStructureId=input.StudentFeeStructureId,ConcessionPolicyId=input.ConcessionPolicyId,
   ConcessionPolicyVersionId=input.ConcessionPolicyVersionId,ConcessionPolicyName=preview.Policy.PolicyName,ConcessionPolicyCode=preview.Policy.PolicyCode,
   ConcessionPolicyVersion=preview.Policy.Version,ApplyOn=preview.Policy.ApplyOn.ToString(),ValueSource=preview.Policy.ValueSource.ToString(),CalculationMethod=preview.Policy.CalculationMethod.ToString(),
   MaximumPercentage=preview.Policy.MaximumPercentage.HasValue?MobileFinanceProjection.Money(preview.Policy.MaximumPercentage.Value):null,
   MaximumFixedAmount=preview.Policy.MaximumFixedAmount.HasValue?MobileFinanceProjection.Money(preview.Policy.MaximumFixedAmount.Value):null,
   FeeHeadConcessions=preview.Mappings.Select(x=>new MobileFeeHeadConcessionDetail {FeeHeadId=x.FeeHeadId,FeeHeadName=names.GetValueOrDefault(x.FeeHeadId,string.Empty),
    AnnualBaseAmount=MobileFinanceProjection.Money(x.PreConcessionFeeHeadAmount),BasedOn=(int)x.UserSpecifiedConcessionAmountType,Amount=MobileFinanceProjection.Money(x.UserSpecifiedConcessionAmountValue),
    DiscountAmount=MobileFinanceProjection.Money(x.FeeHeadConcessionAmount),NetAmount=MobileFinanceProjection.Money(x.PostConcessionFeeHeadAmount)}).ToList(),
   TotalConcessionAmount=MobileFinanceProjection.Money(total),TotalEligibleBase=MobileFinanceProjection.Money(eligibleBase),NetPayable=MobileFinanceProjection.Money(Math.Max(0m,eligibleBase-total)),
   PreviewFingerprint=ComputePreviewFingerprint(input.ConcessionPolicyVersionId,preview.EligibleDueFees,preview.Mappings),GeneratedAt=Clock.Now,
   EligibleDueFees=preview.EligibleDueFees.Select(x=>FamilyEligibleDue(x,names.GetValueOrDefault(x.FeeHeadId,string.Empty),preview.Policy)).ToList() };

	}

	private async Task<ConcessionRequest> RequireOwnedFamilyRequestAsync(Guid child, Guid year, Guid requestId)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(requestId);
		MobileFinanceCommandRules.RequireOwner(child, year, currentUser.GetId(), request.StudentId, request.AcademicYearId, request.CreatorId);
		return request;
	}

	private async Task<MobileConcessionRequestDetail> FamilyDetailAsync(ConcessionRequest request)
	{
		var edit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.EditConcession);
		var submit = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.SubmitConcession);
		var cancel = await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.CancelConcession);
  var type=await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
  var canViewDocuments=await FamilyAccess.IsGrantedAsync(MobileFinancePermissions.ViewConcessionDocument);
  var student=await studentRepository.GetAsync(request.StudentId);
  var version=await financePolicyVersionRepository.GetAsync(request.ConcessionPolicyVersionId);
  var policy=System.Text.Json.JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson)??throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
  var headIds=request.ConcessionFeeHeadMappings.Select(x=>x.FeeHeadId).ToArray();
  var names=(await feeHeadRepository.GetListAsync(x=>Enumerable.Contains(headIds,x.Id))).ToDictionary(x=>x.Id,x=>x.Name);
  List<ConcessionTypeRequirementSnapshot> requirements;
  try { requirements=string.IsNullOrWhiteSpace(request.TypeRequirementsSnapshotJson)&&!request.SubmittedOn.HasValue
   ? type.ConcessionTypeDocuments.Select(d=>new ConcessionTypeRequirementSnapshot(d.Id,d.DocumentName,d.IsRequired,d.MaxFileSizeBytes,d.AcceptedFormats.Select(f=>f.DocumentType).ToList())).ToList()
   : System.Text.Json.JsonSerializer.Deserialize<List<ConcessionTypeRequirementSnapshot>>(request.TypeRequirementsSnapshotJson??string.Empty,CommandJsonOptions)??[]; }
  catch(System.Text.Json.JsonException){throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);}
  var required= requirements.Select(d=>new MobileConcessionDocumentRequirement{DocumentId=d.DocumentId,DocumentName=d.DocumentName,IsRequired=d.IsRequired,
   MaxFileSizeBytes=d.MaxFileSizeBytes,AcceptedFormats=d.AcceptedFormats.Select(format=>format.ToString()).ToList(),IsUploaded=request.ConcessionRequestDocuments.Any(x=>x.DocumentId==d.DocumentId&&x.Status==1&&!string.IsNullOrWhiteSpace(x.Url)),
   Url=canViewDocuments&&request.ConcessionRequestDocuments.Any(x=>x.DocumentId==d.DocumentId&&x.Status==1&&!string.IsNullOrWhiteSpace(x.Url))?BuildDocumentAccessUrl(request.Id,d.DocumentId):null}).ToList();
  return new MobileConcessionRequestDetail { ConcessionRequestId=request.Id, Status=request.Status.ToString(),StatusName=request.Status.ToString(),
   StudentId=request.StudentId,AcademicYearId=request.AcademicYearId,StudentName=FormatStudentName(student),
   ConcessionPolicyName=policy.PolicyName,ConcessionPolicyCode=policy.PolicyCode,ConcessionPolicyVersion=policy.Version,CancellationReason=request.CancellationReason,
   RequiredDocuments=required,MissingDocumentIds=required.Where(x=>x.IsRequired&&!x.IsUploaded).Select(x=>x.DocumentId).ToList(),
   DocumentSubmissionState=required.Where(x=>x.IsRequired).All(x=>x.IsUploaded)?"Complete":"DocumentsPending",CreatedAt=request.CreationTime,SubmittedAt=request.SubmittedOn,
   TotalRequestedConcessionAmount=MobileFinanceProjection.Money(request.GetTotalRequestedConcession()),
   TotalApprovedConcessionAmount=MobileFinanceProjection.Money(request.GetTotalApprovedConcession()),
   TotalReversedConcessionAmount=MobileFinanceProjection.Money(request.GetTotalReversedConcession()),
   NetApprovedConcessionAmount=MobileFinanceProjection.Money(request.GetNetApprovedConcession()),ConcurrencyStamp=request.ConcurrencyStamp,
   AvailableActions=new[]{request.Status==ConcessionRequestStatus.Draft&&edit?"edit":null,request.Status==ConcessionRequestStatus.Draft&&submit?"submit":null,
    request.Status is (ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending)&&cancel?"cancel":null}.Where(x=>x!=null).Select(x=>x!).ToList(),
   Remarks=request.Remarks,StudentFeeStructureId=request.StudentFeeStructureId,ConcessionPolicyId=request.ConcessionPolicyId,
   ConcessionPolicyVersionId=request.ConcessionPolicyVersionId,ConcessionTypeId=request.ConcessionTypeId,ConcessionTypeName=type.Name,
   FeeHeadConcessions=request.ConcessionFeeHeadMappings.Select(x=>new MobileFeeHeadConcessionDetail{FeeHeadId=x.FeeHeadId,FeeHeadName=names.GetValueOrDefault(x.FeeHeadId,string.Empty),AnnualBaseAmount=MobileFinanceProjection.Money(x.PreConcessionFeeHeadAmount),NetAmount=MobileFinanceProjection.Money(x.PostConcessionFeeHeadAmount),BasedOn=(int)x.UserSpecifiedConcessionAmountType,
    Amount=MobileFinanceProjection.Money(x.UserSpecifiedConcessionAmountValue),DiscountAmount=MobileFinanceProjection.Money(x.FeeHeadConcessionAmount)}).ToList(),
   DueFeeConcessions=ConcessionRequestManager.ReadDueFeeEvidence(request).Select(x=>new MobileDueFeeConcession{DueFeeId=x.DueFeeId,FeeHeadId=x.FeeHeadId,Amount=MobileFinanceProjection.Money(x.Amount)}).ToList(),
   SubmittedDocuments=request.ConcessionRequestDocuments.Select(x=>new MobileConcessionSubmittedDocument{DocumentId=x.DocumentId,
    DocumentName=requirements.FirstOrDefault(d=>d.DocumentId==x.DocumentId)?.DocumentName??string.Empty,Url=x.Status==1&&!string.IsNullOrWhiteSpace(x.Url)?BuildDocumentAccessUrl(request.Id,x.DocumentId):string.Empty,Status=x.Status}).ToList() };

	}

	public virtual async Task<MobileConcessionRequestDetail> FamilyGetAsync(Guid child, Guid year, Guid id)
	{
		await RequireFamilyAsync(child, year);
		return await FamilyDetailAsync(await RequireOwnedFamilyRequestAsync(child, year, id));
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<MobileConcessionRequestDetail> FamilySaveAsync(Guid child, Guid year, Guid? id, MobileConcessionInput input)
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
	public virtual async Task<MobileConcessionRequestDetail> FamilyActionAsync(Guid child, Guid year, Guid id, MobileFinanceRequestAction input, bool submit)
	{
		await RequireFamilyAsync(child, year, submit ? MobileFinancePermissions.SubmitConcession : MobileFinancePermissions.CancelConcession);
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var payload = new { child, year, id, input };
		var action = submit ? "concession_submit" : "concession_cancel";
		var replay = await FamilyCommands.ReplayAsync(action, input.IdempotencyKey, payload);
		var request = await RequireOwnedFamilyRequestAsync(child, year, id);
		if (replay.HasValue) { if (replay != id) throw new BusinessException(FamilyFinanceErrorCodes.IdempotencyConflict); await uow.CompleteAsync(); return await FamilyDetailAsync(request); }
		EnsureConcurrencyStamp(request, submit ? input.ConcurrencyStamp : input.RequestConcurrencyStamp);
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
