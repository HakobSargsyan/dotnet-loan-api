using LoanApi;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

public class FakeChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses;
    public List<List<ChatMessage>> Calls { get; } = [];

    public FakeChatClient(params ChatResponse[] responses) => _responses = new(responses);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToList());
        return Task.FromResult(_responses.Dequeue());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

public class ChatToolTests
{
    [Fact]
    public void MonthlyPayment_RejectsTooManyMonths()
    {
        var tools = new LoanTools(
            NullLogger<LoanTools>.Instance
        );

        var result = tools.MonthlyPayment(
            5000m,
            14m,
            2_000_000
        );

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task ToolCall_IsExecuted_AndResultSentBack()
    {
        var call = new FunctionCallContent(
            "call1",
            "MonthlyPayment",
            new Dictionary<string, object?>
            {
                ["amount"] = 5000m,
                ["annualRatePercent"] = 14m,
                ["months"] = 12
            }
        );

        var fake = new FakeChatClient(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, [call])
            ),
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    "It is 448.94 AMD."
                )
            )
        );

        var client = new ChatClientBuilder(fake)
            .UseFunctionInvocation()
            .Build();

        var tools = new LoanTools(
            NullLogger<LoanTools>.Instance
        );

        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(tools.MonthlyPayment)
            ]
        };

        var response = await client.GetResponseAsync(
            "5000 for 12 months?",
            options
        );

        Assert.Equal("It is 448.94 AMD.", response.Text);

        Assert.Equal(2, fake.Calls.Count);

        var functionResult = fake.Calls[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();

        Assert.Equal(
            "448.94 AMD",
            functionResult.Result?.ToString()
        );
    }
}