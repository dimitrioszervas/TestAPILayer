using AutoMapper;

using TestAPILayer.Dtos.Users;
using TestAPILayer.Models;

namespace TestAPILayer.Configurations
{
    public class MapperConfig : Profile
    {
        public MapperConfig()
        {
            // Add your mappings here
            CreateMap<ApiUser, ApiUserDto>().ReverseMap();
        }
    }
}
