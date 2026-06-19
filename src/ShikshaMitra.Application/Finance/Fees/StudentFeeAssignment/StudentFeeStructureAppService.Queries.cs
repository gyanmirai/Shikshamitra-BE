using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId)
			?? throw new Volo.Abp.BusinessException("Student with given id does not exist.");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

		var assignment    = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		var feeStructureId = assignment?.FeeStructureStandardId ?? student.FeeStructureId;

		if (feeStructureId == null)
			throw new Volo.Abp.BusinessException("No fee structure has been assigned to this student.");

		var feeStructure = await feeStructureRepository.GetAsync(feeStructureId.Value)
			?? throw new Volo.Abp.BusinessException("Assigned fee structure could not be found.");

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
			feeHeads = BuildFeeHeadsFromDetails(details, selectedFeeHeadIds);
		else
			feeHeads = [];

		return new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId  = assignment?.Id ?? feeStructure.Id,
			StandardFeeStructureId = feeStructure.Id,
			StudentId              = studentId,
			StandardId             = feeStructure.StandardId,
			StructureName          = feeStructure.Name,
			FeeHeads               = feeHeads,
		};
	}

	private async Task<List<DueFee>> GetDueFeesForStudentAsync(Guid studentId)
	{
		var queryable = await dueFeeRepository.GetQueryableAsync();
		return queryable.Where(d => d.StudentId == studentId)
			.OrderBy(d => d.Year).ThenBy(d => d.Month).ToList();
	}

	private async Task<List<StudentFeeHeadDto>> BuildFeeHeadsFromDueFeesAsync(List<DueFee> dueFees)
	{
		var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
		var feeHeads   = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id);

		return feeHeadIds.Select(feeHeadId =>
		{
			var lines = dueFees.Where(d => d.FeeHeadId == feeHeadId).OrderBy(l => l.Year).ThenBy(l => l.Month).ToList();
			feeHeadMap.TryGetValue(feeHeadId, out var feeHead);

			var isOneTime  = lines.All(l => l.IsOneTime);
			var firstLine  = lines.FirstOrDefault();
			var lastLine   = lines.LastOrDefault();

			return new StudentFeeHeadDto
			{
				FeeHeadId    = feeHeadId,
				FeeHeadName  = feeHead?.Name ?? string.Empty,
				CategoryName = feeHead?.FeeCategory?.Name ?? string.Empty,
				Amount       = (decimal)lines.Sum(l => l.OriginalAmount),
				IsOptional   = false,
				IsOneTime    = isOneTime,
				IsSelected   = true,
				SelectedAmount      = firstLine != null ? (decimal)firstLine.OriginalAmount : 0m,
				PayableMonth        = isOneTime && firstLine != null ? firstLine.Month : null,
				PayableYear         = isOneTime && firstLine != null ? firstLine.Year : null,
				StartPayableMonth   = !isOneTime && firstLine != null ? firstLine.Month : null,
				StartPayableYear    = !isOneTime && firstLine != null ? firstLine.Year : null,
				EndPayableMonth     = !isOneTime && lastLine != null ? lastLine.Month : null,
				EndPayableYear      = !isOneTime && lastLine != null ? lastLine.Year : null,
			};
		}).ToList();
	}

	private static List<StudentFeeHeadDto> BuildFeeHeadsFromDetails(
		List<FeeStructureDetail> details, HashSet<Guid> selectedFeeHeadIds)
		=> details.GroupBy(d => d.FeeHeadId).Select(group =>
		{
			var detail = group.First();
			return new StudentFeeHeadDto
			{
				FeeHeadId    = detail.FeeHeadId,
				FeeHeadName  = detail.FeeHead?.Name ?? string.Empty,
				CategoryName = detail.FeeHead?.FeeCategory?.Name ?? string.Empty,
				Amount       = (decimal)group.Sum(d => d.Amount),
				IsOptional   = detail.IsOptional,
				IsOneTime    = detail.IsOneTime,
				IsSelected   = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId),
			};
		}).ToList();

	private static void EnrichFeeHeadsFromDetails(
		List<StudentFeeHeadDto> feeHeads, List<FeeStructureDetail> details, HashSet<Guid> selectedFeeHeadIds)
	{
		if (details.Count == 0) return;
		var detailByFeeHead = details.GroupBy(d => d.FeeHeadId).ToDictionary(g => g.Key, g => g.First());

		foreach (var head in feeHeads)
		{
			if (!detailByFeeHead.TryGetValue(head.FeeHeadId, out var detail)) continue;
			head.FeeHeadName  = string.IsNullOrEmpty(head.FeeHeadName) ? detail.FeeHead?.Name ?? string.Empty : head.FeeHeadName;
			head.CategoryName = string.IsNullOrEmpty(head.CategoryName) ? detail.FeeHead?.FeeCategory?.Name ?? string.Empty : head.CategoryName;
			head.IsOptional   = detail.IsOptional;
			head.IsOneTime    = detail.IsOneTime;
			head.IsSelected   = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId);
		}

		foreach (var detailGroup in details.GroupBy(d => d.FeeHeadId))
		{
			if (feeHeads.Any(h => h.FeeHeadId == detailGroup.Key)) continue;
			var detail = detailGroup.First();
			feeHeads.Add(new StudentFeeHeadDto
			{
				FeeHeadId    = detail.FeeHeadId,
				FeeHeadName  = detail.FeeHead?.Name ?? string.Empty,
				CategoryName = detail.FeeHead?.FeeCategory?.Name ?? string.Empty,
				Amount       = (decimal)detailGroup.Sum(d => d.Amount),
				IsOptional   = detail.IsOptional,
				IsOneTime    = detail.IsOneTime,
				IsSelected   = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId),
			});
		}
	}

	private static List<CustomOptionalFeeHeadDetail> BuildOptionalDetails(
		IEnumerable<StudentFeeHeadDetailDto>? items) =>
		items?.Select(fd => new CustomOptionalFeeHeadDetail
		{
			FeeHeadId         = fd.FeeHeadId,
			Amount            = fd.Amount,
			PayableMonth      = fd.PayableMonth,
			PayableYear       = fd.PayableYear,
			StartPayableMonth = fd.StartPayableMonth,
			StartPayableYear  = fd.StartPayableYear,
			EndPayableMonth   = fd.EndPayableMonth,
			EndPayableYear    = fd.EndPayableYear,
		}).ToList() ?? new List<CustomOptionalFeeHeadDetail>();
}
