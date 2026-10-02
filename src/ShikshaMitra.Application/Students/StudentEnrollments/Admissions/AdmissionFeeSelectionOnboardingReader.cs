using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Validates saved Admissions intent against existing Finance assignment evidence without modifying it.</summary>
public class AdmissionFeeSelectionOnboardingReader(IRepository<StudentFeeStructure, Guid> assignments) : ITransientDependency
{
	public async Task<string?> GetBlockerAsync(AdmissionApplication application, bool requireConfirmation = true)
	{
		var selection = application.GetFeeSelection();
		if (selection == null) return null;
		var confirmationBlocker = requireConfirmation && selection.OnboardingConfirmedAt == null
			? "Confirm the fee structure saved during Admissions in the Onboarding Fees tab." : null;
		if (!application.StudentId.HasValue) return confirmationBlocker;
		var assignment = await assignments.FindAsync(a => a.StudentId == application.StudentId && a.AcademicYearId == selection.AcademicYearId);
		if (assignment == null) return confirmationBlocker;
		StudentFeeAssignmentPreviewDto? snapshot;
		try { snapshot = assignment.AssignmentSnapshotJson == null ? null : JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson); }
		catch (JsonException) { snapshot = null; }
		if (assignment.FeeStructureStandardId != selection.StructureId || snapshot == null ||
			snapshot.StudentId != application.StudentId || snapshot.AcademicYearId != selection.AcademicYearId ||
			snapshot.StructureId != selection.StructureId || snapshot.RevisionNumber != selection.RevisionNumber || snapshot.Heads == null ||
			snapshot.Heads.Any(h => h == null) || snapshot.Heads.Where(h => h.Included).Select(h => h.FeeHeadId).Distinct().Count() != snapshot.Heads.Count(h => h.Included) ||
			!selection.Heads.Select(h => h.FeeHeadId).ToHashSet().SetEquals(snapshot.Heads.Where(h => h.Included).Select(h => h.FeeHeadId)))
			return "The Finance assignment differs from the fee structure or fee heads saved during Admissions. Ask Finance to review the assignment.";
		return confirmationBlocker;
	}
}
