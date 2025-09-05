using AI_AI_Agent.Contract.DTOs;
using FluentValidation;

namespace AI_AI_Agent.Application.Validators
{
    public class SignInValidator : AbstractValidator<SignInDto>
    {
        public SignInValidator() 
        {
            RuleFor(email=>email.Email).NotNull().NotEmpty();
            RuleFor(password => password.Password).NotNull().NotEmpty();

        }
    }
}
