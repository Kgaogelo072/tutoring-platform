namespace EngineeredTutors.Api.Features.Attempts;

/// <summary>Public, token-authorised routes for taking a diagnostic (section 6).</summary>
public static class AttemptEndpoints
{
    public static void MapAttempts(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/attempts/{token}");

        g.MapGet("", (string token, AttemptService svc, CancellationToken ct) =>
            Run(() => svc.GetStateAsync(token, ct)));

        g.MapPost("/start", (string token, AttemptService svc, CancellationToken ct) =>
            Run(() => svc.StartAsync(token, ct)));

        g.MapGet("/questions", (string token, AttemptService svc, CancellationToken ct) =>
            Run(() => svc.GetQuestionsAsync(token, ct)));

        g.MapPut("/responses/{questionId:guid}",
            (string token, Guid questionId, SaveResponseRequest body, AttemptService svc, CancellationToken ct) =>
                Run(() => svc.SaveResponseAsync(token, questionId, body, ct)));

        g.MapPost("/submit", (string token, AttemptService svc, CancellationToken ct) =>
            Run(() => svc.SubmitAsync(token, ct)));
    }

    private static async Task<IResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (AttemptException ex)
        {
            return Results.Problem(statusCode: ex.Status, detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
        }
    }
}
