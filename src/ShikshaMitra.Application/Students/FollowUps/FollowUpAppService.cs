using ShikshaMitra.Students.StudentEnrollments.FollowUps;
using ShikshaMitra.Students.StudentEnrollments;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.Guids;
using Volo.Abp;
using System.Linq;

namespace ShikshaMitra.Students.FollowUps;

public class FollowUpAppService(IStudentEnrollmentRepository studentEnrollmentRepository, IGuidGenerator guidGenerator)
	: ShikshaMitraAppService, IFollowUpAppService
{
	[RemoteService(false)]
	public async Task<FollowUpWithStatusAndDateDto> AddFollowUpAsync(Guid studentEnrollmentId, AddFollowUpDto input)
	{
		// Fetch the StudentEnrollment by InquiryNumber (as before)
		StudentEnrollment studentEnrollment = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);

		// Create a new FollowUpMessage entity
		var followUpMessage = new FollowUpMessage(
			id: guidGenerator.Create(),          // new Guid if your entity expects an ID
			tenantId: CurrentTenant.Id,           // current tenant ID
			studentEnrollmentId: studentEnrollment.Id,     // foreign key property in FollowUpMessage
			message: input.Message,
			author: CurrentUser?.Name ?? "System",
			timestamp: Clock.Now
		);

		// Add the new follow-up to the StudentEnrollment's collection
		studentEnrollment.FollowUps.Add(followUpMessage);

		// Update status if provided
		if (input.InquiryStatusType.HasValue)
		{
			studentEnrollment = studentEnrollment.ChangeInquiryStatus(input.InquiryStatusType.Value);
		}

		// Update due date if provided
		if (input.DueDate.HasValue)
		{
			studentEnrollment = studentEnrollment.ChnageDueDate(input.DueDate.Value);
		}

		// Save changes
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the updated DTO
		return new FollowUpWithStatusAndDateDto
		{
			FollowUps = ObjectMapper.Map<List<FollowUpMessage>, List<FollowUpMessageDto>>(studentEnrollment.FollowUps.ToList()),
			InquiryStatusType = studentEnrollment.FollowUpStatusType,
			DueDate = studentEnrollment.DueDate
		};
	}

	[RemoteService(false)]
	public async Task<FollowUpWithStatusAndDateDto> GetFollowUpDetailsAsync(Guid studentEnrollmentId)
	{
		StudentEnrollment? inquiry = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);

		List<FollowUpMessageDto> followUpMessageDtolist = ObjectMapper.Map<List<FollowUpMessage>, List<FollowUpMessageDto>>([.. inquiry.FollowUps]);

		return new FollowUpWithStatusAndDateDto
		{
			FollowUps = followUpMessageDtolist,
			InquiryStatusType = inquiry.FollowUpStatusType,
			DueDate = inquiry.DueDate
		};
	}

	[RemoteService(false)]
	public async Task UpdateStatusAndDueDateAsync(Guid studentEnrollmentId, FollowUpStatusType status, DateTimeOffset? dueDate)
	{
		var inquiry = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);
		inquiry = inquiry.ChangeInquiryStatus(status);
		inquiry = inquiry.ChnageDueDate(dueDate);
		await studentEnrollmentRepository.UpdateAsync(inquiry);
	}

	
}
