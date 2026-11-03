using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	// List states use batched reads over the already scoped page. Detailed source
	// verification belongs to the context/preview, not an N+1 calculation per row.
	private async Task PopulateAssignmentStatesAsync(List<AssignmentStudentDto> page, Guid yearId)
	{
		if (page.Count == 0) return;
		var studentIds = page.Select(row => row.StudentId).ToArray();
		var assignments = await studentFeeStructureRepository.GetListAsync(a => a.AcademicYearId == yearId && Enumerable.Contains(studentIds, a.StudentId));
		var accounts = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>()
			.GetListAsync(a => a.AcademicYearId == yearId && a.StudentId.HasValue && Enumerable.Contains(studentIds, a.StudentId.Value));
		var applications = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().GetQueryableAsync();
		var offerings = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetQueryableAsync();
		var cycles = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetQueryableAsync();
		var sources = await AsyncExecuter.ToListAsync(from application in applications
			join offering in offerings on application.AdmissionOfferingId equals offering.Id
			join cycle in cycles on offering.AdmissionCycleId equals cycle.Id
			where cycle.AcademicYearId == yearId &&
				(application.StudentId.HasValue && Enumerable.Contains(studentIds, application.StudentId.Value) ||
				 application.EnrolledStudentId.HasValue && Enumerable.Contains(studentIds, application.EnrolledStudentId.Value))
			select application);
		foreach (var row in page)
		{
			var assignment = assignments.SingleOrDefault(a => a.StudentId == row.StudentId);
			row.IsFeeStructureAssigned = assignment != null;
			row.AssignmentState = assignment != null ? "Assigned" : "Unassigned";
			row.AssignmentSource = assignment?.FinanceFeeAccountId.HasValue == true ? "Admission" : "Finance";
			if (assignment != null) continue;
			var matching = sources.Where(a => a.StudentId == row.StudentId || a.EnrolledStudentId == row.StudentId).ToList();
			if (matching.Count > 0 || accounts.Any(a => a.StudentId == row.StudentId)) row.AssignmentSource = "Admission";
			if (matching.Count > 1) { row.AssignmentState = "NeedsAttention"; continue; }
			var source = matching.SingleOrDefault();
			var prepared = accounts.Where(a => a.StudentId == row.StudentId && a.AssignmentSnapshotJson != null).ToList();
			if (prepared.Count > 1 || prepared.Any(a => a.ApplicationId != source?.Id || a.StudentFeeStructureId.HasValue))
				row.AssignmentState = "NeedsAttention";
			else if (prepared.Count == 1) row.AssignmentState = "Prepared";
			else if (source?.GetFeeSelection() != null) row.AssignmentState = "Selected";
		}
	}
}
