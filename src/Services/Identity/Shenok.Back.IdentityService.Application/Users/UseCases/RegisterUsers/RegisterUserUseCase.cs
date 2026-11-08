using Shenok.Back.IdentityService.Application.Common.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Domain.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUsers
{
    public sealed class RegisterUserUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IUnitOfWork _unitOfWork;

        public RegisterUserUseCase(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _unitOfWork = unitOfWork;
        }
        public async Task<RegisterUserResponse> ExecuteAsync(
            RegisterUserRequest request,
            CancellationToken cancellationToken)
        {
            var email = Email.Create(request.Email);

            // email mnust be unique:
            var existingUser = await _userRepository.ExistsByEmailAsync(email);
            if (existingUser == true)
                throw new ConflictException("User Already exists!");

            var hashedValue = _passwordHasher.Hash(request.Password);
            var passwordHash = PasswordHash.FromHashedValue(hashedValue);

            var profile = UserProfile.Create(request.DisplayName);

            var user = User.Register(email, passwordHash, profile, UserRole.Listener);

            await _userRepository.AddAsync(user,cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);

            return new RegisterUserResponse(user.Id.Value, user.Email.Value, user.Profile.DisplayName);
        }
    }
}
