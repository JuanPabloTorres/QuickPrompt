using Moq;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Interfaces;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

/// <summary>
/// Unit tests for DeletePromptUseCase.
/// Tests deletion logic and error handling.
/// </summary>
public class DeletePromptUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockRepository;
    private readonly DeletePromptUseCase _useCase;

    public DeletePromptUseCaseTests()
    {
        _mockRepository = new Mock<IPromptRepository>();
        _useCase = new DeletePromptUseCase(_mockRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DeletePromptUseCase(null!));
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
        }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentPrompt_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        
        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync((PromptTemplate?)null);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Prompt not found", result.Error);
        }

    #endregion

    #region Success Tests

    [Fact]
    public async Task ExecuteAsync_WithValidId_DeletesPrompt()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var prompt = new PromptTemplate { Id = promptId };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(prompt);

        _mockRepository
            .Setup(x => x.DeleteAsync(promptId))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.True(result.IsSuccess);
        _mockRepository.Verify(x => x.DeleteAsync(promptId), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteFails_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var prompt = new PromptTemplate { Id = promptId };

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(prompt);

        _mockRepository
            .Setup(x => x.DeleteAsync(promptId))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to delete prompt", result.Error);
        }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(promptId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to delete prompt", result.Error);
        }

    #endregion
}
