using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public class StudentFeeDetailsQueryService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService
) : ApplicationService, ITransientDependency
{
	private static void EnsureNotEmpty(Guid value, string fieldName)
	{
		if (value != Guid.Empty) return;

		throw new AbpValidationException(
			"Validation failed",
			[
				new ValidationResult($"{fieldName} is required.", new[] { fieldName })
			]);
	}

	public async Task<FeeCollectFormDto> GetCollectFormAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);

		var studentInfo = BuildStudentInfo(student);

		var feeStructureAssignment = await studentFeeStructureRepository
			.FirstOrDefaultAsync(x => x.StudentId == studentId);

		var ledger = new List<FeeLedgerMonthDto>();
		var feeSummary = new FeeSummaryDto();

		if (feeStructureAssignment != null)
		{
			var dueFeeQ = await dueFeeRepository.GetQueryableAsync();
			var dueFees = dueFeeQ
				.Where(d => d.StudentId == studentId)
				.OrderBy(d => d.Year).ThenBy(d => d.Month)
				.ToList();

			var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

			var grouped = dueFees
				.GroupBy(d => (d.Year, d.Month))
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

			foreach (var group in grouped)
			{
				var lines = group.Select(d => new FeeLedgerLineDto
				{
					DueFeeId = d.Id,
					FeeHeadId = d.FeeHeadId,
					FeeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty),
					Total = d.OriginalAmount,
					Paid = d.PaidAmount,
					Due = d.OutstandingAmount,
					Status = d.OutstandingAmount <= 0d
						? FeeCollectionStatus.Paid
						: (d.PaidAmount > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				}).ToList();

				ledger.Add(new FeeLedgerMonthDto
				{
					Month = group.Key.Month.ToString(),
					Year = group.Key.Year,
					TotalDue = lines.Sum(l => l.Due),
					Items = lines,
				});
			}

			feeSummary = new FeeSummaryDto
			{
				TotalFee = dueFees.Sum(d => d.OriginalAmount),
				PaidFee = dueFees.Sum(d => d.PaidAmount),
				PendingFee = dueFees.Sum(d => d.OutstandingAmount),
			};
		}

		var chargesQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = chargesQ
			.Where(c => c.StudentId == studentId
					 && c.AcademicYearId == academicYearId
					 && c.Status == ChargeStatus.Approved)
			.ToList();

		var chargeDtos = new List<ChargeDetailDto>();
		if (approvedCharges.Count > 0)
		{
			var chargeCollQ  = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeMetaQ  = await feeRecordMetadataRepository.GetQueryableAsync();

			var reversedTxIds = chargeMetaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos = approvedCharges.Select(c =>
			{
				var net = (double)c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid);
				return new ChargeDetailDto
				{
					ChargeId = c.Id,
					Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		return new FeeCollectFormDto
		{
			Student = studentInfo,
			Summary = feeSummary,
			Ledger = ledger,
			Charges = chargeDtos,
		};
	}

	public async Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);
		var studentSummary = BuildStudentSummary(student);

		var feeHeadSummaries = new List<FeeHeadSummaryDto>();

		var feeStructureAssignment = await studentFeeStructureRepository
			.FirstOrDefaultAsync(x => x.StudentId == studentId);

		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();

		if (feeStructureAssignment != null)
		{
			var detailsQ = await feeStructureDetailRepository.GetQueryableAsync();
			var feeHeadsQ = await feeHeadRepository.GetQueryableAsync();

			var feeHeadDefs = (from detail in detailsQ
							   join fh in feeHeadsQ on detail.FeeHeadId equals fh.Id
							   where detail.FeeStructureId == feeStructureAssignment.FeeStructureStandardId
							   select new
							   {
								   detail.FeeHeadId,
								   FeeHeadName = fh.Name,
								   Description = fh.Description ?? string.Empty,
								   NetAmount = detail.Amount,
							   }).ToList();

			var feeCollQ  = await feeCollectionRecordRepository.GetQueryableAsync();

			var reversedTxIds = metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerHead = feeCollQ
				.Where(r => r.StudentId == studentId
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.FeeHeadId)
				.Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.FeeHeadId, x => x.Paid);

			feeHeadSummaries = feeHeadDefs.Select(def =>
			{
				var paid = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
				var pending = Math.Max(0d, def.NetAmount - paid);
				return new FeeHeadSummaryDto
				{
					FeeHeadId = def.FeeHeadId,
					FeeHeadName = def.FeeHeadName,
					Amount = def.NetAmount,
					PaidAmount = paid,
					PendingAmount = pending,
					Description = def.Description,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		var chargesQ = await chargeRepository.GetQueryableAsync();
		var studentCharges = chargesQ
			.Where(c => c.StudentId == studentId
					 && c.AcademicYearId == academicYearId
					 && c.Status == ChargeStatus.Approved)
			.ToList();

		List<FeeChargeDetailDto>? chargeDtos = null;
		if (studentCharges.Count > 0)
		{
			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeReversedTxIds = metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId
						 && !chargeReversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos = studentCharges.Select(c =>
			{
				var net = (double)c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid);
				return new FeeChargeDetailDto
				{
					ChargeId = c.Id,
					Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		var paymentMethodQ = await paymentMethodRecordRepository.GetQueryableAsync();

		var metadataForStudent = metaQ
			.Where(m => m.StudentId == studentId && m.AcademicYearId == academicYearId)
			.OrderByDescending(m => m.CreationTime)
			.ToList();

		List<FeePaymentHistoryDto>? payments = null;
		if (metadataForStudent.Count > 0)
		{
			var metadataIds = metadataForStudent.Select(m => m.Id).ToList();
			var methodsByMetadata = paymentMethodQ
				.Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
				.GroupBy(p => p.FeeRecordMetadataId)
				.Select(g => new { MetadataId = g.Key, PaymentMethods = g.Select(p => p.PaymentMethod).ToList() })
				.ToDictionary(x => x.MetadataId, x => x.PaymentMethods);

			payments = metadataForStudent.Select(m => new FeePaymentHistoryDto
			{
				PaymentId = m.Id,
				ReceiptNumber = m.ReceiptNumber,
				Date = DateOnly.FromDateTime(m.CreationTime.Date),
				Amount = m.TotalPaidFee,
				PaymentMethods = methodsByMetadata.TryGetValue(m.Id, out var methods) ? methods : [],
				ReceiptUrl = null,
				Status = m.Status,
			}).ToList();
		}

		var approvedConcessions = await concessionRequestRepository.GetListAsync(
			r => r.StudentId == studentId && r.Status == ConcessionRequestStatus.Approved);

		List<ConcessionDetailDto>? concessions = null;
		if (approvedConcessions.Count > 0)
		{
			var typeIds = approvedConcessions.Select(r => r.ConcessionTypeId).Distinct().ToList();
			var concessionTypes = await concessionTypeRepository.GetListAsync(ct => typeIds.Contains(ct.Id));
			var typeMap = concessionTypes.ToDictionary(ct => ct.Id, ct => ct.Name);

			concessions = approvedConcessions.Select(r =>
			{
				var total = r.ConcessionFeeHeadMappings.Sum(m => (double)m.FeeHeadConcessionAmount);
				return new ConcessionDetailDto
				{
					ConcessionRequestConcessionTypeId = r.Id,
					ConcessionTypeName = typeMap.GetValueOrDefault(r.ConcessionTypeId, string.Empty),
					Amount = total > 0d ? total : null,
				};
			}).ToList();
		}

		var totalFee = feeHeadSummaries.Sum(fh => fh.Amount);
		var totalPaid = feeHeadSummaries.Sum(fh => fh.PaidAmount);
		var totalPending = feeHeadSummaries.Sum(fh => fh.PendingAmount ?? 0d);
		var totalConcession = concessions?.Sum(c => c.Amount ?? 0d) ?? 0d;

		return new StudentFeeDetailsDto
		{
			Student = studentSummary,
			FeeOverview = new FeeOverviewDto
			{
				TotalFee = totalFee,
				PaidFee = totalPaid,
				PendingFee = totalPending,
				ConcessionAmount = totalConcession > 0d ? totalConcession : null,
			},
			FeeHeads = feeHeadSummaries,
			Charges = chargeDtos,
			Payments = payments,
			Concessions = concessions,
		};
	}

	private async Task<StudentSnapshot> GetStudentSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
	{
		var student = await studentRepository.FindAsync(studentId) ?? throw new BusinessException("Student not found");

		return new StudentSnapshot
		{
			Id = student.Id,
			FirstName = student.FirstName,
			MiddleName = student.MiddleName,
			LastName = student.LastName,
			AcademicBoardId = student.AcademicBoardId,
			BoardType = student.AcademicBoard?.Type,
			AcademicMediumId = student.AcademicMediumId,
			EducationMediumName = student.AcademicMedium?.Name ?? string.Empty,
			StandardId = student.StandardId,
			StandardName = student.Standard?.Name ?? string.Empty,
			DivisionId = student.DivisionId,
			DivisionName = student.Division?.Name ?? string.Empty,
			GrNo = student.GrNo,
			RollNo = student.RollNo,
			ContactNo = student.ContactDetail?.PrimaryContactNo ?? string.Empty,
			Email = student.ContactDetail?.Email ?? string.Empty,
			FeeStructureId = student.FeeStructureId,
		};
	}

	private static StudentInfoDto BuildStudentInfo(StudentSnapshot student) => new()
	{
		StudentId = student.Id,
		StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
		GrNo = student.GrNo?.ToString() ?? string.Empty,
		RollNo = student.RollNo?.ToString() ?? string.Empty,
		BoardId = student.AcademicBoardId ?? Guid.Empty,
		BoardName = student.BoardType?.ToString() ?? string.Empty,
		EducationMediumId = student.AcademicMediumId,
		EducationMediumName = student.EducationMediumName,
		StandardId = student.StandardId ?? Guid.Empty,
		StandardName = student.StandardName,
		DivisionId = student.DivisionId ?? Guid.Empty,
		DivisionName = student.DivisionName,
		ContactNo = student.ContactNo,
		Email = student.Email,
		FeeStructureName = string.Empty,
		IsFeeStructureAssigned = student.FeeStructureId.HasValue,
	};

	private static StudentSummaryDto BuildStudentSummary(StudentSnapshot student) => new()
	{
		Id = student.Id,
		Name = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
		StandardName = student.StandardName,
		DivisionName = student.DivisionName,
		RollNo = student.RollNo?.ToString() ?? string.Empty,
		MobileNo = string.IsNullOrWhiteSpace(student.ContactNo) ? null : student.ContactNo,
	};

	private static string BuildFullName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last };
		return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
	}

	private sealed class StudentSnapshot
	{
		public Guid Id { get; init; }
		public string? FirstName { get; init; }
		public string? MiddleName { get; init; }
		public string? LastName { get; init; }
		public Guid? AcademicBoardId { get; init; }
		public EducationBoardType? BoardType { get; init; }
		public Guid AcademicMediumId { get; init; }
		public string EducationMediumName { get; init; } = string.Empty;
		public Guid? StandardId { get; init; }
		public string StandardName { get; init; } = string.Empty;
		public Guid? DivisionId { get; init; }
		public string DivisionName { get; init; } = string.Empty;
		public int? GrNo { get; init; }
		public int? RollNo { get; init; }
		public string ContactNo { get; init; } = string.Empty;
		public string Email { get; init; } = string.Empty;
		public Guid? FeeStructureId { get; set; }
	}
}
