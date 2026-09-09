using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using Volo.Abp.Application.Dtos;
using Volo.Abp;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	public async Task<ConcessionApplicationsDto> GetApplicationsAsync(GetConcessionApplicationsInput input)
	{
		if (input.AcademicYearId == Guid.Empty || input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		if (input.Status.HasValue && !Enum.IsDefined(typeof(ConcessionRequestStatus), input.Status.Value))
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
				.WithData("Status", input.Status.Value);
		FinanceClassDivisionFilterValidator.Validate(input);

		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip     = input.SkipCount;
		var status   = input.Status.HasValue ? (ConcessionRequestStatus)input.Status.Value : (ConcessionRequestStatus?)null;

		var counts = await concessionRequestRepository.GetStatusCountsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId,
			scopedStudentIdsQuery: scopedStudentIds);
		var items  = await concessionRequestRepository.GetListForApplicationsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, status, input.StandardId, input.DivisionId, skip, pageSize,
			scopedStudentIdsQuery: scopedStudentIds);
		var total  = await concessionRequestRepository.CountForApplicationsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, status, input.StandardId, input.DivisionId,
			scopedStudentIdsQuery: scopedStudentIds);

		var studentIds  = items.Select(i => i.StudentId).Distinct().ToList();
		var students    = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));
		var studentMap  = students.ToDictionary(s => s.Id);

		var typeIds     = items.Select(i => i.ConcessionTypeId).Distinct().ToList();
		var types       = await concessionTypeRepository.GetListAsync(t => typeIds.Contains(t.Id));
		var typeMap     = types.ToDictionary(t => t.Id, t => t.Name);

		var standardIds = items.Select(s => s.StandardId).Distinct().ToList();
		var divisionIds = items.Select(s => s.DivisionId).Distinct().ToList();
		var standards   = standardIds.Count > 0 ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id)) : [];
		var divisions   = divisionIds.Count > 0 ? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id)) : [];
		var standardMap = standards.ToDictionary(s => s.Id, s => s.Name);
		var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);

		return new ConcessionApplicationsDto
		{
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
				return new ConcessionApplicationItemDto
				{
					Id = r.Id, StudentId = r.StudentId,
					StudentName        = FormatStudentName(student),
					GrNumber           = student?.GrNo?.ToString() ?? string.Empty,
					RollNo             = student?.RollNo?.ToString() ?? string.Empty,
					StandardName       = standardMap.GetValueOrDefault(r.StandardId, string.Empty),
					DivisionName       = divisionMap.GetValueOrDefault(r.DivisionId, string.Empty),
					ConcessionTypeName = typeName ?? string.Empty,
					ConcessionAmount   = r.GetTotalRequestedConcession(),
					RequestedConcessionAmount = r.GetTotalRequestedConcession(),
					ApprovedConcessionAmount = r.GetTotalApprovedConcession(),
					Status             = r.Status,
					StatusName         = r.Status.ToString(),
					CreatedAt          = r.CreationTime,
				};
			}).ToList(),
		};
	}

	public async Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(
		Guid studentId, Guid academicYearId, PagedResultRequestDto input)
	{
		if (academicYearId == Guid.Empty || input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

		var queryable = await concessionRequestRepository.GetQueryableAsync();
		var all = await AsyncExecuter.ToListAsync(
			queryable.Where(x => x.StudentId == studentId && x.AcademicYearId == academicYearId)
				.OrderByDescending(x => x.CreationTime));

		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip     = input.SkipCount;
		var page     = all.Skip(skip).Take(pageSize).ToList();

		var student  = await studentRepository.GetAsync(studentId) ?? throw new BusinessException("Student not found.");
		var standard = student?.Standard?.Name;
		var division = student?.Division?.Name;

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

		var items = page.Select(r =>
		{
			typeMap.TryGetValue(r.ConcessionTypeId, out var type);
			return MapStudentItem(r, type, feeHeadMap);
		}).ToList();

		return new StudentConcessionRequestsDto
		{
			StudentDetails = new StudentBasicDetailDto
			{
				StudentId    = student.Id,
				Name         = FormatStudentName(student),
				RollNo       = student.RollNo?.ToString() ?? string.Empty,
				GrNumber     = student.GrNo?.ToString() ?? string.Empty,
				StandardName = standard ?? string.Empty,
				DivisionName = division ?? string.Empty,
			},
			ConcessionBasicDetail = new ConcessionBasicDetailDto
			{
				TotalApplications    = all.Count,
				PendingApplications  = all.Count(x => x.Status == ConcessionRequestStatus.Pending),
				ApprovedApplications = all.Count(x => x.Status == ConcessionRequestStatus.Approved),
				RejectedApplications = all.Count(x => x.Status == ConcessionRequestStatus.Rejected),
			},
			TotalCount  = all.Count,
			CurrentPage = skip / pageSize + 1,
			PageSize    = pageSize,
			TotalPages  = (int)Math.Ceiling(all.Count / (double)pageSize),
			Items       = items,
		};
	}

	public async Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id)
	{
		var request    = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		var assignment = await studentFeeStructureRepository.GetAsync(request.StudentFeeStructureId);
		var applicableIds = assignment.GetConcessionApplicableFeeHeadIds().ToHashSet();
		var dueFees    = await dueFeeRepository.GetListAsync(df =>
			df.StudentId == request.StudentId && applicableIds.Contains(df.FeeHeadId));
		var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
		var feeHeads   = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
		var version = await financePolicyVersionRepository.GetAsync(request.ConcessionPolicyVersionId);
		var policy = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		dueFees = request.Status == ConcessionRequestStatus.Pending
			? ConcessionRequestManager.GetEligibleDueFees(dueFees, policy.ApplyOn)
			: dueFees.Where(df => request.ConcessionDueFeeAllocations.Any(a => a.DueFeeId == df.Id)).ToList();

		var groups = request.ConcessionFeeHeadMappings.Select(m =>
		{
			var lines = dueFees
				.Where(df => df.FeeHeadId == m.FeeHeadId)
				.OrderBy(df => df.Year).ThenBy(df => df.Month)
				.Select(df =>
				{
					var allocation = request.ConcessionDueFeeAllocations.FirstOrDefault(x => x.DueFeeId == df.Id);
					var reversed = allocation is null ? 0m : request.ConcessionReversals
						.Where(x => x.ConcessionDueFeeAllocationId == allocation.Id).Sum(x => x.Amount);
					var reversible = Math.Max(0m, (allocation?.ConcessionAmount ?? 0m) - reversed);
					var isReversal = request.Status is ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed;
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
						IsSelectable = isReversal ? reversible > 0m : df.OutstandingAmount > 0m,
						MaxConcessionAmount = isReversal ? reversible : df.OutstandingAmount,
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
				DueFeeLines = lines,
			};
		}).ToList();

		return new ConcessionApprovalContextDto
		{
			ConcessionRequestId = request.Id,
			StudentId           = request.StudentId,
			ConcessionPolicyName = policy.PolicyName,
			ConcessionPolicyCode = policy.PolicyCode,
			ConcessionPolicyVersion = policy.Version,
			RequestConcurrencyStamp = request.ConcurrencyStamp,
			TotalRequestedConcessionAmount = request.GetTotalRequestedConcession(),
			FeeHeadGroups = groups,
		};
	}
}
