using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using Volo.Abp.Application.Dtos;
using Volo.Abp;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Policies;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	[Authorize(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	public async Task<ConcessionRequestContextDto> GetContextAsync(
		Guid academicYearId,
		Guid studentId,
		Guid? studentFeeStructureId = null,
		Guid? concessionTypeId = null,
		Guid? concessionPolicyId = null,
		Guid? concessionPolicyVersionId = null,
		Guid? requestId = null)
	{
		await EnsureAnyPermissionAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add,
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Edit,
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Submit);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);
		await ValidateContextRequestAsync(requestId, studentId, academicYearId);
		var benefits = await concessionRequestRepository.GetBlockingBenefitsAsync(studentId, academicYearId, requestId);
		var activePolicies = await concessionPolicyRepository.GetListAsync(x =>
			x.AcademicYearId == academicYearId && x.Status == ConcessionPolicyStatus.Active);
		var activePolicyIds = activePolicies.Select(x => x.Id).ToList();
		var policyVersions = activePolicyIds.Count == 0
			? []
			: await financePolicyVersionRepository.GetListAsync(x =>
				x.PolicyType == "concession" && activePolicyIds.Contains(x.PolicyId));
		var currentVersionByPolicyId = policyVersions
			.GroupBy(x => x.PolicyId)
			.ToDictionary(group => group.Key,
				group => group.OrderByDescending(x => x.VersionNumber).First());
		var policyOptions = activePolicies
			.Where(policy => currentVersionByPolicyId.ContainsKey(policy.Id))
			.OrderByDescending(policy => policy.IsDefault)
			.ThenBy(policy => policy.PolicyName)
			.Select(policy => new ConcessionPolicyOptionDto
			{
				ConcessionPolicyId = policy.Id,
				ConcessionPolicyVersionId = currentVersionByPolicyId[policy.Id].Id,
				PolicyName = policy.PolicyName,
				PolicyCode = policy.PolicyCode,
				Version = currentVersionByPolicyId[policy.Id].VersionNumber,
				IsDefault = policy.IsDefault,
				ValueSource = policy.ValueSource.ToString(),
				CalculationMethod = policy.CalculationMethod.ToString(),
				PercentageValue = policy.PercentageValue,
				FixedAmount = policy.FixedAmount,
				MaximumPercentage = policy.MaximumPercentage,
				MaximumFixedAmount = policy.MaximumFixedAmount,
				ApplyOn = policy.ApplyOn.ToString(),
				AutoCreateRefund = policy.AutoCreateRefund,
				RoundingMethod = policy.RoundingMethod.ToString(),
				ApplicableConcessionTypeIds = policy.ApplicableConcessionTypeIds.ToList(),
				FeeHeadScope = policy.FeeHeadScope.ToString(),
				ApplicableFeeHeadIds = policy.ApplicableFeeHeadIds.ToList(),
			})
			.ToList();
		var assignments = await studentFeeStructureRepository.GetListAsync(x =>
			x.StudentId == studentId && x.AcademicYearId == academicYearId);
		var feeStructureIds = assignments.Select(x => x.FeeStructureStandardId).Distinct().ToList();
		var feeStructureNames = feeStructureIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await feeStructureRepository.GetListAsync(x => feeStructureIds.Contains(x.Id)))
				.ToDictionary(x => x.Id, x => x.Name);
		var assignmentOptions = assignments
			.OrderBy(x => x.ChargeStartDate)
			.ThenBy(x => x.Id)
			.Select(x => new ConcessionAssignmentOptionDto
			{
				StudentFeeStructureId = x.Id,
				FeeStructureId = x.FeeStructureStandardId,
				FeeStructureName = feeStructureNames.GetValueOrDefault(
					x.FeeStructureStandardId, $"Fee structure {x.FeeStructureStandardId}"),
				ChargeStartDate = x.ChargeStartDate,
				ChargeEndDate = x.ChargeEndDate,
			})
			.ToList();
		if (!studentFeeStructureId.HasValue && assignments.Count > 1)
		{
			var student = await studentRepository.GetAsync(studentId);
			return new ConcessionRequestContextDto
			{
				AcademicYearId = academicYearId,
				StudentId = studentId,
				StudentName = FormatStudentName(student),
				AssignmentSelectionRequired = true,
				Assignments = assignmentOptions,
				ConcessionPolicies = policyOptions,
				Blockers = ["Select the fee assignment for this concession request."],
			};
		}
		var context = await concessionRequestManager.GetContextAsync(
			academicYearId, studentId, studentFeeStructureId);
		var feeHeadIds = context.EligibleDueFees.Select(x => x.FeeHeadId).Distinct().ToList();
		var feeHeads = feeHeadIds.Count == 0
			? []
			: await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
		var feeHeadNames = feeHeads.ToDictionary(x => x.Id, x => x.Name);
		var concessionEnabledHeadIds = feeHeads.Where(x => x.AcademicYearId == academicYearId &&
			x.IsActive && x.IsConcessionApplied).Select(x => x.Id).ToHashSet();
		var hasConcessionEnabledHeads = concessionEnabledHeadIds.Count > 0;
		if (context.Assignment.ConcessionFeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads)
			concessionEnabledHeadIds.IntersectWith(context.Assignment.GetConcessionApplicableFeeHeadIds());
		var eligibleDues = context.EligibleDueFees.Where(x => concessionEnabledHeadIds.Contains(x.FeeHeadId)).ToList();
		var includePaid = context.Assignment.ConcessionPolicyId.HasValue &&
			activePolicies.Any(x => x.Id == context.Assignment.ConcessionPolicyId.Value &&
				x.ApplyOn == ConcessionApplyOn.AllFees);
		var concessionTypes = await concessionTypeRepository.GetListAsync(x =>
			x.AcademicYearId == academicYearId && x.IsActive);
		if (concessionPolicyId.HasValue || concessionPolicyVersionId.HasValue)
		{
			var selected = activePolicies.FirstOrDefault(x => x.Id == concessionPolicyId);
			if (selected is null || !currentVersionByPolicyId.TryGetValue(selected.Id, out var version) ||
				version.Id != concessionPolicyVersionId)
				throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
			eligibleDues = ConcessionRequestManager.GetEligibleDueFees(eligibleDues, selected.ApplyOn);
		}
		var blockers = new List<string>();
		if (eligibleDues.Count == 0)
		{
			if (context.EligibleDueFees.Count == 0)
				blockers.Add("This fee assignment has no eligible installment amount to discount.");
			else if (!hasConcessionEnabledHeads)
			{
				var blockedHeadNames = feeHeads
					.Select(x => x.Name)
					.OrderBy(x => x)
					.ToList();
				blockers.Add($"Outstanding fee heads are not eligible for concessions: {string.Join(", ", blockedHeadNames)}. Check 'Enable concessions', active status, and academic year for the intended heads in Finance > Fees > Settings > Fee Head.");
			}
			else if (concessionEnabledHeadIds.Count == 0)
				blockers.Add("This fee assignment excludes all concession-enabled fee heads. Review concession eligibility on its fee structure.");
			else
				blockers.Add("No installments match this policy's application rule.");
		}

		return new ConcessionRequestContextDto
		{
			AcademicYearId = academicYearId,
			StudentId = studentId,
			StudentName = FormatStudentName(context.Student),
			StudentFeeStructureId = context.Assignment.Id,
			FeeStructureName = context.FeeStructure.Name,
			Assignments = assignmentOptions,
			Blockers = blockers,
			FeeHeads = eligibleDues
				.GroupBy(x => x.FeeHeadId)
				.Select(group => new ConcessionEligibleFeeHeadDto
				{
					FeeHeadId = group.Key,
					FeeHeadName = feeHeadNames.GetValueOrDefault(group.Key, string.Empty),
					EligibleOutstanding = group.Sum(x => includePaid
						? Math.Max(0m, x.OriginalAmount - x.NetAmount) : x.OutstandingAmount),
				})
				.OrderBy(x => x.FeeHeadName)
				.ToList(),
			EligibleDueFees = MapEligibleDueFees(eligibleDues, feeHeadNames, includePaid),
			ConcessionPolicies = policyOptions,
			ConcessionTypes = concessionTypes.OrderBy(x => x.Name).Select(type => new ConcessionTypeOptionDto
			{
				ConcessionTypeId = type.Id,
				BenefitBlocker = MapBenefitBlocker(benefits.FirstOrDefault(x => x.ConcessionTypeId == type.Id)),
				Name = type.Name,
				Description = type.Description,
				Documents = type.ConcessionTypeDocuments.OrderBy(x => x.DocumentName)
					.Select(document => new ConcessionDocumentRequirementDto
					{
						DocumentId = document.Id,
						DocumentName = document.DocumentName,
						IsRequired = document.IsRequired,
						MaxFileSizeBytes = document.MaxFileSizeBytes,
						AcceptedFormats = document.AcceptedFormats.Select(x => x.DocumentType).ToList(),
					})
					.ToList(),
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
	public async Task<ConcessionApplicationsDto> GetApplicationsAsync(GetConcessionApplicationsInput input)
	{
		if (input.AcademicYearId == Guid.Empty || input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		if (input.Status.HasValue && !Enum.IsDefined(typeof(ConcessionRequestStatus), input.Status.Value))
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
				.WithData("Status", input.Status.Value);
		FinanceClassDivisionFilterValidator.Validate(input);

		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var canReviewDrafts = currentUser.Id.HasValue &&
			(await AuthorizationService.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Approve) ||
			 await AuthorizationService.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Reject));
        var tableQuery = ShikshaMitra.Finance.Common.FinanceTableQuery.Parse(input.Filters, input.Search,
            input.SortBy, input.SortOrder, ConcessionApplicationsTable.Columns);
        ConcessionApplicationsTable.Statuses(input.Tab);
		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip     = input.SkipCount;
		var status   = input.Status.HasValue ? (ConcessionRequestStatus)input.Status.Value : (ConcessionRequestStatus?)null;

		var counts = await concessionRequestRepository.GetStatusCountsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId,
			scopedStudentIdsQuery: scopedStudentIds, draftCreatorId: currentUser.Id, tableQuery: tableQuery,
			includeReviewableDrafts: canReviewDrafts);
		var items  = await concessionRequestRepository.GetListForApplicationsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, status, input.StandardId, input.DivisionId, skip, pageSize,
			scopedStudentIdsQuery: scopedStudentIds, draftCreatorId: currentUser.Id, tableQuery: tableQuery, tab: input.Tab,
			includeReviewableDrafts: canReviewDrafts);
		var total  = await concessionRequestRepository.CountForApplicationsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, status, input.StandardId, input.DivisionId,
			scopedStudentIdsQuery: scopedStudentIds, draftCreatorId: currentUser.Id, tableQuery: tableQuery, tab: input.Tab,
			includeReviewableDrafts: canReviewDrafts);

		var studentIds  = items.Select(i => i.StudentId).Distinct().ToList();
		var students    = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));
		var studentMap  = students.ToDictionary(s => s.Id);

		var typeIds     = items.Select(i => i.ConcessionTypeId).Distinct().ToList();
		var types       = await concessionTypeRepository.GetListAsync(t => typeIds.Contains(t.Id));
		var typeMap     = types.ToDictionary(t => t.Id, t => t.Name);
		var policyVersionIds = items.Select(x => x.ConcessionPolicyVersionId).Distinct().ToList();
		var policyVersions = policyVersionIds.Count == 0
			? []
			: await financePolicyVersionRepository.GetListAsync(x => policyVersionIds.Contains(x.Id));
		var policySnapshotMap = policyVersions.ToDictionary(
			x => x.Id,
			x => JsonSerializer.Deserialize<ConcessionPolicySnapshot>(x.SnapshotJson));

		var standardIds = items.Select(s => s.StandardId).Distinct().ToList();
		var divisionIds = items.Select(s => s.DivisionId).Distinct().ToList();
		var standards   = standardIds.Count > 0 ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id)) : [];
		var divisions   = divisionIds.Count > 0 ? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id)) : [];
		var standardMap = standards.ToDictionary(s => s.Id, s => s.Name);
		var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);

		var isAdministrator = currentUser.Id.HasValue && await userScopeService.IsAdminUserAsync(currentUser.Id.Value);
		return new ConcessionApplicationsDto
		{
			DraftCount     = counts.GetValueOrDefault(ConcessionRequestStatus.Draft),
			PendingCount   = counts.GetValueOrDefault(ConcessionRequestStatus.Pending),
			ApprovedCount  = counts.GetValueOrDefault(ConcessionRequestStatus.Approved),
			RejectedCount  = counts.GetValueOrDefault(ConcessionRequestStatus.Rejected),
			CancelledCount = counts.GetValueOrDefault(ConcessionRequestStatus.Cancelled),
			PartiallyReversedCount = counts.GetValueOrDefault(ConcessionRequestStatus.PartiallyReversed),
			ReversedCount = counts.GetValueOrDefault(ConcessionRequestStatus.Reversed),
			TotalCount     = total,
			CurrentPage    = input.SkipCount / pageSize + 1,
			PageSize       = pageSize,
			TotalPages     = (int)Math.Ceiling(total / (double)pageSize),
			Items = items.Select(r =>
			{
				studentMap.TryGetValue(r.StudentId, out var student);
				typeMap.TryGetValue(r.ConcessionTypeId, out var typeName);
				policySnapshotMap.TryGetValue(r.ConcessionPolicyVersionId, out var policySnapshot);
				return new ConcessionApplicationItemDto
				{
					Id = r.Id, StudentId = r.StudentId, ConcessionTypeId = r.ConcessionTypeId,
					RequesterId = r.CreatorId,
					StudentName        = FormatStudentName(student),
					GrNumber           = FormatStudentGrNumber(student),
					RollNo             = student?.RollNo?.ToString() ?? string.Empty,
					StandardName       = standardMap.GetValueOrDefault(r.StandardId, string.Empty),
					DivisionName       = divisionMap.GetValueOrDefault(r.DivisionId, string.Empty),
					ConcessionTypeName = typeName ?? string.Empty,
					ConcessionPolicyCode = policySnapshot?.PolicyCode ?? string.Empty,
					ConcessionPolicyVersion = policySnapshot?.Version ?? 0,
					DocumentSubmissionState = GetDocumentSubmissionState(r),
					ConcessionAmount   = r.GetTotalRequestedConcession(),
					RequestedConcessionAmount = r.GetTotalRequestedConcession(),
					ApprovedConcessionAmount = r.GetTotalApprovedConcession(),
					Status             = r.Status,
					StatusName         = r.Status.ToString(),
					CreatedAt          = r.CreationTime,
					ConcurrencyStamp   = r.ConcurrencyStamp,
					AvailableActions   = BuildAvailableActions(r, isAdministrator),
				};
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
	public async Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(
		Guid studentId, Guid academicYearId, PagedResultRequestDto input)
	{
		if (academicYearId == Guid.Empty || input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

		var queryable = await concessionRequestRepository.GetQueryableAsync();
		var scopedQuery = queryable.Where(x =>
			x.StudentId == studentId && x.AcademicYearId == academicYearId);

		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip     = input.SkipCount;
		var total = await AsyncExecuter.CountAsync(scopedQuery);
		var pending = await AsyncExecuter.CountAsync(scopedQuery.Where(x =>
			x.Status == ConcessionRequestStatus.Draft || x.Status == ConcessionRequestStatus.Pending));
		var approved = await AsyncExecuter.CountAsync(scopedQuery.Where(x => x.Status == ConcessionRequestStatus.Approved));
		var rejected = await AsyncExecuter.CountAsync(scopedQuery.Where(x => x.Status == ConcessionRequestStatus.Rejected));
		var page = await AsyncExecuter.ToListAsync(scopedQuery
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(pageSize));

		var students = await studentRepository.WithDetailsAsync(
			x => x.AcademicBoard!, x => x.AcademicMedium!, x => x.Standard!, x => x.Division!);
		var student = await AsyncExecuter.FirstOrDefaultAsync(students.Where(x => x.Id == studentId))
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
		var standard = student.Standard?.Name;
		var division = student.Division?.Name;

		var typeIds  = page.Select(r => r.ConcessionTypeId).Distinct().ToList();
		var types    = typeIds.Count > 0
			? await concessionTypeRepository.GetListAsync(t => typeIds.Contains(t.Id))
			: new List<ConcessionType>();
		var typeMap  = types.ToDictionary(t => t.Id);

		var feeHeadIds = page.SelectMany(r => r.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId)).Distinct().ToList();
		var feeHeads   = feeHeadIds.Count > 0
			? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
			: new List<FeeHead>();
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
		var canViewDocuments = await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.ViewDocument);
		var canViewRefunds = await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
		var requestIds = page.Select(x => x.Id).ToList();
		var refundMap = canViewRefunds
			? (await feeRefundRepository.GetListAsync(x => requestIds.Contains(x.ConcessionRequestId ?? Guid.Empty)))
				.GroupBy(x => x.ConcessionRequestId!.Value)
				.ToDictionary(x => x.Key, x => (IReadOnlyList<ConcessionRefundReferenceDto>)x.Select(r =>
					new ConcessionRefundReferenceDto
					{
						RefundRequestId = r.Id, VoucherNumber = r.VoucherNumber,
						OriginalReceiptId = r.OriginalReceiptId!.Value,
						Amount = r.CalculatedAmount, Status = r.Status.ToString()
					}).ToList())
			: new Dictionary<Guid, IReadOnlyList<ConcessionRefundReferenceDto>>();

		var isAdministrator = currentUser.Id.HasValue && await userScopeService.IsAdminUserAsync(currentUser.Id.Value);
		var items = page.Select(r =>
		{
			typeMap.TryGetValue(r.ConcessionTypeId, out var type);
			return MapStudentItem(r, type, feeHeadMap, canViewDocuments,
				refundMap.GetValueOrDefault(r.Id, []), isAdministrator);
		}).ToList();

		return new StudentConcessionRequestsDto
		{
			StudentDetails = new StudentBasicDetailDto
			{
				StudentId    = student.Id,
				Name         = FormatStudentName(student),
				RollNo       = student.RollNo?.ToString() ?? string.Empty,
				GrNumber     = FormatStudentGrNumber(student),
				StandardName = standard ?? string.Empty,
				DivisionName = division ?? string.Empty,
				BoardName = student.AcademicBoard?.Name ?? string.Empty,
				EducationMediumName = student.AcademicMedium?.Name ?? string.Empty,
			},
			ConcessionBasicDetail = new ConcessionBasicDetailDto
			{
				TotalApplications    = total,
				PendingApplications  = pending,
				ApprovedApplications = approved,
				RejectedApplications = rejected,
			},
			TotalCount  = total,
			CurrentPage = skip / pageSize + 1,
			PageSize    = pageSize,
			TotalPages  = (int)Math.Ceiling(total / (double)pageSize),
			Items       = items,
		};
	}

	[Authorize(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
	public async Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		ConcessionRequestLiveEvaluation? evaluation = null;
		List<DueFee> dueFees;
		if (request.Status is ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending)
		{
			evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
			dueFees = evaluation.DueFees.ToList();
		}
		else
		{
			var allocatedDueIds = request.ConcessionDueFeeAllocations.Select(x => x.DueFeeId).ToHashSet();
			dueFees = await dueFeeRepository.GetListAsync(df =>
				df.StudentId == request.StudentId &&
				df.StudentFeeStructureId == request.StudentFeeStructureId &&
				allocatedDueIds.Contains(df.Id));
		}
		var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
		var version = await financePolicyVersionRepository.GetAsync(request.ConcessionPolicyVersionId);
		var policy = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		var isReversal = request.Status is ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed;
		var requestedDueAmounts = ConcessionRequestManager.ReadDueFeeEvidence(request)
			.ToDictionary(x => x.DueFeeId, x => x.RequestedAmount);

		var groups = request.ConcessionFeeHeadMappings.Select(m =>
		{
			var liveMapping = evaluation?.Mappings.FirstOrDefault(x => x.FeeHeadId == m.FeeHeadId);
			var liveMaximum = liveMapping?.FeeHeadConcessionAmount ?? m.FeeHeadConcessionAmount;
			var suggestedTotal = isReversal ? 0m : Math.Min(m.FeeHeadConcessionAmount, liveMaximum);
			var eligibleHeadDues = dueFees
				.Where(df => df.FeeHeadId == m.FeeHeadId && ConcessionRequestManager.GetConcessionCapacity(df, policy) > 0m)
				.ToList();
			var suggestedByDue = !isReversal && requestedDueAmounts.Count > 0
				? eligibleHeadDues.ToDictionary(x => x.Id, x => requestedDueAmounts.GetValueOrDefault(x.Id))
				: isReversal || eligibleHeadDues.Count == 0
				? new Dictionary<Guid, decimal>()
				: ConcessionAmountAllocator.AllocateProportionally(
					eligibleHeadDues.Select(x => new ConcessionAmountCapacity(x.Id,
						ConcessionRequestManager.GetConcessionCapacity(x, policy))).ToList(),
					suggestedTotal).ToDictionary(x => x.Key, x => x.Amount);
			var lines = dueFees
				.Where(df => df.FeeHeadId == m.FeeHeadId)
				.OrderBy(df => df.InstallmentSequence.HasValue ? 0 : 1)
				.ThenBy(df => df.InstallmentSequence ?? int.MaxValue)
				.ThenBy(df => df.DemandDate ?? df.CoverageStartDate ?? df.ScheduleDueDate ?? df.DueDate)
				.ThenBy(df => df.Year).ThenBy(df => df.Month).ThenBy(df => df.Id)
				.Select(df =>
				{
					var allocation = request.ConcessionDueFeeAllocations.FirstOrDefault(x => x.DueFeeId == df.Id);
					var reversed = allocation is null ? 0m : request.ConcessionReversals
						.Where(x => x.ConcessionDueFeeAllocationId == allocation.Id).Sum(x => x.Amount);
					var reversible = Math.Max(0m, (allocation?.ConcessionAmount ?? 0m) - reversed);
					return new DueFeeApprovalLineDto
					{
						DueFeeId = df.Id, FeeHeadId = df.FeeHeadId,
						ConcessionDueFeeAllocationId = allocation?.Id,
						Month = (int)df.Month, Year = df.Year,
						MonthLabel = df.Month.ToString(),
						OriginalAmount = df.OriginalAmount,
						PaidAmount = df.PaidAmount,
						OutstandingAmount = df.OutstandingAmount,
						Status = df.Status.ToString(),
						IsSelectable = isReversal ? reversible > 0m : ConcessionRequestManager.GetConcessionCapacity(df, policy) > 0m,
						MaxConcessionAmount = isReversal ? reversible : ConcessionRequestManager.GetConcessionCapacity(df, policy),
						SuggestedConcessionAmount = suggestedByDue.GetValueOrDefault(df.Id),
						AppliedConcessionAmount = allocation?.ConcessionAmount ?? 0m,
						ReversedConcessionAmount = reversed,
						ReversibleConcessionAmount = reversible,
						ConcurrencyStamp = df.ConcurrencyStamp,
					};
				}).ToList();

			return new FeeHeadApprovalGroupDto
			{
				FeeHeadId = m.FeeHeadId,
				FeeHeadName = feeHeadNameMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
				RequestedConcessionAmount = m.FeeHeadConcessionAmount,
				LiveMaximumConcessionAmount = liveMaximum,
				SuggestedConcessionAmount = suggestedTotal,
				HasChangedSinceSubmission = liveMaximum < m.FeeHeadConcessionAmount ||
					dueFees.Where(x => x.FeeHeadId == m.FeeHeadId)
						.Any(x => requestedDueAmounts.GetValueOrDefault(x.Id) >
							ConcessionRequestManager.GetConcessionCapacity(x, policy)),
				DueFeeLines = lines,
			};
		}).ToList();
		var previewFingerprint = ComputePreviewFingerprint(
			request.ConcessionPolicyVersionId,
			dueFees,
			evaluation?.Mappings.AsEnumerable() ?? request.ConcessionFeeHeadMappings.AsEnumerable());

		return new ConcessionApprovalContextDto
		{
			BenefitBlocker = request.Status is ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending
				? MapBenefitBlocker(await concessionRequestRepository.FindBlockingBenefitAsync(
					request.StudentId, request.ConcessionTypeId, request.AcademicYearId, request.Id)) : null,
			ConcessionRequestId = request.Id,
			StudentId           = request.StudentId,
			ConcessionPolicyName = policy.PolicyName,
			ConcessionPolicyCode = policy.PolicyCode,
			ConcessionPolicyVersion = policy.Version,
			RequestConcurrencyStamp = request.ConcurrencyStamp,
			TotalRequestedConcessionAmount = request.GetTotalRequestedConcession(),
			TotalLiveMaximumConcessionAmount = groups.Sum(x => x.LiveMaximumConcessionAmount),
			HasChangedSinceSubmission = groups.Any(x => x.HasChangedSinceSubmission),
			PreviewFingerprint = previewFingerprint,
			FeeHeadGroups = groups,
		};
	}
}
