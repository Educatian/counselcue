using System.Net.Http.Headers;
using System.Net.Http.Json;

const string clientInstructions = """
당신은 한국의 32세 직장인 내담자 김지혜입니다. 초기면담에서 회사에 갈 때 시작되는 불안과 팀장과의 대화에서 심해지는 신체 긴장을 경험합니다.
상담자가 묻지 않은 핵심 정보를 한꺼번에 공개하지 말고, 한 번에 1~3문장으로 자연스럽게 답하세요.
공감적 반영과 개방형 질문에는 조금 더 구체적으로 말하고, 성급한 조언이나 감정 축소에는 짧고 경계적인 반응을 보이세요.
상담자나 전문가 역할로 전환하지 말고, 진단이나 치료 지시를 하지 마세요. 위기 상황을 연기하거나 자해 계획을 생성하지 마세요.
응답은 한국어로만 하세요.
""";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:8787");
builder.Services.AddHttpClient("openai", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ready", model = "gpt-realtime-2.1" }));

app.MapPost("/token", async (IHttpClientFactory clients, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    string? apiKey = configuration["OPENAI_API_KEY"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem("OPENAI_API_KEY가 서버 환경에 설정되지 않았습니다.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    object sessionRequest = new
    {
        expires_after = new { anchor = "created_at", seconds = 600 },
        session = new
        {
            type = "realtime",
            model = "gpt-realtime-2.1",
            output_modalities = new[] { "text" },
            instructions = clientInstructions
        }
    };

    using HttpRequestMessage request = new(HttpMethod.Post, "realtime/client_secrets")
    {
        Content = JsonContent.Create(sessionRequest)
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    string? safetyIdentifier = configuration["OPENAI_SAFETY_IDENTIFIER"];
    if (!string.IsNullOrWhiteSpace(safetyIdentifier))
    {
        request.Headers.Add("OpenAI-Safety-Identifier", safetyIdentifier);
    }

    HttpClient client = clients.CreateClient("openai");
    using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
    string body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem("OpenAI 임시 토큰 발급에 실패했습니다.", statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Content(body, "application/json");
});

app.Run();
