using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
public class ConcessionRequestAppService(
	IConcessionRequestRepository concessionRequestRepository,
	ConcessionRequestManager concessionRequestManager,
	IRepository<Student, Guid> studentRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	ICurrentUser currentUser,
	IGuidGenerator guidGenerator)
	: ApplicationService, IConcessionRequestAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(
		Guid academicYearId,
		CreateConcessionRequestDto input)
	{
		var request = await concessionRequestManager.CreatePendingAsync(
			CurrentTenant.Id,
			academicYearId,
			input.StudentId,
			input.ConcessionTypeId,
			input.Remarks,
			MapFeeHeadInputs(input.FeeHeadConcessions));

		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(
			await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(
		Guid id,
		Guid academicYearId,
		UpdateConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.EnsurePending();

		if (request.StudentId != input.StudentId || request.ConcessionTypeId != input.ConcessionTypeId)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.NotPending);
		}

		await concessionRequestManager.ReplaceFeeHeadMappingsAsync(request, MapFeeHeadInputs(input.FeeHeadConcessions));
		request.UpdateRemarks(input.Remarks);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetAsync(id);
		request.EnsureCanDelete();
		await concessionRequestRepository.DeleteAsync(request);
	}

	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id) =>
		ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(
			await MapDetailAsync(await concessionRequestRepository.GetWithDetailsAsync(id)));

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task UploadDocumentsAsync(Guid id, UploadConcessionDocumentsDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.EnsurePending();

		var docs = input.Documents.Select(d => new ConcessionRequestDocument(
			guidGenerator.Create(),
			CurrentTenant.Id,
			id,
			d.DocumentId,
			d.Url,
			d.Status)).ToList();

		request.ReplaceDocuments(docs);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);
	}

	public async Task<ConcessionApplicationsDto> GetApplicationsAsync(GetConcessionApplicationsInput input)
	{
		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip = input.SkipCount;
		var status = input.Status.HasValue ? (ConcessionRequestStatus)input.Status.Value : (ConcessionRequestStatus?)null;

		var counts = await concessionRequestRepository.GetStatusCountsAsync(
			input.BoardId, input.EducationMediumId);

		var items = await concessionRequestRepository.GetListForApplicationsAsync(
			input.BoardId, input.EducationMediumId, status, skip, pageSize);

		var total = await concessionRequestRepository.CountForApplicationsAsync(
			input.BoardId, input.EducationMediumId, status);

		var studentIds = items.Select(i => i.StudentId).Distinct().ToList();
		var students = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));
		var studentMap = students.ToDictionary(s => s.Id);

		var typeIds = items.Select(i => i.ConcessionTypeId).Distinct().ToList();
		var types = await concessionTypeRepository.GetListAsync(t => typeIds.Contains(t.Id));
		var typeMap = types.ToDictionary(t => t.Id, t => t.Name);

		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
		var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();
		var standards = standardIds.Count > 0
			? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
			: [];
		var divisions = divisionIds.Count > 0
			? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id))
			: [];
		var standardMap = standards.ToDictionary(s => s.Id, s => s.Name);
		var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);

		return new ConcessionApplicationsDto
		{
			PendingCount = counts.GetValueOrDefault(ConcessionRequestStatus.Pending),
			ApprovedCount = counts.GetValueOrDefault(ConcessionRequestStatus.Approved),
			RejectedCount = counts.GetValueOrDefault(ConcessionRequestStatus.Rejected),
			CancelledCount = counts.GetValueOrDefault(ConcessionRequestStatus.Cancelled),
			TotalCount = total,
			CurrentPage = input.SkipCount / pageSize + 1,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(total / (double)pageSize),
			Items = items.Select(r =>
			{
				studentMap.TryGetValue(r.StudentId, out var student);
				typeMap.TryGetValue(r.ConcessionTypeId, out var typeName);
				return new ConcessionApplicationItemDto
				{
					Id = r.Id,
					StudentId = r.StudentId,
					StudentName = FormatStudentName(student),
					GrNumber = student?.GrNo?.ToString() ?? string.Empty,
					RollNo = student?.RollNo?.ToString() ?? string.Empty,
					StandardName = student?.StandardId is Guid sid && standardMap.TryGetValue(sid, out var sn) ? sn : string.Empty,
					DivisionName = student?.DivisionId is Guid did && divisionMap.TryGetValue(did, out var dn) ? dn : string.Empty,
					ConcessionTypeName = typeName ?? string.Empty,
					ConcessionAmount = r.GetTotalRequestedConcession(),
					Status = r.Status,
					StatusName = r.Status.ToString(),
					CreatedAt = r.CreationTime,
				};
			}).ToList(),
		};
	}

	public async Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(
		Guid studentId,
		PagedResultRequestDto input)
	{
		var queryable = await concessionRequestRepository.GetQueryableAsync();
		var all = await AsyncExecuter.ToListAsync(
			queryable.Where(x => x.StudentId == studentId).OrderByDescending(x => x.CreationTime));

		var pageSize = Math.Min(input.MaxResultCount, 100);
		var skip = input.SkipCount;
		var page = all.Skip(skip).Take(pageSize).ToList();

		var student = await studentRepository.GetAsync(studentId);
		var standard = student.StandardId.HasValue ? await standardRepository.FindAsync(student.StandardId.Value) : null;
		var division = student.DivisionId.HasValue ? await divisionRepository.FindAsync(student.DivisionId.Value) : null;

		var items = new List<StudentConcessionRequestItemDto>();
		foreach (var r in page)
		{
			items.Add(await MapStudentItemAsync(r));
		}

		return new StudentConcessionRequestsDto
		{
			StudentDetails = new StudentBasicDetailDto
			{
				StudentId = student.Id,
				Name = FormatStudentName(student),
				RollNo = student.RollNo?.ToString() ?? string.Empty,
				GrNumber = student.GrNo?.ToString() ?? string.Empty,
				StandardName = standard?.Name ?? string.Empty,
				DivisionName = division?.Name ?? string.Empty,
			},
			ConcessionBasicDetail = new ConcessionBasicDetailDto
			{
				TotalApplications = all.Count,
				PendingApplications = all.Count(x => x.Status == ConcessionRequestStatus.Pending),
				ApprovedApplications = all.Count(x => x.Status == ConcessionRequestStatus.Approved),
				RejectedApplications = all.Count(x => x.Status == ConcessionRequestStatus.Rejected),
			},
			TotalCount = all.Count,
			CurrentPage = skip / pageSize + 1,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(all.Count / (double)pageSize),
			Items = items,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == request.StudentId);
		var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

		var groups = request.ConcessionFeeHeadMappings.Select(m =>
		{
			var lines = dueFees
				.Where(df => df.FeeHeadId == m.FeeHeadId)
				.OrderBy(df => df.Year)
				.ThenBy(df => df.Month)
				.Select(df => new DueFeeApprovalLineDto
				{
					DueFeeId = df.Id,
					FeeHeadId = df.FeeHeadId,
					Month = (int)df.Month,
					Year = df.Year,
					MonthLabel = df.Month.ToString(),
					OriginalAmount = (decimal)df.OriginalAmount,
					PaidAmount = (decimal)df.PaidAmount,
					OutstandingAmount = (decimal)df.OutstandingAmount,
					Status = df.Status.ToString(),
					IsSelectable = df.OutstandingAmount > 0,
					MaxConcessionAmount = (decimal)df.OutstandingAmount,
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
			StudentId = request.StudentId,
			TotalRequestedConcessionAmount = request.GetTotalRequestedConcession(),
			FeeHeadGroups = groups,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, ApproveConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == request.StudentId);

		var approvals = input.FeeHeadApprovals?.Select(a => new FeeHeadApprovalInput(
			a.FeeHeadId,
			a.ApprovedConcessionAmount,
			a.DueFeeAllocations.Select(d => new DueFeeAllocationInput(d.DueFeeId, d.ConcessionAmount)).ToList()
		)).ToList() ?? concessionRequestManager.BuildAutoApproveAllocations(request, dueFees);

		foreach (var group in approvals)
		{
			var mapping = request.ConcessionFeeHeadMappings.FirstOrDefault(m => m.FeeHeadId == group.FeeHeadId);
			if (mapping == null)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.FeeHeadNotInStructure);
			}

			var sum = group.DueFeeAllocations.Sum(a => a.ConcessionAmount);
			if (sum != group.ApprovedConcessionAmount && group.DueFeeAllocations.Count > 0)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.AllocationSumMismatch);
			}
		}

		var allocations = concessionRequestManager.BuildDueFeeAllocations(
			CurrentTenant.Id, request.Id, approvals, dueFees);

		request.Approve(currentUser.Id!.Value, input.Message, allocations);

		foreach (var allocation in allocations)
		{
			var dueFee = dueFees.First(df => df.Id == allocation.DueFeeId);
			dueFee.ApplyConcession((double)allocation.ConcessionAmount);
			await dueFeeRepository.UpdateAsync(dueFee);
		}

		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return new ConcessionApprovalResponseDto
		{
			ConcessionRequestId = request.Id,
			StudentConcessionTypeRequestId = request.Id,
			Status = request.Status,
			StatusName = request.Status.ToString(),
			ApprovedAt = request.GetApprovedOn() ?? Clock.Now,
			ApproverId = currentUser.Id!.Value,
			ApproverMessage = input.Message,
			TotalApprovedAmount = allocations.Sum(a => a.ConcessionAmount),
			DueFeeAllocations = allocations.Select(a =>
			{
				var df = dueFees.First(x => x.Id == a.DueFeeId);
				return new ApprovedDueFeeAllocationDto
				{
					DueFeeId = a.DueFeeId,
					FeeHeadId = a.FeeHeadId,
					Month = (int)df.Month,
					Year = df.Year,
					ConcessionAmount = a.ConcessionAmount,
				};
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, RejectConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.Reject(currentUser.Id!.Value, input.Message);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return new ConcessionRejectionResponseDto
		{
			StudentConcessionTypeRequestId = request.Id,
			Status = request.Status,
			StatusName = request.Status.ToString(),
			RejectedAt = Clock.Now,
			RejectionReason = request.RejectionReason ?? input.Message,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Delete)]
	public async Task<ConcessionCancellationResponseDto> CancelAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.Cancel();
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return new ConcessionCancellationResponseDto
		{
			StudentConcessionTypeRequestId = request.Id,
			Status = request.Status,
			StatusName = request.Status.ToString(),
			CancelledAt = Clock.Now,
		};
	}

	private static List<FeeHeadConcessionInput> MapFeeHeadInputs(IEnumerable<FeeHeadConcessionDto> items) =>
		items.Select(i => new FeeHeadConcessionInput(
			i.FeeHeadId,
			(DiscountBasis)i.BasedOn,
			i.Amount)).ToList();

	private async Task<ConcessionRequestDetailDto> MapDetailAsync(ConcessionRequest request)
	{
		var student = await studentRepository.GetAsync(request.StudentId);
		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
		var feeHeads = feeHeadIds.Count > 0
			? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
			: [];
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

		var requiredDocs = type.ConcessionTypeDocuments.Select(d => new ConcessionDocumentRequirementDto
		{
			DocumentId = d.Id,
			DocumentName = d.DocumentName,
			IsUploaded = request.ConcessionRequestDocuments.Any(rd => rd.DocumentId == d.Id),
		}).ToList();

		return new ConcessionRequestDetailDto
		{
			ConcessionRequestId = request.Id,
			StudentId = request.StudentId,
			StudentName = FormatStudentName(student),
			ConcessionTypeId = request.ConcessionTypeId,
			ConcessionTypeName = type.Name,
			Status = request.Status,
			StatusName = request.Status.ToString(),
			TotalConcessionAmount = request.GetTotalRequestedConcession(),
			Remarks = request.Remarks,
			BoardId = request.GetBoardId(),
			EducationMediumId = request.GetEducationMediumId(),
			StandardId = request.GetStandardId(),
			DivisionId = request.GetDivisionId(),
			FeeHeadConcessions = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
			{
				FeeHeadId = m.FeeHeadId,
				FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
				AnnualBaseAmount = m.PreConcessionFeeHeadAmount,
				BasedOn = (int)m.UserSpecifiedConcessionAmountType,
				Amount = m.UserSpecifiedConcessionAmountValue,
				DiscountAmount = m.FeeHeadConcessionAmount,
				NetAmount = m.PostConcessionFeeHeadAmount,
			}).ToList(),
			RequiredDocuments = requiredDocs,
			SubmittedDocuments = request.ConcessionRequestDocuments.Select(d => new ConcessionSubmittedDocumentDto
			{
				DocumentId = d.DocumentId,
				DocumentName = type.ConcessionTypeDocuments.FirstOrDefault(ct => ct.Id == d.DocumentId)?.DocumentName ?? string.Empty,
				Url = d.Url,
				Status = d.Status,
			}).ToList(),
			CreatedAt = request.CreationTime,
		};
	}

	private async Task<StudentConcessionRequestItemDto> MapStudentItemAsync(ConcessionRequest request)
	{
		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
		var feeHeads = feeHeadIds.Count > 0
			? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
			: [];
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

		return new StudentConcessionRequestItemDto
		{
			StudentConcessionTypeRequestId = request.Id,
			ConcessionTypeId = request.ConcessionTypeId,
			ConcessionTypeName = type.Name,
			Status = request.Status,
			TotalAmount = request.GetTotalRequestedConcession(),
			FeeHeads = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
			{
				FeeHeadId = m.FeeHeadId,
				FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
				AnnualBaseAmount = m.PreConcessionFeeHeadAmount,
				BasedOn = (int)m.UserSpecifiedConcessionAmountType,
				Amount = m.UserSpecifiedConcessionAmountValue,
				DiscountAmount = m.FeeHeadConcessionAmount,
				NetAmount = m.PostConcessionFeeHeadAmount,
			}).ToList(),
			SubmittedDocuments = request.ConcessionRequestDocuments.Select(d => new ConcessionSubmittedDocumentDto
			{
				DocumentId = d.DocumentId,
				DocumentName = type.ConcessionTypeDocuments.FirstOrDefault(ct => ct.Id == d.DocumentId)?.DocumentName ?? string.Empty,
				Url = d.Url,
				Status = d.Status,
			}).ToList(),
			CreatedAt = request.CreationTime,
			UpdatedAt = request.LastModificationTime,
			Message = request.ApproverMessage ?? request.RejectionReason,
			ApprovedAt = request.GetApprovedOn(),
		};
	}

	private static string FormatStudentName(Student? student)
	{
		if (student == null)
		{
			return string.Empty;
		}

		return string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(s => !string.IsNullOrWhiteSpace(s)));
	}
}
