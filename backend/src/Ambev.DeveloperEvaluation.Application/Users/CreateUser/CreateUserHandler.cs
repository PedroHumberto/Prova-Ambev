using AutoMapper;
using MediatR;
using Ambev.DeveloperEvaluation.Application.Common.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Common.Security;

namespace Ambev.DeveloperEvaluation.Application.Users.CreateUser;

/// <summary>
/// Handler for processing CreateUserCommand requests
/// </summary>
public class CreateUserHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of CreateUserHandler
    /// </summary>
    /// <param name="userRepository">The user repository</param>
    /// <param name="mapper">The AutoMapper instance</param>
    public CreateUserHandler(
        IUserRepository userRepository,
        IMapper mapper,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Handles the CreateUserCommand request
    /// </summary>
    /// <param name="command">The CreateUser command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created user details</returns>
    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingUser != null)
            throw new UserAlreadyExistsException(command.Email);

        var user = _mapper.Map<User>(command);
        user.Password = _passwordHasher.HashPassword(command.Password);

        var createdUser = await _userRepository.CreateAsync(user, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var result = _mapper.Map<CreateUserResult>(createdUser);
        return result;
    }
}
