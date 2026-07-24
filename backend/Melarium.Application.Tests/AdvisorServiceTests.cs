using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Advisor;
using Melarium.Application.Features.Advisor.DTOs;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Locks advisor ownership, the 60-message cap, and the transactional AI guard
/// (nothing persisted on AI failure). Groq is mocked via <see cref="IAdvisorAiClient"/>.</summary>
public class AdvisorServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IAdvisorAiClient _ai = Substitute.For<IAdvisorAiClient>();
    private readonly IWeatherService _weather = Substitute.For<IWeatherService>();
    private readonly ITranscriptionService _transcription = Substitute.For<ITranscriptionService>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();

    private AdvisorService Service(int userId = 1) =>
        new(_uow, _access, new TestCurrentUser { UserId = userId, Role = UserRole.Beekeeper }, _ai, _weather, _transcription, _plan);

    [Fact]
    public async Task GetConversation_NotOwner_ThrowsNotFound()
    {
        _uow.AdvisorConversations.GetWithMessagesAsync(5)
            .Returns(new AdvisorConversation { Id = 5, UserId = 2 }); // owned by someone else

        await Assert.ThrowsAsync<NotFoundException>(() => Service(userId: 1).GetConversationAsync(5));
    }

    [Fact]
    public async Task SendMessage_AtMessageCap_ThrowsBusinessRule_AndDoesNotCallAi()
    {
        var convo = new AdvisorConversation { Id = 5, UserId = 1 };
        for (int i = 0; i < 60; i++)
            convo.Messages.Add(new AdvisorMessage { Role = AdvisorRole.User, Content = "x" });
        _uow.AdvisorConversations.GetWithMessagesAsync(5).Returns(convo);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().SendMessageAsync(5, new SendMessageDto { Message = "još jedno" }));

        await _ai.DidNotReceive().SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AiFailure_ThrowsBusinessRule_AndPersistsNothing()
    {
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("groq down"));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().CreateConversationAsync(new CreateConversationDto { Message = "pitanje" }));

        await _uow.AdvisorConversations.DidNotReceive().AddAsync(Arg.Any<AdvisorConversation>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_Success_PersistsUserThenAssistantMessage()
    {
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("Odgovor savjetnika.");

        AdvisorConversation? added = null;
        _uow.AdvisorConversations.AddAsync(Arg.Do<AdvisorConversation>(c => added = c))
            .Returns(ci => ci.Arg<AdvisorConversation>());
        _uow.AdvisorConversations.GetWithMessagesAsync(Arg.Any<int>()).Returns(_ => added);

        var result = await Service().CreateConversationAsync(new CreateConversationDto { Message = "Kako prihraniti slabo društvo?" });

        Assert.NotNull(added);
        Assert.Equal(2, added!.Messages.Count);
        Assert.Equal(AdvisorRole.User, added.Messages[0].Role);
        Assert.Equal(AdvisorRole.Assistant, added.Messages[1].Role);
        Assert.Equal("Odgovor savjetnika.", added.Messages[1].Content);
        Assert.Equal(2, result.Messages.Count);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_WithBeehive_EnforcesAccess_BeforeCallingAi()
    {
        _access.EnsureCanAccessBeehiveAsync(7)
            .Returns(Task.FromException(new ForbiddenAccessException()));

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => Service().CreateConversationAsync(new CreateConversationDto { BeehiveId = 7, Message = "x" }));

        await _ai.DidNotReceive().SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_EmptyResult_ThrowsValidation()
    {
        _transcription.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string>()).Returns("   ");

        await Assert.ThrowsAsync<ValidationException>(() => Service().TranscribeAsync(Stream.Null, "note.webm"));
    }
}
