using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Route("api/students/records/changes")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
public class StudentRecordChangesController(IStudentRecordChangeAppService service) : AbpController
{
	[HttpGet] public Task<PagedResultDto<StudentRecordChangeDto>> GetListAsync([FromQuery] StudentRecordChangeQueryDto input) => service.GetListAsync(input);
	[HttpGet("{id:guid}")] public Task<StudentRecordChangeDto> GetAsync(Guid id) => service.GetAsync(id);
	[HttpGet("students/{studentId:guid}/snapshot/{section}")]
	public Task<StudentRecordSectionSnapshotDto> GetSnapshotAsync(Guid studentId, string section) => service.GetSnapshotAsync(studentId, section);
	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordChangeDto> CreateAsync([FromBody] CreateStudentRecordChangeDto input) => service.CreateAsync(input);
	[HttpPost("{id:guid}/approve"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public Task<StudentRecordChangeDto> ApproveAsync(Guid id, [FromBody] StudentRecordDecisionDto input) => service.ApproveAsync(id, input);
	[HttpPost("{id:guid}/reject"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public Task<StudentRecordChangeDto> RejectAsync(Guid id, [FromBody] StudentRecordDecisionDto input) => service.RejectAsync(id, input);
	[HttpPost("{id:guid}/cancel"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel)]
	public Task<StudentRecordChangeDto> CancelAsync(Guid id, [FromBody] StudentRecordDecisionDto input) => service.CancelAsync(id, input);
}
