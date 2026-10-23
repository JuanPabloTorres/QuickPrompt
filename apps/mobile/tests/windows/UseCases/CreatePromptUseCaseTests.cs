using Moq;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;
using QuickPrompt.Tools;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

public class CreatePromptUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockRepository;
    private readonly CreatePromptUseCase _useCase;

    public CreatePromptUseCaseTests()
    {
        _mockRepository = new Mock<IPromptRepository>();
        _useCase = new CreatePromptUseCase(_mockRepository.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreatePromptUseCase(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ReturnsFailure()
    {
        // Act
        var result = await _useCase.ExecuteAsync(null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Request cannot be null", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ReturnsFailure(string title)
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = title,
            Template = "<variable>",
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Title is required", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTemplate_ReturnsFailure(string template)
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test",
            Template = template,
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Template is required", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplateWithoutAngleBraces_ReturnsFailure()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test",
            Template = "No variables",
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("must contain at least one variable", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_CreatesPrompt()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test",
            Template = "<var>",
            Category = "Marketing"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Test", result.Value.Title);
        Assert.Equal("<var>", result.Value.Template);
        Assert.Equal(PromptCategory.Marketing, result.Value.Category);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsVariablesCorrectly()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test",
            Template = "<type> <product> <audience>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.AddAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Variables.Count);
    }
}
