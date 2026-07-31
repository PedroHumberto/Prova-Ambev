using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using Ambev.DeveloperEvaluation.WebApi;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.CreateUser;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class AutoMapperConfigurationTests
{
    private readonly IMapper _mapper;

    public AutoMapperConfigurationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(_ => { }, typeof(Program).Assembly, typeof(ApplicationLayer).Assembly);
        _mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    [Fact]
    public void Configuration_ProgramAssemblies_IsValid()
    {
        _mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }

    [Fact]
    public void CreateUserMappings_AllLayers_PreserveContractAndMapUsernameToName()
    {
        var request = new CreateUserRequest
        {
            Username = "test.user",
            Password = "Valid1!Password",
            Email = "test.user@example.com",
            Phone = "+5511999999999",
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        var command = _mapper.Map<CreateUserCommand>(request);
        var user = _mapper.Map<User>(command);
        user.Id = Guid.NewGuid();
        var result = _mapper.Map<CreateUserResult>(user);
        var response = _mapper.Map<CreateUserResponse>(result);

        command.Should().BeEquivalentTo(request);
        user.Should().BeEquivalentTo(command, options => options.ExcludingMissingMembers());
        result.Name.Should().Be(request.Username);
        response.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void GetUserMappings_AllLayers_PreserveContractAndMapUsernameToName()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Username = "test.user",
            Email = "test.user@example.com",
            Phone = "+5511999999999",
            Role = UserRole.Customer,
            Status = UserStatus.Active
        };

        var command = _mapper.Map<GetUserCommand>(id);
        var result = _mapper.Map<GetUserResult>(user);
        var response = _mapper.Map<GetUserResponse>(result);

        command.Id.Should().Be(id);
        result.Name.Should().Be(user.Username);
        response.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void AuthenticateUserMappings_AllLayers_PreserveCredentialsAndPublicResult()
    {
        var request = new AuthenticateUserRequest
        {
            Email = "test.user@example.com",
            Password = "Valid1!Password"
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "test.user",
            Email = request.Email,
            Phone = "+5511999999999",
            Role = UserRole.Admin
        };

        var command = _mapper.Map<AuthenticateUserCommand>(request);
        var result = _mapper.Map<AuthenticateUserResult>(user);
        result.Token = "jwt-token";
        var response = _mapper.Map<AuthenticateUserResponse>(result);

        command.Should().BeEquivalentTo(request);
        result.Name.Should().Be(user.Username);
        result.Role.Should().Be(nameof(UserRole.Admin));
        response.Should().BeEquivalentTo(result, options => options.ExcludingMissingMembers());
        response.Token.Should().Be(result.Token);
    }

    [Fact]
    public void SaleMappings_ApplicationResults_PreserveAggregateAndNestedItemValues()
    {
        var sale = ApplicationSaleTestData.CreateSale();

        var createResult = _mapper.Map<CreateSaleResult>(sale);
        var getResult = _mapper.Map<GetSaleByIdResult>(sale);
        var updateResult = _mapper.Map<UpdateSaleResult>(sale);
        var listResult = _mapper.Map<ListSalesItemResult>(sale);

        createResult.Should().BeEquivalentTo(getResult);
        updateResult.Should().BeEquivalentTo(getResult);
        listResult.Should().BeEquivalentTo(getResult, options => options.ExcludingMissingMembers());
        typeof(ListSalesItemResult).GetProperty(nameof(SaleResult.Items)).Should().BeNull();
        getResult.Id.Should().Be(sale.Id);
        getResult.SaleNumber.Should().Be(sale.SaleNumber);
        getResult.Subtotal.Should().Be(sale.Subtotal);
        getResult.DiscountAmount.Should().Be(sale.DiscountAmount);
        getResult.TotalAmount.Should().Be(sale.TotalAmount);
        getResult.Items.Should().HaveCount(sale.Items.Count);
        getResult.Items.Should().BeEquivalentTo(sale.Items, options => options.ExcludingMissingMembers());
    }
}
