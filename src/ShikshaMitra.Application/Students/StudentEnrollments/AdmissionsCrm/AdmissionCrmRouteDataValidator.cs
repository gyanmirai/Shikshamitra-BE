using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteDataValidator(
	IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
	IStudentRepository studentRepository) : ApplicationService, ITransientDependency
{
	public async Task ValidateAsync(
		AdmissionCrmRouteType type,
		AdmissionCrmSpecialRouteDataDto data,
		AdmissionInquiryLead lead)
	{
		if (type == AdmissionCrmRouteType.SiblingPriority && !data.RelatedStudentId.HasValue)
			throw new BusinessException("AdmissionCrm:VerifiedSiblingStudentRequired");
		if (type == AdmissionCrmRouteType.ReturningStudent && !data.FormerStudentId.HasValue)
			throw new BusinessException("AdmissionCrm:FormerStudentRequired");
		if (data.RelatedStudentId.HasValue)
		{
			if (type == AdmissionCrmRouteType.SiblingPriority)
				await EnsureSiblingRelationshipAsync(lead, data.RelatedStudentId.Value);
			else
				await studentRepository.GetAsync(data.RelatedStudentId.Value);
		}
		if (data.FormerStudentId.HasValue)
			await studentRepository.GetAsync(data.FormerStudentId.Value);
		if (type == AdmissionCrmRouteType.StaffChild && string.IsNullOrWhiteSpace(data.AuthoritativeReference))
			throw new BusinessException("AdmissionCrm:AuthoritativeStaffReferenceRequired");
		if (type == AdmissionCrmRouteType.RteEws && (string.IsNullOrWhiteSpace(data.AuthorityName) ||
			string.IsNullOrWhiteSpace(data.AllotmentNumber) || string.IsNullOrWhiteSpace(data.AllotmentRound) || !data.AdmissionDeadline.HasValue))
			throw new BusinessException("AdmissionCrm:RteAllotmentDetailsRequired");
		if (type == AdmissionCrmRouteType.RteEws)
		{
			var queryable = await assignmentRepository.GetQueryableAsync();
			var assignments = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
				entity.TenantId == CurrentTenant.Id && entity.RouteType == AdmissionCrmRouteType.RteEws));
			if (assignments.Any(entity => string.Equals(
				AdmissionCrmRouteMapper.Deserialize<AdmissionCrmSpecialRouteDataDto>(entity.RouteDataJson)?.AllotmentNumber,
				data.AllotmentNumber,
				StringComparison.OrdinalIgnoreCase)))
				throw new BusinessException("AdmissionCrm:RteAllotmentAlreadyImported");
		}
		if (type is AdmissionCrmRouteType.Transfer or AdmissionCrmRouteType.Lateral or AdmissionCrmRouteType.Interstate &&
			(string.IsNullOrWhiteSpace(data.PreviousSchool) || string.IsNullOrWhiteSpace(data.PreviousBoard) || string.IsNullOrWhiteSpace(data.LastClassCompleted)))
			throw new BusinessException("AdmissionCrm:TransferHistoryRequired");
		if (type == AdmissionCrmRouteType.OverseasReturn &&
			(string.IsNullOrWhiteSpace(data.Country) || string.IsNullOrWhiteSpace(data.EquivalenceStatus)))
			throw new BusinessException("AdmissionCrm:OverseasEquivalenceDetailsRequired");
	}

	private async Task EnsureSiblingRelationshipAsync(AdmissionInquiryLead lead, Guid relatedStudentId)
	{
		if (lead.StudentId == relatedStudentId)
		{
			throw new BusinessException("AdmissionCrm:SiblingRelationshipNotVerified")
				.WithData("RelatedStudentId", relatedStudentId);
		}

		var relatedStudent = await studentRepository.GetInquiryForUpdateAsync(relatedStudentId);
		if (relatedStudent == null || relatedStudent.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:VerifiedSiblingStudentRequired")
				.WithData("RelatedStudentId", relatedStudentId);
		}

		var leadPhone = NormalizeContact(lead.PrimaryContactNo);
		var leadEmail = NormalizeEmail(lead.Email);
		var relatedPhones = new[]
			{
				relatedStudent.ContactDetail?.PrimaryContactNo,
				relatedStudent.ContactDetail?.SecondaryContactNo,
				relatedStudent.FamilyDetail?.FatherDetails?.ContactNo,
				relatedStudent.FamilyDetail?.MotherDetails?.ContactNo,
				relatedStudent.FamilyDetail?.GuardianDetails?.ContactNo
			}
			.Select(NormalizeContact)
			.Where(value => value != null)
			.ToHashSet(StringComparer.Ordinal);
		var relatedEmails = new[]
			{
				relatedStudent.ContactDetail?.Email,
				relatedStudent.FamilyDetail?.FatherDetails?.Email,
				relatedStudent.FamilyDetail?.MotherDetails?.Email,
				relatedStudent.FamilyDetail?.GuardianDetails?.Email
			}
			.Select(NormalizeEmail)
			.Where(value => value != null)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if ((leadPhone == null || !relatedPhones.Contains(leadPhone)) &&
			(leadEmail == null || !relatedEmails.Contains(leadEmail)))
		{
			throw new BusinessException("AdmissionCrm:SiblingRelationshipNotVerified")
				.WithData("RelatedStudentId", relatedStudentId);
		}
	}

	private static string? NormalizeContact(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		var digits = new string(value.Where(char.IsDigit).ToArray());
		return digits.Length == 0 ? null : digits;
	}

	private static string? NormalizeEmail(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
