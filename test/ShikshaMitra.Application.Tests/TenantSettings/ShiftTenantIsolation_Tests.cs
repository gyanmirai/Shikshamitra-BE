using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class ShiftTenantIsolation_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IShiftManagementAppService _service;
	private readonly IRepository<ShiftManagement, Guid> _repository;
	private readonly ICurrentTenant _tenant;
	private readonly IDataFilter _filter;
	private readonly ShiftReferenceValidator _validator;

	public ShiftTenantIsolation_Tests()
	{
		_service = GetRequiredService<IShiftManagementAppService>();
		_repository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_tenant = GetRequiredService<ICurrentTenant>();
		_filter = GetRequiredService<IDataFilter>();
		_validator = GetRequiredService<ShiftReferenceValidator>();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Lookup_and_mutations_should_isolate_tenants_even_when_filter_is_disabled(bool disableFilter)
	{
		var tenantA = Guid.NewGuid();
		var tenantB = Guid.NewGuid();
		var host = await CreateAsync(null, "Legacy host shift");
		var own = await CreateAsync(tenantA, "Tenant A shift");
		var foreign = await CreateAsync(tenantB, "Tenant B shift");

		using (_tenant.Change(tenantA))
		using (disableFilter ? _filter.Disable<IMultiTenant>() : null)
		{
			if (!disableFilter)
			{
				(await _repository.GetListAsync()).Select(shift => shift.Id).ShouldBe([own.Id]);
				(await _repository.FindAsync(foreign.Id)).ShouldBeNull();
				(await _repository.FindAsync(host.Id)).ShouldBeNull();
			}
			var list = await _service.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
			list.TotalCount.ShouldBe(1);
			list.Items.Select(shift => shift.Id).ShouldBe([own.Id]);
			(await _service.GetAsync(own.Id)).Id.ShouldBe(own.Id);
			await WithUnitOfWorkAsync(() => _validator.EnsureExistsAsync(own.Id));
			await WithUnitOfWorkAsync(() => _validator.EnsureExistsAsync(null));

			foreach (var inaccessible in new[] { host.Id, foreign.Id })
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(inaccessible));
				await Should.ThrowAsync<EntityNotFoundException>(() => _service.UpdateAsync(inaccessible, Input("Attempted overwrite")));
				await Should.ThrowAsync<EntityNotFoundException>(() => _service.DeleteAsync(inaccessible));
				(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => _validator.EnsureExistsAsync(inaccessible))))
					.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
			}
		}

		using (_tenant.Change(tenantB))
		{
			(await _service.GetAsync(foreign.Id)).ShiftName.ShouldBe("Tenant B shift");
		}
		using (_tenant.Change(null))
		{
			(await _service.GetAsync(host.Id)).ShiftName.ShouldBe("Legacy host shift");
			await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(own.Id));
		}
	}

	[Fact]
	public async Task Creation_should_take_tenant_from_the_server_context()
	{
		var tenantId = Guid.NewGuid();
		var shift = await CreateAsync(tenantId, "Scoped creation");
		using (_tenant.Change(tenantId))
		{
			(await _repository.GetAsync(shift.Id)).TenantId.ShouldBe(tenantId);
		}
	}

	private async Task<ShiftManagementDto> CreateAsync(Guid? tenantId, string name)
	{
		using (_tenant.Change(tenantId))
		{
			return await _service.CreateAsync(Input(name));
		}
	}

	private static CreateUpdateShiftManagementDto Input(string name) => new()
	{
		ShiftName = name,
		StartTime = new TimeOnly(8, 0),
		EndTime = new TimeOnly(16, 0)
	};
}
