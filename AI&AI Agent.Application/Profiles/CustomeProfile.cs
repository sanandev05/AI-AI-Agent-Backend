using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Domain.Entities;
using AutoMapper;

namespace AI_AI_Agent.Application.Profiles
{
    public class CustomeProfile : Profile
    {
        public CustomeProfile()
        {
            CreateMap<Chat, ChatDto>().ReverseMap();
            CreateMap<Message, MessageDto>()
    .ForMember(dest => dest.ImageUrls, opt => opt.MapFrom(src =>
        src.ImageUrls != null ? src.ImageUrls.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>()))
    .ReverseMap()
    .ForMember(dest => dest.ImageUrls, opt => opt.MapFrom(src =>
        src.ImageUrls != null ? string.Join(";", src.ImageUrls) : null));
        }
    }
}
