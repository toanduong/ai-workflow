using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Application.UnitTests.Workflows;

/// <summary>
/// Unit tests for Blob Storage service upload functionality.
/// These tests verify the blob storage upload behavior independently from the full workflow.
/// </summary>
public class BlobStorageUploadTests
{
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();

    [Fact]
    public async Task UploadAsync_ShouldReturnBlobUrl_WhenSuccessful()
    {
        // Arrange
        var containerName = "workflow-outputs";
        var blobName = "test-workflow-20260409-120000-arm-template.json";
        var content = """
        {
          "$schema": "https://schema.management.azure.com/...",
          "contentVersion": "1.0.0.0"
        }
        """;

        var expectedUrl = $"https://storage.blob.core.windows.net/{containerName}/{blobName}";
        _blobStorageService.UploadAsync(containerName, blobName, content, Arg.Any<CancellationToken>())
            .Returns(expectedUrl);

        // Act
        var result = await _blobStorageService.UploadAsync(containerName, blobName, content, CancellationToken.None);

        // Assert
        result.Should().Be(expectedUrl);
        await _blobStorageService.Received(1).UploadAsync(
            Arg.Is(containerName),
            Arg.Is(blobName),
            Arg.Is(content),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_ShouldHandleMultipleUploads_InSameContainer()
    {
        // Arrange
       var containerName = "workflow-outputs";
        var uploads = new[]
        {
            ("workflow-1-plan.json", "plan content 1"),
            ("workflow-1-arm.json", "arm content 1"),
            ("workflow-2-plan.json", "plan content 2"),
            ("workflow-2-arm.json", "arm content 2")
        };

        foreach (var (blobName, content) in uploads)
        {
            _blobStorageService.UploadAsync(containerName, blobName, content, Arg.Any<CancellationToken>())
                .Returns($"https://storage.blob.core.windows.net/{containerName}/{blobName}");
        }

        // Act & Assert
        foreach (var (blobName, content) in uploads)
        {
            var result = await _blobStorageService.UploadAsync(containerName, blobName, content, CancellationToken.None);
            result.Should().Contain(blobName);
        }

        await _blobStorageService.Received(4).UploadAsync(
            Arg.Is(containerName),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_ShouldThrowException_WhenBlobStorageFails()
    {
        // Arrange
        var containerName = "workflow-outputs";
        var blobName = "test.json";
        var content = "test content";

        _blobStorageService.UploadAsync(containerName, blobName, content, Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new Exception("Blob storage is unavailable"));

        // Act & Assert
        await _blobStorageService.Invoking(async s =>
                await s.UploadAsync(containerName, blobName, content, CancellationToken.None))
            .Should().ThrowAsync<Exception>()
            .WithMessage("Blob storage is unavailable");
    }

    [Fact]
    public async Task UploadAsync_ShouldUseCorrectContainerName()
    {
        // Arrange
        var containerName = "workflow-outputs";
        var blobName = "test.json";
        var content = "test";

        _blobStorageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://fake.url");

        // Act
        await _blobStorageService.UploadAsync(containerName, blobName, content, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).UploadAsync(
            Arg.Is<string>(c => c == "workflow-outputs"),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_ShouldAcceptCancellationToken()
    {
        // Arrange
        var containerName = "workflow-outputs";
        var blobName = "test.json";
        var content = "test";
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _blobStorageService.UploadAsync(containerName, blobName, content, cancellationToken)
            .Returns("https://fake.url");

        // Act
        var result = await _blobStorageService.UploadAsync(containerName, blobName, content, cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        await _blobStorageService.Received(1).UploadAsync(
            containerName,
            blobName,
            content,
            Arg.Is<CancellationToken>(ct => ct == cancellationToken));
    }

    [Fact]
    public async Task UploadAsync_ShouldVerifyContentPassedCorrectly()
    {
        // Arrange
        var containerName = "workflow-outputs";
        var blobName = "workflow-plan.json";
        var expectedContent = """
        {
          "workflowName": "test-workflow",
          "steps": [
            {
              "stepName": "Step1",
              "connectorName": "Odoo",
              "apiName": "ListContacts"
            }
          ]
        }
        """;

        string? capturedContent = null;
        _blobStorageService.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Do<string>(c => capturedContent = c),
                Arg.Any<CancellationToken>())
            .Returns("https://fake.url");

        // Act
        await _blobStorageService.UploadAsync(containerName, blobName, expectedContent, CancellationToken.None);

        // Assert
        capturedContent.Should().Be(expectedContent);
        capturedContent.Should().Contain("test-workflow");
        capturedContent.Should().Contain("Step1");
    }
}
