using Moq;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

/// <summary>
/// Unit tests for CreatePromptUseCase.
/// Tests validation, business logic, and error handling.
/// </summary>
public class CreatePromptUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockRepository;
    private readonly CreatePromptUseCase _useCase;

    public CreatePromptUseCaseTests()
    {
        _mockRepository = new Mock<IPromptRepository>();
        _useCase = new CreatePromptUseCase(_mockRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreatePromptUseCase(null!));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ReturnsFailure()
    {
        // Act
        var result = await _useCase.ExecuteAsync(null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Request cannot be null", result.Error);
        Assert.Equal("InvalidRequest", result.ErrorCode);
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
            Description = "Test description",
            Template = "<variable>",
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Title is required", result.Error);
        Assert.Equal("ValidationError", result.ErrorCode);
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
            Title = "Test Prompt",
            Description = "Test description",
            Template = template,
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Template is required", result.Error);
        Assert.Equal("ValidationError", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplateWithoutAngleBraces_ReturnsFailure()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "This is a template without variables",
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("must contain at least one variable", result.Error);
        Assert.Equal("ValidationError", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCategory_UsesGeneralAsDefault()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "<variable>",
            Category = "InvalidCategory"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PromptCategory.General, result.Value.Category);
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_CreatesPrompt()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "Write an ad for <product>",
            Category = "Marketing"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Prompt", result.Value.Title);
        Assert.Equal("Test description", result.Value.Description);
        Assert.Equal("Write an ad for <product>", result.Value.Template);
        Assert.Equal(PromptCategory.Marketing, result.Value.Category);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ExtractsVariables()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "Write a <type> for <product> targeting <audience>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Variables);
        Assert.Equal(3, result.Value.Variables.Count);
        Assert.Contains("type", result.Value.Variables.Keys);
        Assert.Contains("product", result.Value.Variables.Keys);
        Assert.Contains("audience", result.Value.Variables.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_CallsRepositorySaveOnce()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        await _useCase.ExecuteAsync(request);

        // Assert
        _mockRepository.Verify(
            x => x.SavePromptAsync(It.IsAny<PromptTemplate>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_GeneratesGuidId()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to create prompt", result.Error);
        Assert.Contains("Database error", result.Error);
        Assert.Equal("DatabaseError", result.ErrorCode);
    }

    #endregion

    #region Category Tests

    [Theory]
    [InlineData("General", PromptCategory.General)]
    [InlineData("Marketing", PromptCategory.Marketing)]
    [InlineData("Programming", PromptCategory.Programming)]
    [InlineData("Writing", PromptCategory.Writing)]
    public async Task ExecuteAsync_WithValidCategory_SetsCorrectCategory(string categoryString, PromptCategory expectedCategory)
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "<variable>",
            Category = categoryString
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCategory, result.Value.Category);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteAsync_WithVeryLongTitle_Succeeds()
    {
        // Arrange
        var longTitle = new string('A', 500);
        var request = new CreatePromptRequest
        {
            Title = longTitle,
            Description = "Test description",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(longTitle, result.Value.Title);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleVariablesOfSameName_ExtractsOnce()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "Test description",
            Template = "Write <type> for <product> and another <type> for <product>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Variables.Count); // type and product
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDescription_Succeeds()
    {
        // Arrange
        var request = new CreatePromptRequest
        {
            Title = "Test Prompt",
            Description = "",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.SavePromptAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(1);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Value.Description);
    }

    #endregion
}
