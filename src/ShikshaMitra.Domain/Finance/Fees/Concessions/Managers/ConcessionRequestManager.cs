using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionRequestManager(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IGuidGenerator guidGenerator,
	ILogger<ConcessionRequestManager> logger) : DomainService
{
	public async Task<ConcessionRequest> CreatePendingAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid concessionTypeId,
		string? remarks,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions)
	{
		var student = await LoadStudentAsync(studentId);
		var concessionType = await LoadConcessionTypeAsync(concessionTypeId, academicYearId);

		if (await concessionRequestRepository.HasPendingForStudentAndTypeAsync(studentId, concessionTypeId))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		}

		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == studentId);

		var requestId = guidGenerator.Create();
		var request = new ConcessionRequest(
			requestId,
			tenantId,
			studentId,
			concessionTypeId,
			academicYearId,
			student.AcademicBoardId ?? Guid.Empty,
			student.AcademicMediumId,
			student.StandardId!.Value,
			student.DivisionId!.Value,
			remarks);

		foreach (var m in BuildFeeHeadMappings(tenantId, requestId, feeHeadConcessions, dueFees, concessionTypeId))
		{
			request.ConcessionFeeHeadMappings.Add(m);
		}

		await concessionRequestRepository.InsertAsync(request, autoSave: true);
		logger.LogDebug("Concession request {RequestId} created for student {StudentId}", request.Id, studentId);
		return request;
	}

	public async Task ReplaceFeeHeadMappingsAsync(
		ConcessionRequest request,
		IReadOnlyList<FeeHeadConcessionInput> feeHeadConcessions)
	{
		request.EnsurePending();
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == request.StudentId);
		var mappings = BuildFeeHeadMappings(
			request.TenantId,
			request.Id,
			feeHeadConcessions,
			dueFees,
			request.ConcessionTypeId);
		request.ReplaceFeeHeadMappings(mappings);
	}

	public List<ConcessionDueFeeAllocation> BuildDueFeeAllocations(
		Guid? tenantId,
		Guid concessionRequestId,
		IReadOnlyList<FeeHeadApprovalInput> feeHeadApprovals,
		List<DueFee> studentDueFees)
	{
		var allocations = new List<ConcessionDueFeeAllocation>();

		foreach (var group in feeHeadApprovals)
		{
			foreach (var line in group.DueFeeAllocations)
			{
				var dueFee = studentDueFees.FirstOrDefault(df => df.Id == line.DueFeeId)
					?? throw new BusinessException(FinanceConcessionErrorCodes.DueFeeNotFound)
						.WithData("DueFeeId", line.DueFeeId);

				if (dueFee.FeeHeadId != group.FeeHeadId)
				{
					throw new BusinessException(FinanceConcessionErrorCodes.DueFeeWrongFeeHead);
				}

				if (line.ConcessionAmount <= 0)
				{
					continue;
				}

				if (dueFee.OutstandingAmount <= 0)
				{
					throw new BusinessException(FinanceConcessionErrorCodes.ConcessionOnFullyPaidDueFee)
						.WithData("DueFeeId", line.DueFeeId);
				}

				allocations.Add(new ConcessionDueFeeAllocation(
					guidGenerator.Create(),
					tenantId,
					concessionRequestId,
					line.DueFeeId,
					group.FeeHeadId,
					line.ConcessionAmount));
			}
		}

		return allocations;
	}

	public List<FeeHeadApprovalInput> BuildAutoApproveAllocations(
		ConcessionRequest request,
		List<DueFee> studentDueFees)
	{
		var result = new List<FeeHeadApprovalInput>();

		foreach (var mapping in request.ConcessionFeeHeadMappings)
		{
			var unpaid = studentDueFees
				.Where(df => df.FeeHeadId == mapping.FeeHeadId && df.OutstandingAmount > 0)
				.OrderBy(df => df.Year)
				.ThenBy(df => df.Month)
				.ToList();

			if (unpaid.Count == 0)
			{
				continue;
			}

			var total = mapping.FeeHeadConcessionAmount;
			var perLine = Math.Floor(total / unpaid.Count * 100) / 100;
			var remainder = total - perLine * unpaid.Count;
			var lines = new List<DueFeeAllocationInput>();

			for (var i = 0; i < unpaid.Count; i++)
			{
				var amount = perLine + (i == unpaid.Count - 1 ? remainder : 0);
				lines.Add(new DueFeeAllocationInput(unpaid[i].Id, amount));
			}

			result.Add(new FeeHeadApprovalInput(mapping.FeeHeadId, total, lines));
		}

		return result;
	}

	public decimal CalculateDiscount(decimal baseAmount, DiscountBasis basis, decimal value)
	{
		if (basis == DiscountBasis.Percentage)
		{
			if (value > 100)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.PercentageOver100);
			}

			return Math.Round(baseAmount * value / 100m, 2, MidpointRounding.AwayFromZero);
		}

		return value;
	}

	private List<ConcessionFeeHeadMapping> BuildFeeHeadMappings(
		Guid? tenantId,
		Guid concessionRequestId,
		IReadOnlyList<FeeHeadConcessionInput> inputs,
		List<DueFee> dueFees,
		Guid concessionTypeId)
	{
		if (inputs.Count == 0)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
				.WithData("Message", "At least one fee head concession is required");
		}

		var mappings = new List<ConcessionFeeHeadMapping>();

		foreach (var input in inputs)
		{
			var headDueFees = dueFees.Where(df => df.FeeHeadId == input.FeeHeadId).ToList();
			if (headDueFees.Count == 0)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.FeeHeadNotInStructure)
					.WithData("FeeHeadId", input.FeeHeadId);
			}

			var baseAmount = headDueFees.Sum(df => (decimal)df.OriginalAmount);
			var discount = CalculateDiscount(baseAmount, input.BasedOn, input.Amount);

			if (discount > baseAmount)
			{
				throw new BusinessException(FinanceConcessionErrorCodes.DiscountExceedsBase)
					.WithData("FeeHeadId", input.FeeHeadId);
			}

			mappings.Add(new ConcessionFeeHeadMapping(
				guidGenerator.Create(),
				tenantId,
				concessionRequestId,
				input.FeeHeadId,
				baseAmount,
				discount,
				baseAmount - discount,
				input.BasedOn,
				input.Amount));
		}

		return mappings;
	}

	private async Task<Student> LoadStudentAsync(Guid studentId)
	{
		var student = await studentRepository.FindAsync(studentId)
			?? throw new BusinessException(FinanceConcessionErrorCodes.StudentNotFound)
				.WithData("StudentId", studentId);

		if (student.StandardId is null || student.DivisionId is null)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch)
				.WithData("StudentId", studentId);
		}

		return student;
	}

	private async Task<ConcessionType> LoadConcessionTypeAsync(Guid concessionTypeId, Guid academicYearId)
	{
		var type = await concessionTypeRepository.FindAsync(concessionTypeId)
			?? throw new BusinessException(FinanceConcessionErrorCodes.ConcessionTypeNotFound);

		if (type.AcademicYearId != academicYearId)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcessionTypeNotFound);
		}

		if (!type.IsActive)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.Inactive)
				.WithData("ConcessionTypeId", concessionTypeId);
		}

		return type;
	}
}

public record FeeHeadConcessionInput(Guid FeeHeadId, DiscountBasis BasedOn, decimal Amount);

public record DueFeeAllocationInput(Guid DueFeeId, decimal ConcessionAmount);

public record FeeHeadApprovalInput(
	Guid FeeHeadId,
	decimal ApprovedConcessionAmount,
	IReadOnlyList<DueFeeAllocationInput> DueFeeAllocations);
