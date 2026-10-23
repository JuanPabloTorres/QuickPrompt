using Moq;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

/// <summary>
/// Unit tests for UpdatePromptUseCase.
/// Tests update validation, business logic, and error handling.
/// </summary>
public class UpdatePromptUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockRepository;
    private readonly UpdatePromptUseCase _useCase;

    public UpdatePromptUseCaseTests()
    {
        _mockRepository = new Mock<IPromptRepository>();
        _useCase = new UpdatePromptUseCase(_mockRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdatePromptUseCase(null!));
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
        Assert.Equal("Request cannot be null", result.Error);
        }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPromptId_ReturnsFailure()
    {
        // Arrange
        var request = new UpdatePromptRequest
        {
            PromptId = Guid.Empty,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid prompt ID", result.Error);
        }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ReturnsFailure(string title)
    {
        // Arrange
        var request = new UpdatePromptRequest
        {
            PromptId = Guid.NewGuid(),
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
        var request = new UpdatePromptRequest
        {
            PromptId = Guid.NewGuid(),
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
    public async Task ExecuteAsync_WithNonExistentPrompt_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync((PromptTemplate?)null);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Prompt not found", result.Error);
        }

    [Fact]
    public async Task ExecuteAsync_WithTemplateWithoutAngleBraces_ReturnsFailure()
    {
        // Arrange
        var request = new UpdatePromptRequest
        {
            PromptId = Guid.NewGuid(),
            Title = "Test",
            Template = "No variables here",
            Category = "General"
        };

        var existingPrompt = new PromptTemplate { Id = request.PromptId };
        _mockRepository
            .Setup(x => x.GetByIdAsync(request.PromptId))
            .ReturnsAsync(existingPrompt);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("must contain at least one variable", result.Error);
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_UpdatesPrompt()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate
        {
            Id = promptId,
            Title = "Old Title",
            Template = "<old>",
            Category = PromptCategory.General
        };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "New Title",
            Description = "New Description",
            Template = "<new> and <variables>",
            Category = "Marketing"
        };

        var updatedPrompt = new PromptTemplate
        {
            Id = promptId,
            Title = request.Title,
            Description = request.Description,
            Template = request.Template,
            Category = PromptCategory.Marketing
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New Title", result.Value.Title);
        Assert.Equal("New Description", result.Value.Description);
        Assert.Equal("<new> and <variables>", result.Value.Template);
        Assert.Equal(PromptCategory.Marketing, result.Value.Category);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsVariablesFromNewTemplate()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "Write <type> for <product> targeting <audience>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<PromptTemplate>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRepositoryUpdateOnce()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        await _useCase.ExecuteAsync(request);

        // Assert
        _mockRepository.Verify(
            x => x.UpdateAsync(It.IsAny<PromptTemplate>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesPromptId()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(promptId, result.Value.Id);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to update prompt", result.Error);
        }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateReturnsFalse_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId, Title = "Test", Template = "<var>" };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "General"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>()))
            .ReturnsAsync(false); // Repository indicates update failed

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to update prompt", result.Error);
    }

    #endregion

    #region Category Tests

    [Theory]
    [InlineData("General", PromptCategory.General)]
    [InlineData("Marketing", PromptCategory.Marketing)]
    [InlineData("Writing", PromptCategory.Writing)]
    [InlineData("Programming", PromptCategory.Programming)]
    public async Task ExecuteAsync_WithValidCategory_SetsCorrectCategory(
        string categoryString,
        PromptCategory expectedCategory)
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = categoryString
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCategory, result.Value.Category);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCategory_UsesGeneralAsDefault()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var existingPrompt = new PromptTemplate { Id = promptId };

        var request = new UpdatePromptRequest
        {
            PromptId = promptId,
            Title = "Test",
            Template = "<variable>",
            Category = "InvalidCategory"
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(existingPrompt);

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<PromptTemplate>())).ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PromptCategory.General, result.Value.Category);
    }

    #endregion
}
