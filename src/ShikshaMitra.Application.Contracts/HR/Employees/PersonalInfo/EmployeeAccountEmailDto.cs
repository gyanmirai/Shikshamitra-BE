using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeeAccountEmailDto
{
	public Guid UserId { get; set; }
	public string? EmployeeEmail { get; set; }
	public string AccountEmail { get; set; } = string.Empty;
	public string UserName { get; set; } = string.Empty;
	public string ConcurrencyToken { get; set; } = string.Empty;
}

public class ChangeEmployeeAccountEmailDto
{
	[Required, EmailAddress, StringLength(256)]
	public string Email { get; set; } = string.Empty;
	[Required, StringLength(512, MinimumLength = 5)]
	public string Reason { get; set; } = string.Empty;
	public bool ConfirmEmployeeVerified { get; set; }
	[Required, StringLength(64, MinimumLength = 64)]
	public string ConcurrencyToken { get; set; } = string.Empty;
	public Guid IdempotencyKey { get; set; }
}

public interface IEmployeeAccountEmailAppService : IApplicationService
{
	Task<EmployeeAccountEmailDto> GetAsync(Guid employeeId);
	Task<EmployeeAccountEmailDto> ChangeAsync(Guid employeeId, ChangeEmployeeAccountEmailDto input);
}