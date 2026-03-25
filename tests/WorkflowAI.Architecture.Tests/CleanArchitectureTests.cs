using NetArchTest.Rules;

namespace WorkflowAI.Architecture.Tests;

public class CleanArchitectureTests
{
    private const string DomainNamespace = "WorkflowAI.Domain";
    private const string ApplicationNamespace = "WorkflowAI.Application";
    private const string InfrastructureNamespace = "WorkflowAI.Infrastructure";
    private const string FunctionsNamespace = "WorkflowAI.Functions";

    [Fact]
    public void Domain_Should_Not_DependOn_Application()
    {
        var result = Types.InAssembly(typeof(Domain.Common.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer should not depend on Application layer.");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Domain.Common.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer should not depend on Infrastructure layer.");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_Functions()
    {
        var result = Types.InAssembly(typeof(Domain.Common.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FunctionsNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer should not depend on Functions layer.");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application layer should not depend on Infrastructure layer.");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Functions()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FunctionsNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application layer should not depend on Functions layer.");
    }

    [Fact]
    public void Infrastructure_Should_Not_DependOn_Functions()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FunctionsNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Infrastructure layer should not depend on Functions layer.");
    }
}
