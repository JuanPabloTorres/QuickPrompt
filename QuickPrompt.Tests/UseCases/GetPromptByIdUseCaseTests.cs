using Moq;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

/// <summary>
/// Unit tests for GetPromptByIdUseCase.
/// Tests retrieval logic and error handling.
/// </summary>
public class GetPromptByIdUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockRepository;
    private readonly GetPromptByIdUseCase _useCase;

    public GetPromptByIdUseCaseTests()
    {
        _mockRepository = new Mock<IPromptRepository>();
        _useCase = new GetPromptByIdUseCase(_mockRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GetPromptByIdUseCase(null!));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithEmptyPromptId_ReturnsFailure()
    {
        // Act
        var result = await _useCase.ExecuteAsync(Guid.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid prompt ID", result.Error);
        Assert.Equal("InvalidRequest", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentPrompt_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.GetPromptByIdAsync(promptId))
            .ReturnsAsync((PromptTemplate?)null);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Prompt not found", result.Error);
        Assert.Equal("NotFound", result.ErrorCode);
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task ExecuteAsync_WithValidId_ReturnsPrompt()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var prompt = new PromptTemplate
        {
            Id = promptId,
            Title = "Test Prompt",
            Description = "Test Description",
            Template = "<variable>",
            Category = PromptCategory.General
        };

        _mockRepository
            .Setup(x => x.GetPromptByIdAsync(promptId))
            .ReturnsAsync(prompt);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(promptId, result.Value.Id);
        Assert.Equal("Test Prompt", result.Value.Title);
        Assert.Equal("Test Description", result.Value.Description);
        Assert.Equal("<variable>", result.Value.Template);
        Assert.Equal(PromptCategory.General, result.Value.Category);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRepositoryOnce()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var prompt = new PromptTemplate { Id = promptId };

        _mockRepository
            .Setup(x => x.GetPromptByIdAsync(promptId))
            .ReturnsAsync(prompt);

        // Act
        await _useCase.ExecuteAsync(promptId);

        // Assert
        _mockRepository.Verify(
            x => x.GetPromptByIdAsync(promptId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithPromptWithVariables_ReturnsVariables()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var prompt = new PromptTemplate
        {
            Id = promptId,
            Template = "<var1> and <var2>",
            Variables = new Dictionary<string, string>
            {
                { "var1", "" },
                { "var2", "" }
            }
        };

        _mockRepository
            .Setup(x => x.GetPromptByIdAsync(promptId))
            .ReturnsAsync(prompt);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Variables);
        Assert.Equal(2, result.Value.Variables.Count);
        Assert.Contains("var1", result.Value.Variables.Keys);
        Assert.Contains("var2", result.Value.Variables.Keys);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.GetPromptByIdAsync(promptId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to retrieve prompt", result.Error);
        Assert.Equal("DatabaseError", result.ErrorCode);
    }

    #endregion
}
