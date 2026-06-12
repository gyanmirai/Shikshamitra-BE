using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public class StudentFeeStructureAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	DueFeeManager dueFeeManager,
	FinanceScopeAccessService financeScopeAccessService
) : ApplicationService, IStudentFeeStructureAppService
{
	public async Task AssignAsync(AssignStudentFeeStructureDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId)
			?? throw new BusinessException("Student with given id does not exist.");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);

		if (student.FeeStructureId != null)
		{
			throw new BusinessException(
				"Fee structure is already assigned to the student. Please unassign the existing fee structure before assigning a new one.");
		}

		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new BusinessException("Fee structure with given id does not exist.");

		student.AssignFeeStructure(input.StandardFeeStructureId);

		await studentFeeStructureRepository.InsertAsync(
			new StudentFeeStructure(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				studentId: student.Id,
				feeStructureStandardId: feeStructure.Id),
			autoSave: false);

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var optionalDetails = input.FeeHeadDetails?.Select(fd => new CustomOptionalFeeHeadDetail
		{
			FeeHeadId = fd.FeeHeadId,
			Amount = fd.Amount,
			PayableMonth = fd.PayableMonth,
			PayableYear = fd.PayableYear,
			StartPayableMonth = fd.StartPayableMonth,
			StartPayableYear = fd.StartPayableYear,
			EndPayableMonth = fd.EndPayableMonth,
			EndPayableYear = fd.EndPayableYear
		}).ToList() ?? new List<CustomOptionalFeeHeadDetail>();

		var dueFees = await dueFeeManager.GenerateForStudentAsync(
			student.Id,
			feeStructure,
			academicYear,
			input.SelectedOptionalFeeHeadIds,
			optionalDetails);
	
		await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	public async Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input)
	{
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new BusinessException("Fee structure with given id does not exist.");

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);

		var optionalDetails = input.FeeHeadDetails?.Select(fd => new CustomOptionalFeeHeadDetail
		{
			FeeHeadId = fd.FeeHeadId,
			Amount = fd.Amount,
			PayableMonth = fd.PayableMonth,
			PayableYear = fd.PayableYear,
			StartPayableMonth = fd.StartPayableMonth,
			StartPayableYear = fd.StartPayableYear,
			EndPayableMonth = fd.EndPayableMonth,
			EndPayableYear = fd.EndPayableYear
		}).ToList() ?? new List<CustomOptionalFeeHeadDetail>();

		var result = new BulkAssignStudentFeeStructureResultDto();

		foreach (var studentId in input.StudentIds)
		{
			try
			{
				var student = await studentRepository.GetAsync(studentId);
				if (student == null)
				{
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId,
						Success = false,
						ErrorMessage = "Student not found."
					});
					result.SkippedCount++;
					continue;
				}

				await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

				if (student.FeeStructureId != null)
				{
					result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
					{
						StudentId = studentId,
						Success = false,
						ErrorMessage = "Fee structure already assigned."
					});
					result.SkippedCount++;
					continue;
				}

				student.AssignFeeStructure(input.StandardFeeStructureId);

				await studentFeeStructureRepository.InsertAsync(
					new StudentFeeStructure(
						id: GuidGenerator.Create(),
						tenantId: CurrentTenant.Id,
						studentId: student.Id,
						feeStructureStandardId: feeStructure.Id),
					autoSave: false);

				var dueFees = await dueFeeManager.GenerateForStudentAsync(
					student.Id,
					feeStructure,
					academicYear,
					input.SelectedOptionalFeeHeadIds,
					optionalDetails);

				await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);

				result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
				{
					StudentId = studentId,
					Success = true
				});
				result.SuccessCount++;
			}
			catch (Exception ex)
			{
				result.Results.Add(new BulkAssignStudentFeeStructureResultItemDto
				{
					StudentId = studentId,
					Success = false,
					ErrorMessage = ex.Message
				});
				result.SkippedCount++;
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();

		return result;
	}


	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId)
			?? throw new BusinessException("Student with given id does not exist.");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);

		// Prefer the fee-collection assignment row; fall back to Student.FeeStructureId.
		var feeStructureId = assignment?.FeeStructureStandardId ?? student.FeeStructureId;

		if (feeStructureId == null)
			throw new BusinessException("No fee structure has been assigned to this student.");

		var feeStructure = await feeStructureRepository.GetAsync(feeStructureId.Value)
			?? throw new BusinessException("Assigned fee structure could not be found.");

		var dueFees = await GetDueFeesForStudentAsync(studentId);
		var selectedFeeHeadIds = dueFees.Select(d => d.FeeHeadId).ToHashSet();

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructureId.Value);

		List<StudentFeeHeadDto> feeHeads;
		if (dueFees.Count > 0)
		{
			feeHeads = await BuildFeeHeadsFromDueFeesAsync(dueFees);
			EnrichFeeHeadsFromDetails(feeHeads, details, selectedFeeHeadIds);
		}
		else if (details.Count > 0)
		{
			feeHeads = BuildFeeHeadsFromDetails(details, selectedFeeHeadIds);
		}
		else
		{
			feeHeads = [];
		}

		return new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId = assignment?.Id ?? feeStructure.Id,
			StandardFeeStructureId = feeStructure.Id,
			StudentId = studentId,
			StandardId = feeStructure.StandardId,
			StructureName = feeStructure.Name,
			FeeHeads = feeHeads,
		};
	}

	private async Task<List<DueFee>> GetDueFeesForStudentAsync(Guid studentId)
	{
		var queryable = await dueFeeRepository.GetQueryableAsync();
		return queryable
			.Where(d => d.StudentId == studentId)
			.OrderBy(d => d.Year)
			.ThenBy(d => d.Month)
			.ToList();
	}

	private static List<StudentFeeHeadDto> BuildFeeHeadsFromDetails(
		List<FeeStructureDetail> details,
		HashSet<Guid> selectedFeeHeadIds)
	{
		return details
			.GroupBy(d => d.FeeHeadId)
			.Select(group =>
			{
				var detail = group.First();
				return new StudentFeeHeadDto
				{
					FeeHeadId = detail.FeeHeadId,
					FeeHeadName = detail.FeeHead?.Name ?? string.Empty,
					CategoryName = detail.FeeHead?.FeeCategory?.Name ?? string.Empty,
					Amount = (decimal)group.Sum(d => d.Amount),
					IsOptional = detail.IsOptional,
					IsOneTime = detail.IsOneTime,
					IsSelected = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId),
				};
			})
			.ToList();
	}

	private async Task<List<StudentFeeHeadDto>> BuildFeeHeadsFromDueFeesAsync(List<DueFee> dueFees)
	{
		var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id);

		return feeHeadIds.Select(feeHeadId =>
		{
			var lines = dueFees.Where(d => d.FeeHeadId == feeHeadId).OrderBy(l => l.Year).ThenBy(l => l.Month).ToList();
			feeHeadMap.TryGetValue(feeHeadId, out var feeHead);

			var isOneTime = lines.All(l => l.IsOneTime);
			var firstLine = lines.FirstOrDefault();
			var lastLine = lines.LastOrDefault();

			return new StudentFeeHeadDto
			{
				FeeHeadId = feeHeadId,
				FeeHeadName = feeHead?.Name ?? string.Empty,
				CategoryName = feeHead?.FeeCategory?.Name ?? string.Empty,
				Amount = (decimal)lines.Sum(l => l.OriginalAmount),
				IsOptional = false,
				IsOneTime = isOneTime,
				IsSelected = true,

				// Reconstructed custom selection properties
				SelectedAmount = firstLine != null ? (decimal)firstLine.OriginalAmount : 0m,
				PayableMonth = isOneTime && firstLine != null ? firstLine.Month : null,
				PayableYear = isOneTime && firstLine != null ? firstLine.Year : null,
				StartPayableMonth = !isOneTime && firstLine != null ? firstLine.Month : null,
				StartPayableYear = !isOneTime && firstLine != null ? firstLine.Year : null,
				EndPayableMonth = !isOneTime && lastLine != null ? lastLine.Month : null,
				EndPayableYear = !isOneTime && lastLine != null ? lastLine.Year : null,
			};
		}).ToList();
	}

	private static void EnrichFeeHeadsFromDetails(
		List<StudentFeeHeadDto> feeHeads,
		List<FeeStructureDetail> details,
		HashSet<Guid> selectedFeeHeadIds)
	{
		if (details.Count == 0)
			return;

		var detailByFeeHead = details
			.GroupBy(d => d.FeeHeadId)
			.ToDictionary(g => g.Key, g => g.First());

		foreach (var head in feeHeads)
		{
			if (!detailByFeeHead.TryGetValue(head.FeeHeadId, out var detail))
				continue;

			head.FeeHeadName = string.IsNullOrEmpty(head.FeeHeadName) ? detail.FeeHead?.Name ?? string.Empty : head.FeeHeadName;
			head.CategoryName = string.IsNullOrEmpty(head.CategoryName)
				? detail.FeeHead?.FeeCategory?.Name ?? string.Empty
				: head.CategoryName;
			head.IsOptional = detail.IsOptional;
			head.IsOneTime = detail.IsOneTime;
			head.IsSelected = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId);
		}

		foreach (var detailGroup in details.GroupBy(d => d.FeeHeadId))
		{
			if (feeHeads.Any(h => h.FeeHeadId == detailGroup.Key))
				continue;

			var detail = detailGroup.First();
			feeHeads.Add(new StudentFeeHeadDto
			{
				FeeHeadId = detail.FeeHeadId,
				FeeHeadName = detail.FeeHead?.Name ?? string.Empty,
				CategoryName = detail.FeeHead?.FeeCategory?.Name ?? string.Empty,
				Amount = (decimal)detailGroup.Sum(d => d.Amount),
				IsOptional = detail.IsOptional,
				IsOneTime = detail.IsOneTime,
				IsSelected = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId),
			});
		}
	}
}
