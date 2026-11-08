using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUsers
{
    public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
    {
        public RegisterUserValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .MaximumLength(250)
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(128)
                .Matches("[A-Z]").WithMessage("Password MUST contain an upercase letter.")
                .Matches("[a-z]").WithMessage("Password MUST contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password MUST contain a digit.");

            RuleFor(x => x.DisplayName)
                .NotEmpty()
                .MaximumLength(50);
        }
    }
}
