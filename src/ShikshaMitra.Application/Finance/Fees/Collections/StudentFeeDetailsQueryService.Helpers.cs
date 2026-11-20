using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using PaymentTerm = ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.PaymentTerm;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class StudentFeeDetailsQueryService
{
	internal async Task<IReadOnlyList<Guid>> GetLedgerFeeHeadOrderAsync(
		Guid structureId, CancellationToken cancellationToken = default)
	{
		var structures = LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<FeeStructure, Guid>>();
		var structure = await structures.FindAsync(structureId, includeDetails: false, cancellationToken);
		if (structure is null) return [];
		var savedOrder = FeeStructureHeadOrder.Read(structure);
		if (savedOrder.Count > 0) return savedOrder;

		var details = LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<FeeStructureDetail, Guid>>();
		var lines = await details.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(lines.Where(line => line.FeeStructureId == structureId)
			.OrderBy(line => line.CreationTime).ThenBy(line => line.Id).Select(line => line.FeeHeadId),
			cancellationToken);
	}

	private static IReadOnlyList<AssignmentHeadDto> ReadLedgerAssignmentHeads(string? snapshotJson)
	{
		if (string.IsNullOrEmpty(snapshotJson)) return [];
		try
		{
			return JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(snapshotJson)?.Heads ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	internal async Task<Dictionary<Guid, string>> GetLedgerPaymentTermNamesAsync(
		IReadOnlyCollection<DueFee> dues, Guid academicYearId, IReadOnlyList<AssignmentHeadDto> assignmentHeads,
		CancellationToken cancellationToken = default)
	{
		var pinnedNames = assignmentHeads
			.Where(head => !string.IsNullOrWhiteSpace(head.PaymentTermName))
			.ToLookup(head => (head.FeeHeadId, head.PaymentTermId, head.PaymentTermVersionId));
		var names = new Dictionary<Guid, string>();
		var unresolved = new List<DueFee>();
		foreach (var due in dues.Where(due => due.PaymentTermId.HasValue))
		{
			var head = pinnedNames[(due.FeeHeadId, due.PaymentTermId, due.PaymentTermVersionId)].FirstOrDefault();
			if (head?.PaymentTermName is { } name)
				names.Add(due.Id, name);
			else
				unresolved.Add(due);
		}
		if (unresolved.Count == 0) return names;

		var termIds = unresolved.Select(due => due.PaymentTermId!.Value).Distinct().ToArray();
		var repository = LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<PaymentTerm, Guid>>();
		var terms = (await repository.GetListAsync(term => term.AcademicYearId == academicYearId
			&& Enumerable.Contains(termIds, term.Id), cancellationToken: cancellationToken))
			.ToDictionary(term => term.Id, term => term.Name);
		foreach (var due in unresolved)
			if (terms.TryGetValue(due.PaymentTermId!.Value, out var name))
				names.Add(due.Id, name);
		return names;
	}

	private async Task<StudentSnapshot> GetStudentSnapshotAsync(Guid studentId, Guid academicYearId,
		CancellationToken cancellationToken)
	{
		var students = await studentRepository.GetQueryableAsync();
		var snapshot = students.Where(student => student.Id == studentId).Select(student => new StudentSnapshot
		{
			Id = student.Id, AcademicYearId = student.AcademicYearId, FirstName = student.FirstName,
			MiddleName = student.MiddleName, LastName = student.LastName,
			AcademicBoardId = student.AcademicBoardId, BoardType = student.AcademicBoard == null ? null : student.AcademicBoard.Type,
			AcademicMediumId = student.AcademicMediumId,
			EducationMediumName = student.AcademicMedium == null ? string.Empty : student.AcademicMedium.Name,
			StandardId = student.StandardId, StandardName = student.Standard == null ? string.Empty : student.Standard.Name,
			DivisionId = student.DivisionId, DivisionName = student.Division == null ? string.Empty : student.Division.Name,
			GrNo = student.GrNo, RollNo = student.RollNo,
			ContactNo = student.ContactDetail == null ? string.Empty : student.ContactDetail.PrimaryContactNo ?? string.Empty,
			Email = student.ContactDetail == null ? string.Empty : student.ContactDetail.Email ?? string.Empty,
			FeeStructureId = student.FeeStructureId,
		});
		var result = await AsyncExecuter.FirstOrDefaultAsync(snapshot, cancellationToken)
			?? throw new BusinessException("Student not found");
		if (result.AcademicYearId == academicYearId) return result;
		var enrollments = await LazyServiceProvider
			.LazyGetRequiredService<IRepository<StudentEnrollment, Guid>>().GetQueryableAsync();
		var standards = await LazyServiceProvider
			.LazyGetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync();
		var historical = await AsyncExecuter.FirstOrDefaultAsync(
			from enrollment in enrollments
			join standard in standards on enrollment.StandardId equals (Guid?)standard.Id
			where enrollment.StudentId == studentId && enrollment.AcademicYearId == academicYearId
			select new
			{
				enrollment.StandardId, enrollment.DivisionId, enrollment.RollNumber,
				StandardName = standard.Name,
				BoardId = standard.EducationMediumStage.EducationMedium.EducationBoardId,
				BoardType = standard.EducationMediumStage.EducationMedium.EducationBoard.Type,
				MediumId = standard.EducationMediumStage.EducationMediumId,
				MediumName = standard.EducationMediumStage.EducationMedium.Name
			}, cancellationToken);
		if (historical == null) throw new BusinessException("Student enrollment not found for selected year");
		result.AcademicBoardId = historical.BoardId;
		result.BoardType = historical.BoardType;
		result.AcademicMediumId = historical.MediumId;
		result.EducationMediumName = historical.MediumName;
		result.StandardId = historical.StandardId;
		result.StandardName = historical.StandardName;
		result.DivisionId = historical.DivisionId;
		result.RollNo = historical.RollNumber;
		result.FeeStructureId = await AsyncExecuter.FirstOrDefaultAsync(
			(await studentFeeStructureRepository.GetQueryableAsync())
				.Where(assignment => assignment.StudentId == studentId &&
					assignment.AcademicYearId == academicYearId)
				.Select(assignment => (Guid?)assignment.FeeStructureStandardId), cancellationToken);
		if (historical.DivisionId.HasValue)
		{
			var divisions = await LazyServiceProvider
				.LazyGetRequiredService<IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>()
				.GetQueryableAsync();
			result.DivisionName = await AsyncExecuter.FirstOrDefaultAsync(
				divisions.Where(division => division.Id == historical.DivisionId.Value)
					.Select(division => division.Name), cancellationToken) ?? string.Empty;
		}
		return result;
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
		StandardId = student.StandardId ?? Guid.Empty, StandardName = student.StandardName,
		DivisionId = student.DivisionId ?? Guid.Empty, DivisionName = student.DivisionName,
		ContactNo = student.ContactNo, Email = student.Email,
		FeeStructureName = string.Empty, IsFeeStructureAssigned = student.FeeStructureId.HasValue,
	};

	private static StudentSummaryDto BuildStudentSummary(StudentSnapshot student) => new()
	{
		Id = student.Id,
		Name = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
		GrNo = student.GrNo?.ToString() ?? string.Empty,
		StandardName = student.StandardName, DivisionName = student.DivisionName,
		RollNo = student.RollNo?.ToString() ?? string.Empty,
		MobileNo = string.IsNullOrWhiteSpace(student.ContactNo) ? null : student.ContactNo,
	};

	private static string BuildFullName(string? first, string? middle, string? last) =>
		string.Join(" ", new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)));

	private async Task<Dictionary<Guid, double>> GetInReconciliationByDueFeeAsync(Guid studentId, Guid academicYearId)
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var allocQ = await dueFeeAllocationRepository.GetQueryableAsync();
		var reconMetaIds = metaQ.Where(m => m.StudentId == studentId && m.AcademicYearId == academicYearId && m.Status == FeeCollectionStatus.InReconciliation).Select(m => m.Id);
		var amounts = await AsyncExecuter.ToListAsync(allocQ.Where(a => reconMetaIds.Contains(a.FeeRecordMetadataId))
			.GroupBy(a => a.DueFeeId)
			.Select(g => new { DueFeeId = g.Key, Amount = g.Sum(a => a.Amount) }));
		return amounts.ToDictionary(x => x.DueFeeId, x => x.Amount);
	}

	private async Task<Dictionary<Guid, double>> GetInReconciliationByFeeHeadAsync(Guid studentId)
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var collQ = await feeCollectionRecordRepository.GetQueryableAsync();
		var reconMetaIds = metaQ.Where(m => m.StudentId == studentId && m.Status == FeeCollectionStatus.InReconciliation).Select(m => m.Id);
		var amounts = await AsyncExecuter.ToListAsync(collQ.Where(r => r.StudentId == studentId && reconMetaIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.FeeHeadId)
			.Select(g => new { FeeHeadId = g.Key, Amount = g.Sum(r => r.Amount) }));
		return amounts.ToDictionary(x => x.FeeHeadId, x => x.Amount);
	}

	private sealed class StudentSnapshot
	{
		public Guid Id { get; init; }
		public Guid? AcademicYearId { get; init; }
		public string? FirstName { get; init; }
		public string? MiddleName { get; init; }
		public string? LastName { get; init; }
		public Guid? AcademicBoardId { get; set; }
		public EducationBoardType? BoardType { get; set; }
		public Guid AcademicMediumId { get; set; }
		public string EducationMediumName { get; set; } = string.Empty;
		public Guid? StandardId { get; set; }
		public string StandardName { get; set; } = string.Empty;
		public Guid? DivisionId { get; set; }
		public string DivisionName { get; set; } = string.Empty;
		public int? GrNo { get; init; }
		public int? RollNo { get; set; }
		public string ContactNo { get; init; } = string.Empty;
		public string Email { get; init; } = string.Empty;
		public Guid? FeeStructureId { get; set; }
	}
}
