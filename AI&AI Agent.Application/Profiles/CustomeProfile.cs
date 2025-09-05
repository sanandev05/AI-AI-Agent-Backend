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
            CreateMap<Message, MessageDto>().ReverseMap();
        }
    }
}
