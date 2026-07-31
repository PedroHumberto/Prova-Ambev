using System.Reflection;
using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.IoC;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Architecture;

public sealed class LayerDependencyTests
{
    public static TheoryData<Assembly, string[]> ForbiddenDependencies => new()
    {
        {
            typeof(User).Assembly,
            ["Ambev.DeveloperEvaluation.Application", "Ambev.DeveloperEvaluation.ORM", "Ambev.DeveloperEvaluation.IoC", "Ambev.DeveloperEvaluation.WebApi"]
        },
        {
            typeof(ApplicationLayer).Assembly,
            ["Ambev.DeveloperEvaluation.ORM", "Ambev.DeveloperEvaluation.IoC", "Ambev.DeveloperEvaluation.WebApi"]
        },
        {
            typeof(DefaultContext).Assembly,
            ["Ambev.DeveloperEvaluation.Application", "Ambev.DeveloperEvaluation.IoC", "Ambev.DeveloperEvaluation.WebApi"]
        },
        {
            typeof(DependencyResolver).Assembly,
            ["Ambev.DeveloperEvaluation.WebApi"]
        }
    };

    [Theory]
    [MemberData(nameof(ForbiddenDependencies))]
    public void AssemblyReferences_ArchitecturalLayer_DoesNotReferenceForbiddenLayers(
        Assembly assembly,
        string[] forbiddenDependencies)
    {
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        referencedAssemblies.Should().NotIntersectWith(forbiddenDependencies);
    }

    [Fact]
    public void ContractsAssemblyReferences_IndependentContracts_HasNoProjectReferences()
    {
        var contractsAssembly = Assembly.Load("Ambev.DeveloperEvaluation.Contracts");
        var projectReferences = contractsAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("Ambev.DeveloperEvaluation.", StringComparison.Ordinal));

        projectReferences.Should().BeEmpty();
    }
}
