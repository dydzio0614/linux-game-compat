using System.ClientModel;
using System.ClientModel.Primitives;
using LinuxGameCompat.Data;
using OpenAI.Responses;

namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed class OpenAiCompatibilitySummaryProvider(ResponsesClient client) : ICompatibilitySummaryProvider
{
	private static readonly BinaryData OutputSchema = BinaryData.FromString(CompatibilitySummaryPromptContract.OutputSchemaJson);

	public async Task<CompatibilitySummaryProviderResult> GenerateAsync(CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (!string.Equals(request.Model, "gpt-5.4-mini", StringComparison.Ordinal))
			throw new CompatibilitySummaryProviderException(ProviderFailureKind.Permanent, "The configured model is not approved by generator contract v1.");
		if (request.MaximumOutputTokens is < 1 or > 500)
			throw new CompatibilitySummaryProviderException(ProviderFailureKind.Permanent, "The output-token limit must be between 1 and 500.");

		ResponseTextOptions textOptions = new()
		{
			TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("compatibility_summary", OutputSchema, null, true)
		};
		textOptions.Patch.Set("verbosity"u8, "low");
		CreateResponseOptions options = new()
		{
			Model = request.Model,
				Instructions = CompatibilitySummaryPromptContract.Instructions,
			MaxOutputTokenCount = request.MaximumOutputTokens,
			StoredOutputEnabled = false,
			ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.None },
			TextOptions = textOptions
		};
		options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.Prompt));

		try
		{
			ClientResult<ResponseResult> response = await client.CreateResponseAsync(options, cancellationToken);
			if (response.Value.Status != ResponseStatus.Completed)
				throw new CompatibilitySummaryProviderException(ProviderFailureKind.Permanent, "The provider returned incomplete output.");
			(CompatibilityStatus Status, string Summary) output = ProviderOutputValidator.Parse(response.Value.GetOutputText());
			return new CompatibilitySummaryProviderResult(output.Status, output.Summary,
				response.Value.Usage?.InputTokenCount ?? 0, response.Value.Usage?.OutputTokenCount ?? 0);
		}
		catch (CompatibilitySummaryProviderException) { throw; }
		catch (Exception exception)
		{
			ProviderFailureKind kind = ProviderFailureClassifier.Classify(exception, cancellationToken);
			throw new CompatibilitySummaryProviderException(kind, "OpenAI summary generation failed.", exception);
		}
	}

	public static OpenAiCompatibilitySummaryProvider Create(string apiKey, TimeSpan timeout, int maximumRetries = 2)
	{
		if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("OPENAI_API_KEY is required in generation mode.", nameof(apiKey));
		ResponsesClientOptions options = new() { NetworkTimeout = timeout, RetryPolicy = new ClientRetryPolicy(maximumRetries) };
		return new OpenAiCompatibilitySummaryProvider(new ResponsesClient(new ApiKeyCredential(apiKey), options));
	}
}
