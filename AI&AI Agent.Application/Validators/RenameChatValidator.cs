using AI_AI_Agent.Contract.DTOs;
using FluentValidation;

namespace AI_AI_Agent.Application.Validators
{
    public class RenameChatValidator : AbstractValidator<RenameChatDto>
    {
        public RenameChatValidator()
        {
            RuleFor(x => x.NewTitle)
                .NotEmpty().WithMessage("New title cannot be empty.")
                .MaximumLength(100).WithMessage("Title cannot be longer than 100 characters.");
        }
    }
}
