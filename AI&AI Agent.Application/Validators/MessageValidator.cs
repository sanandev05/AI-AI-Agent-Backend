using AI_AI_Agent.Contract.DTOs;
using FluentValidation;

namespace AI_AI_Agent.Application.Validators
{
    public class MessageValidator : AbstractValidator<MessageDto>
    {
        public MessageValidator() 
        { 
        }
    }
}
