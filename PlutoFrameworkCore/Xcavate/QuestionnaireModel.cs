using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlutoFramework.Model.Xcavate
{
    public record QuestionnaireCondition
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("question_text")]
        public required string QuestionText { get; init; }

        [JsonPropertyName("options")]
        public List<string>? Options { get; init; }
    }

    public record QuestionnaireQuestion
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("question_text")]
        public required string QuestionText { get; init; }

        [JsonPropertyName("options")]
        public List<string>? Options { get; init; }

        [JsonPropertyName("conditions")]
        public QuestionnaireCondition? Conditions { get; init; }
    }

    public record QuestionnaireDeclaration
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("question_text")]
        public required string QuestionText { get; init; }

        [JsonPropertyName("options")]
        public List<string>? Options { get; init; }
    }

    public record QuestionnaireSection
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("introText")]
        public string? IntroText { get; init; }

        [JsonPropertyName("questions")]
        public required List<QuestionnaireQuestion> Questions { get; init; }

        [JsonPropertyName("declarations")]
        public required List<QuestionnaireDeclaration> Declarations { get; init; }
    }

    public record QuestionnaireInfo
    {
        public required List<QuestionnaireSection> Sections { get; set; }
        public required Func<Task> Navigation { get; set; }
    }

    public record QuestionnaireApiResponse<ResultT>
    {
        [JsonPropertyName("error")]
        public required bool Error { get; set; }

        [JsonPropertyName("data")]
        public required string Data { get; set; }

        [JsonPropertyName("code")]
        public required int Code { get; set; }

        [JsonPropertyName("result")]
        public required ResultT Result { get; set; }
    }

    public record QuestionnaireAnswers
    {
        [JsonPropertyName("userId")]
        public required string UserId { get; set; }

        [JsonPropertyName("account_address")]
        public required string AccountAddress { get; set; }

        [JsonPropertyName("responses")]
        public required Dictionary<string, Dictionary<string, object?>> Responses { get; set; }
    }


    public record QuestionnaireAcceptTerms
    {
        [JsonPropertyName("hasAgreedToTerms")]
        public required bool HasAgreedToTerms { get; set; }
    }

    public record QuestionnaireSectionEvaluation
    {
        [JsonPropertyName("questionnaire_id")]
        public required string QuestionnaireId { get; init; }

        [JsonPropertyName("passed")]
        public bool Passed { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }

    public record QuestionnaireEvaluation
    {
        [JsonPropertyName("passed")]
        public bool Passed { get; init; }

        [JsonPropertyName("sections")]
        public required List<QuestionnaireSectionEvaluation> Sections { get; init; }
    }

    public record QuestionnaireSubmissionRecord
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("account_address")]
        public required string AccountAddress { get; init; }

        [JsonPropertyName("userId")]
        public string? UserId { get; init; }

        [JsonPropertyName("responses")]
        public required Dictionary<string, Dictionary<string, object?>> Responses { get; init; }

        [JsonPropertyName("assessment")]
        public QuestionnaireEvaluation? Assessment { get; init; }

        [JsonPropertyName("submittedAt")]
        public required string SubmittedAt { get; init; }
    }

    public class QuestionnaireModel
    {
        private const string API_URL = "https://app.realxmarket.io";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<List<QuestionnaireSection>> GetXcavateQuestionsAsync()
        {
            var client = new HttpClient();

            var response = await client.GetAsync($"{API_URL}/api/v2/questionnaire/questions");

            response.EnsureSuccessStatusCode();

            var apiResponseJson = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<QuestionnaireApiResponse<List<QuestionnaireSection>>>(apiResponseJson, JsonOptions);

            return apiResponse?.Result ?? throw new Exception();
        }

        public static async Task<QuestionnaireSubmissionRecord> PostAnswersAsync(QuestionnaireAnswers answers)
        {
            var client = new HttpClient();

            var response = await client.PostAsJsonAsync($"{API_URL}/api/v2/questionnaire/responses", answers);

            response.EnsureSuccessStatusCode();

            var apiResponseJson = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<QuestionnaireApiResponse<QuestionnaireSubmissionRecord>>(apiResponseJson, JsonOptions);

            return apiResponse?.Result ?? throw new Exception();
        }

        public static async Task<QuestionnaireEvaluation> EvaluateAnswersAsync(Dictionary<string, Dictionary<string, object?>> responses)
        {
            var client = new HttpClient();

            var response = await client.PostAsJsonAsync($"{API_URL}/api/v2/questionnaire/evaluate", responses);
            
            response.EnsureSuccessStatusCode();

            var apiResponseJson = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<QuestionnaireApiResponse<QuestionnaireEvaluation>>(apiResponseJson, JsonOptions);

            return apiResponse?.Result ?? throw new Exception();
        }

        public static async Task<string> AcceptTermsAsync(string address)
        {
            var accept = new QuestionnaireAcceptTerms
            {
                HasAgreedToTerms = true
            };

            var client = new HttpClient();

            var response = await client.PutAsJsonAsync($"{API_URL}/api/questionnaire/terms/{address}", accept);

            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadAsStringAsync();

            return apiResponse;
        }
    }
}
