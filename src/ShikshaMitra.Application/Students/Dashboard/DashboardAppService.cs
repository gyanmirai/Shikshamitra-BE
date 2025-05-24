using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Dashboard
{
	public class DashboardAppService(
		IRepository<DashboardLayout, Guid> dashboardLayoutRepository,
		IGuidGenerator guidGenerator,
		ICurrentUser currentUser) : ShikshaMitraAppService
	{
		public async Task<DashboardLayoutDto> GetLayoutAsync()
		{
			var userId = currentUser.GetId();
			var dashboardLayout = await dashboardLayoutRepository.GetAsync(x => x.UserId == userId);

			return dashboardLayout == null
				? new DashboardLayoutDto
				{
					Layouts = "{}",
					VisibleWidgets = "{}"
				}
				: new DashboardLayoutDto
				{
					Id = dashboardLayout.Id,
					Layouts = dashboardLayout.Layouts,
					VisibleWidgets = dashboardLayout.VisibleWidgets
				};
		}

		public async Task<DashboardLayoutDto> SaveLayoutAsync(SaveDashboardLayoutDto input)
		{
			var userId = currentUser.GetId();
			var dashboardLayout = await dashboardLayoutRepository.GetAsync(x => x.UserId == userId);

			if (dashboardLayout == null)
			{
				dashboardLayout = new DashboardLayout(guidGenerator.Create(), userId, input.Layouts, input.VisibleWidgets, CurrentTenant.Id);
				await dashboardLayoutRepository.InsertAsync(dashboardLayout);
			}
			else
			{
				dashboardLayout.Layouts = input.Layouts;
				dashboardLayout.VisibleWidgets = input.VisibleWidgets;
				await dashboardLayoutRepository.UpdateAsync(dashboardLayout);
			}

			return new DashboardLayoutDto
			{
				Id = dashboardLayout.Id,
				Layouts = dashboardLayout.Layouts,
				VisibleWidgets = dashboardLayout.VisibleWidgets
			};
		}
	}
}
