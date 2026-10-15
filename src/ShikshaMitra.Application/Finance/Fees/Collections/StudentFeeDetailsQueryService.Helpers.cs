using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class StudentFeeDetailsQueryService
{
	private async Task<StudentSnapshot> GetStudentSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
	{
		var students = await studentRepository.GetQueryableAsync();
		var snapshot = students.Where(student => student.Id == studentId).Select(student => new StudentSnapshot
		{
			Id = student.Id, FirstName = student.FirstName,
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
		return await AsyncExecuter.FirstOrDefaultAsync(snapshot, cancellationToken)
			?? throw new BusinessException("Student not found");
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
		return allocQ.Where(a => reconMetaIds.Contains(a.FeeRecordMetadataId))
			.GroupBy(a => a.DueFeeId)
			.Select(g => new { DueFeeId = g.Key, Amount = g.Sum(a => a.Amount) })
			.ToDictionary(x => x.DueFeeId, x => x.Amount);
	}

	private async Task<Dictionary<Guid, double>> GetInReconciliationByFeeHeadAsync(Guid studentId)
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var collQ = await feeCollectionRecordRepository.GetQueryableAsync();
		var reconMetaIds = metaQ.Where(m => m.StudentId == studentId && m.Status == FeeCollectionStatus.InReconciliation).Select(m => m.Id);
		return collQ.Where(r => r.StudentId == studentId && reconMetaIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.FeeHeadId)
			.Select(g => new { FeeHeadId = g.Key, Amount = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.FeeHeadId, x => x.Amount);
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
