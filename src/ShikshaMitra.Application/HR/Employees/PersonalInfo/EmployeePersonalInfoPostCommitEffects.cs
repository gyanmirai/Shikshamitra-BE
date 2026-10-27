using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoPostCommitEffects(
	EmployeeDocumentManager employeeDocumentManager,
	IEmployeeEmailService emailService,
	IDistributedEventBus distributedEventBus,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager,
	IClock clock,
	ILogger<EmployeePersonalInfoPostCommitEffects> logger)
	: ITransientDependency
{
	public async Task ExecuteAsync(
		Employee employee,
		CreateEmployeePersonalInfoDto input,
		EmployeePersonalInfoUserAccount userAccount,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		if (profilePictureContent != null &&
		    !string.IsNullOrWhiteSpace(profilePictureFileName))
		{
			await employeeDocumentManager.UploadProfilePictureAsync(
				employee.Id,
				profilePictureFileName,
				profilePictureContent);
		}

		var fullName = $"{input.FirstName} {input.LastName}".Trim();
		logger.LogInformation(
			"Staff created: EmployeeId={EmployeeId}, Code={EmployeeCode}, Email={Email}",
			employee.Id,
			employee.EmployeeCode,
			input.Email);
		await distributedEventBus.PublishAsync(new HR.Events.EmployeeCreatedEto
		{
			EmployeeId = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = fullName,
			Email = input.Email,
			TenantId = currentTenant.Id,
			CreatedAt = clock.Now
		});
		await emailService.SendWelcomeEmailAsync(
			input.Email,
			fullName,
			userAccount.Username,
			userAccount.Password);
	}

	public void Register(
		Employee employee,
		EmployeePersonalInfoDto employeeDto,
		CreateEmployeePersonalInfoDto input,
		EmployeePersonalInfoUserAccount userAccount,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		var currentUnitOfWork = unitOfWorkManager.Current ??
			throw new InvalidOperationException(
				"Deferred staff creation effects require an active unit of work.");
		var employeeId = employee.Id;
		var employeeCode = employee.EmployeeCode;
		var employeeEmail = input.Email;
		var fullName = $"{input.FirstName} {input.LastName}".Trim();
		var tenantId = currentTenant.Id;
		var createdAt = clock.Now;

		currentUnitOfWork.OnCompleted(async () =>
		{
			using var tenantScope = currentTenant.Change(tenantId);
			if (profilePictureContent != null &&
			    !string.IsNullOrWhiteSpace(profilePictureFileName))
			{
				try
				{
					using var profileUnitOfWork = unitOfWorkManager.Begin(
						new AbpUnitOfWorkOptions { IsTransactional = true },
						requiresNew: true);
					employeeDto.ProfilePictureBlobName =
						await employeeDocumentManager.UploadProfilePictureAsync(
							employeeId,
							profilePictureFileName,
							profilePictureContent);
					await profileUnitOfWork.CompleteAsync();
				}
				catch (Exception exception)
				{
					logger.LogError(
						exception,
						"Staff {EmployeeId} was finalized, but its profile picture could not be persisted.",
						employeeId);
				}
			}

			logger.LogInformation(
				"Staff created: EmployeeId={EmployeeId}, Code={EmployeeCode}, Email={Email}",
				employeeId,
				employeeCode,
				employeeEmail);

			try
			{
				await distributedEventBus.PublishAsync(new HR.Events.EmployeeCreatedEto
				{
					EmployeeId = employeeId,
					EmployeeCode = employeeCode,
					FullName = fullName,
					Email = employeeEmail,
					TenantId = tenantId,
					CreatedAt = createdAt
				});
			}
			catch (Exception exception)
			{
				logger.LogError(
					exception,
					"Staff {EmployeeId} was finalized, but its creation event could not be published.",
					employeeId);
			}

			await emailService.SendWelcomeEmailAsync(
				employeeEmail,
				fullName,
				userAccount.Username,
				userAccount.Password);
		});
	}
}
