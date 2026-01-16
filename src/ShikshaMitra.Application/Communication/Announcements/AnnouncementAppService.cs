using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Communication.Announcements.Dtos;
using ShikshaMitra.Communication.AnnouncementTypes;
using ShikshaMitra.Groups;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Communication.Announcements
{
	[RemoteService(false)]
	[Authorize]
    public class AnnouncementAppService : ApplicationService, IAnnouncementAppService
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IRepository<AnnouncementType, Guid> _announcementTypeRepository;
        private readonly IRepository<IdentityUser, Guid> _userRepository;
        private readonly IRepository<Group, Guid> _groupRepository;

        public AnnouncementAppService(
            IAnnouncementRepository announcementRepository,
            IRepository<AnnouncementType, Guid> announcementTypeRepository,
            IRepository<IdentityUser, Guid> userRepository,
            IRepository<Group, Guid> groupRepository)
        {
            _announcementRepository = announcementRepository;
            _announcementTypeRepository = announcementTypeRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
        }

        public async Task<AnnouncementDto> CreateAsync(AnnouncementCreateDto input)
        {
            // Validate references
            if (!await _announcementTypeRepository.AnyAsync(x => x.Id == input.Type))
            {
                throw new UserFriendlyException("Invalid Announcement Type ID");
            }

            var announcement = ObjectMapper.Map<AnnouncementCreateDto, Announcement>(input);
            announcement.Status = global::ShikshaMitra.AnnouncementStatus.Upcoming; // Default status
            announcement.IsVisible = true; // Default visibility

            await _announcementRepository.InsertAsync(announcement);

            return await MapToDtoAsync(announcement);
        }

        public async Task<AnnouncementDto> GetAsync(Guid id)
        {
            var announcement = await _announcementRepository.GetAsync(id);
            return await MapToDtoAsync(announcement);
        }

        public async Task<PagedResultDto<AnnouncementDto>> GetListAsync(GetAnnouncementListInput input)
        {
            if (input.Page.HasValue || input.PageSize.HasValue)
            {
                input.MaxResultCount = input.PageSize ?? 10;
                input.SkipCount = ((input.Page ?? 1) - 1) * input.MaxResultCount;
            }

            var query = await _announcementRepository.GetQueryableAsync();

            if (input.AcademicMediumId != Guid.Empty)
            {
                query = query.Where(x => x.AcademicMediumId == input.AcademicMediumId);
            }

            if (input.Status != 0) // 0 = All
            {
                query = query.Where(x => x.Status == input.Status);
            }



            var totalCount = await AsyncExecuter.CountAsync(query);

            query = query.OrderByDescending(x => x.CreationTime)
                         .PageBy(input);

            var items = await AsyncExecuter.ToListAsync(query);

            var dtos = new List<AnnouncementDto>();
            foreach (var item in items)
            {
                dtos.Add(await MapToDtoAsync(item));
            }

            return new PagedResultDto<AnnouncementDto>(totalCount, dtos);
        }

        public async Task<AnnouncementDto> UpdateAsync(Guid id, AnnouncementUpdateDto input)
        {
            var announcement = await _announcementRepository.GetAsync(id);

            if (announcement.Status == global::ShikshaMitra.AnnouncementStatus.Published || announcement.Status == global::ShikshaMitra.AnnouncementStatus.Completed)
            {
                // Logic: allow update only if not completed? Contract doesn't say. 
                // Using standard leniency or strictness. Let's allow but maybe warn.
                // For now, standard update.
            }

             if (!await _announcementTypeRepository.AnyAsync(x => x.Id == input.Type))
            {
                throw new UserFriendlyException("Invalid Announcement Type ID");
            }

            ObjectMapper.Map(input, announcement);
            await _announcementRepository.UpdateAsync(announcement);

            return await MapToDtoAsync(announcement);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _announcementRepository.DeleteAsync(id);
        }

        public async Task PublishAsync(Guid id)
        {
            var announcement = await _announcementRepository.GetAsync(id);
            if (announcement.Status == global::ShikshaMitra.AnnouncementStatus.Published)
            {
                throw new UserFriendlyException("Announcement is already published", code: "409");
            }
            
            announcement.Status = global::ShikshaMitra.AnnouncementStatus.Published;
            await _announcementRepository.UpdateAsync(announcement);
        }

        public async Task UpdateVisibilityAsync(Guid id, AnnouncementVisibilityInput input)
        {
            var announcement = await _announcementRepository.GetAsync(id);
            announcement.IsVisible = input.IsVisible;
            await _announcementRepository.UpdateAsync(announcement);
        }

        private async Task<AnnouncementDto> MapToDtoAsync(Announcement announcement)
        {
            var dto = ObjectMapper.Map<Announcement, AnnouncementDto>(announcement);

            // Fetch Type Name
            var type = await _announcementTypeRepository.FindAsync(announcement.TypeId);
            if (type != null)
            {
                dto.Type = new LookupDto { Id = type.Id, Name = type.Name };
            }

            // Fetch Creator Name
            if (announcement.CreatorId.HasValue)
            {
                var user = await _userRepository.FindAsync(announcement.CreatorId.Value);
                if (user != null)
                {
                    dto.AnnouncedBy = new AnnouncedByDto { Id = user.Id, Name = $"{user.Name} {user.Surname}" };
                }
            }

            // Map Audience
            if (announcement.Audience != null && announcement.Audience.Any())
            {
                var groups = await _groupRepository.GetListAsync(x => announcement.Audience.Contains(x.Id));
                dto.Audience = groups.Select(g => new LookupDto { Id = g.Id, Name = g.Name }).ToList();
            }
            else
            {
                dto.Audience = new List<LookupDto>();
            }

            return dto;
        }
    }
}
