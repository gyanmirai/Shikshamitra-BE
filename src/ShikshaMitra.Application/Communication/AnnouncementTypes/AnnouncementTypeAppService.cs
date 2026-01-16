using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Communication.AnnouncementTypes.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
	[RemoteService(false)]
	public class AnnouncementTypeAppService : ShikshaMitraAppService, IAnnouncementTypeAppService
    {
        private readonly IRepository<AnnouncementType, Guid> _repository;

        public AnnouncementTypeAppService(IRepository<AnnouncementType, Guid> repository)
        {
            _repository = repository;
        }

        public async Task<AnnouncementTypeListResultDto<AnnouncementTypeDto>> GetListAsync(GetAnnouncementTypesInput input)
        {
            int page = input.Page ?? 1;
            int pageSize = input.PageSize ?? 10;
            int skipCount = (page - 1) * pageSize;

            var queryable = await _repository.GetQueryableAsync();
            queryable = queryable.Where(x => x.AcademicMediumId == input.AcademicMediumId);

            long totalCount = await AsyncExecuter.CountAsync(queryable);
            
            queryable = queryable.OrderByDescending(x => x.CreationTime)
                                 .PageBy(skipCount, pageSize);

            var entities = await AsyncExecuter.ToListAsync(queryable);
            var dtos = ObjectMapper.Map<List<AnnouncementType>, List<AnnouncementTypeDto>>(entities);

            return new AnnouncementTypeListResultDto<AnnouncementTypeDto>(dtos, totalCount, page, pageSize);
        }

        public async Task<AnnouncementTypeDto> GetAsync(Guid id, Guid academicMediumId)
        {
            var entity = await _repository.GetAsync(id);
            if (entity.AcademicMediumId != academicMediumId)
            {
                throw new UserFriendlyException("Entity not found for this Academic Medium.");
            }
            return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(entity);
        }

        public async Task<AnnouncementTypeDto> CreateAsync(Guid academicMediumId, AnnouncementTypeCreateDto input)
        {
            var entity = new AnnouncementType(
                GuidGenerator.Create(),
                academicMediumId,
                input.AnnouncementType,
                input.Description,
                input.Color
            );

            await _repository.InsertAsync(entity);
            return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(entity);
        }

        public async Task<AnnouncementTypeDto> UpdateAsync(Guid id, AnnouncementTypeUpdateDto input)
        {
            var entity = await _repository.GetAsync(id);

            entity.Name = input.AnnouncementType;
            entity.Description = input.Description;
            entity.Color = input.Color;

            await _repository.UpdateAsync(entity);
            return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(entity);
        }

        public async Task<MessageDto> DeleteAsync(Guid id)
        {
            var entity = await _repository.GetAsync(id);
           
            await _repository.DeleteAsync(entity);
            
            return new MessageDto { Message = "Announcement type deleted successfully" };
        }
    }
}
