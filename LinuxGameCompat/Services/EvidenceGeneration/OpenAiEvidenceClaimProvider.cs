using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI.Responses;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed class OpenAiEvidenceClaimProvider(ResponsesClient client, EvidenceGenerationOptions settings) : IEvidenceClaimProvider
{
	private static readonly BinaryData OutputSchema = BinaryData.FromString(EvidenceClaimPromptContract.OutputSchemaJson);

	public async Task<EvidenceClaimProviderResult> GenerateAsync(EvidenceClaimProviderRequest request, CancellationToken cancellationToken)
	{
		if (!string.Equals(request.Model, settings.Model, StringComparison.Ordinal) || request.MaximumOutputTokens is < 1 || request.MaximumOutputTokens > 800)
			throw new EvidenceClaimProviderException("invalid_request", "The evidence provider request violates contract v1.");
		ResponseTextOptions text = new() { TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("evidence_claims", OutputSchema, null, true) };
		text.Patch.Set("verbosity"u8, "low");
		CreateResponseOptions options = new()
		{
			Model = request.Model, Instructions = EvidenceClaimPromptContract.Instructions,
			MaxOutputTokenCount = request.MaximumOutputTokens, StoredOutputEnabled = false,
			ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.None }, TextOptions = text
		};
		options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.FactsJson));
		try
		{
			ClientResult<ResponseResult> response = await client.CreateResponseAsync(options, cancellationToken);
			if (response.Value.Status != ResponseStatus.Completed) throw new EvidenceClaimProviderException("incomplete_output", "The provider returned incomplete output.");
			IReadOnlyList<GeneratedEvidenceClaim> claims = EvidenceClaimOutputValidator.Parse(response.Value.GetOutputText(), settings.MaximumGeneratedClaimsPerSource);
			return new EvidenceClaimProviderResult(claims, response.Value.Usage?.InputTokenCount ?? 0, response.Value.Usage?.OutputTokenCount ?? 0);
		}
		catch (EvidenceClaimProviderException) { throw; }
		catch (Exception exception)
		{
			throw new EvidenceClaimProviderException(EvidenceClaimOutputValidator.Classify(exception, cancellationToken), "OpenAI evidence claim generation failed.", exception);
		}
	}

	public static OpenAiEvidenceClaimProvider Create(string apiKey, EvidenceGenerationOptions settings)
	{
		if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("OPENAI_API_KEY is required in evidence generation mode.", nameof(apiKey));
		ResponsesClientOptions options = new() { NetworkTimeout = TimeSpan.FromSeconds(settings.ProviderTimeoutSeconds), RetryPolicy = new ClientRetryPolicy(settings.MaximumProviderRetries) };
		return new OpenAiEvidenceClaimProvider(new ResponsesClient(new ApiKeyCredential(apiKey), options), settings);
	}
}
