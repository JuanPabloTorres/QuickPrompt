using Moq;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Interfaces;
using Xunit;

namespace QuickPrompt.Tests.UseCases;

/// <summary>
/// Unit tests for ExecutePromptUseCase.
/// Tests prompt execution, variable filling, and caching.
/// </summary>
public class ExecutePromptUseCaseTests
{
    private readonly Mock<IPromptRepository> _mockPromptRepository;
    private readonly Mock<IFinalPromptRepository> _mockFinalPromptRepository;
    private readonly Mock<IPromptCacheService> _mockCacheService;
    private readonly ExecutePromptUseCase _useCase;

    public ExecutePromptUseCaseTests()
    {
        _mockPromptRepository = new Mock<IPromptRepository>();
        _mockFinalPromptRepository = new Mock<IFinalPromptRepository>();
        _mockCacheService = new Mock<IPromptCacheService>();
        
        _useCase = new ExecutePromptUseCase(
            _mockPromptRepository.Object,
            _mockFinalPromptRepository.Object,
            _mockCacheService.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullPromptRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutePromptUseCase(
                null!,
                _mockFinalPromptRepository.Object,
                _mockCacheService.Object));
    }

    [Fact]
    public void Constructor_WithNullFinalPromptRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutePromptUseCase(
                _mockPromptRepository.Object,
                null!,
                _mockCacheService.Object));
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutePromptUseCase(
                _mockPromptRepository.Object,
                _mockFinalPromptRepository.Object,
                null!));
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
        var request = new ExecutePromptRequest
        {
            PromptId = Guid.Empty,
            Variables = new Dictionary<string, string>()
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid prompt ID", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentPrompt_ReturnsFailure()
    {
        // Arrange
        var request = new ExecutePromptRequest
        {
            PromptId = Guid.NewGuid(),
            Variables = new Dictionary<string, string>()
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(request.PromptId))
            .ReturnsAsync((PromptTemplate?)null);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Prompt not found", result.Error);
    }

    #endregion

    #region Variable Filling Tests

    [Fact]
    public async Task ExecuteAsync_FillsSingleVariable_Correctly()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Write an ad for <product>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "product", "QuickPrompt" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Write an ad for QuickPrompt", result.Value.CompletedText);
    }

    [Fact]
    public async Task ExecuteAsync_FillsMultipleVariables_Correctly()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Write a <type> for <product> targeting <audience>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "type", "blog post" },
                { "product", "QuickPrompt" },
                { "audience", "developers" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Write a blog post for QuickPrompt targeting developers", result.Value.CompletedText);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatedVariables_FillsAllOccurrences()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Use <product> to improve your workflow. <product> is amazing!"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "product", "QuickPrompt" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Use QuickPrompt to improve your workflow. QuickPrompt is amazing!", result.Value.CompletedText);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task ExecuteAsync_CachesVariableValues()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Write for <product>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "product", "QuickPrompt" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _useCase.ExecuteAsync(request);

        // Assert
        _mockCacheService.Verify(
            x => x.AddAsync("product", "QuickPrompt"),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CachesAllVariableValues()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "<type> for <product>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "type", "blog" },
                { "product", "QuickPrompt" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _useCase.ExecuteAsync(request);

        // Assert
        _mockCacheService.Verify(
            x => x.AddAsync("type", "blog"),
            Times.Once);
        _mockCacheService.Verify(
            x => x.AddAsync("product", "QuickPrompt"),
            Times.Once);
    }

    #endregion

    #region FinalPrompt Saving Tests

    [Fact]
    public async Task ExecuteAsync_SavesFinalPrompt()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "<variable>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string> { { "variable", "value" } }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _useCase.ExecuteAsync(request);

        // Assert
        _mockFinalPromptRepository.Verify(
            x => x.AddAsync(It.Is<FinalPrompt>(fp =>
                fp.SourcePromptId == promptId &&
                fp.CompletedText == "value" &&
                fp.IsFavorite == false)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSavedFinalPrompt()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "<variable>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string> { { "variable", "value" } }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.FinalPrompt);
        Assert.Equal(promptId, result.Value.FinalPrompt.SourcePromptId);
        Assert.Equal("value", result.Value.FinalPrompt.CompletedText);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        var request = new ExecutePromptRequest
        {
            PromptId = Guid.NewGuid(),
            Variables = new Dictionary<string, string>()
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to execute prompt", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSaveFails_ReturnsFailure()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "<variable>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string> { { "variable", "value" } }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ThrowsAsync(new Exception("Save failed"));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteAsync_WithEmptyVariableDictionary_FillsNothing()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Static template with <variable>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>()
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Static template with <variable>", result.Value.CompletedText);
    }

    [Fact]
    public async Task ExecuteAsync_WithSpecialCharactersInVariables_HandlesCorrectly()
    {
        // Arrange
        var promptId = Guid.NewGuid();
        var template = new PromptTemplate
        {
            Id = promptId,
            Template = "Product: <product>"
        };

        var request = new ExecutePromptRequest
        {
            PromptId = promptId,
            Variables = new Dictionary<string, string>
            {
                { "product", "Quick<Prompt>" }
            }
        };

        _mockPromptRepository
            .Setup(x => x.GetByIdAsync(promptId))
            .ReturnsAsync(template);

        _mockFinalPromptRepository
            .Setup(x => x.AddAsync(It.IsAny<FinalPrompt>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Product: Quick<Prompt>", result.Value.CompletedText);
    }

    #endregion
}
