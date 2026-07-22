using Newtonsoft.Json.Linq;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace LoanOriginationWorkers.Workers;

public static class NotificationWorker
{
    public const string JobType = "notificationService";

    private const string RejectApplicationElementId = "Task_RejectApplication";
    private const string NotifyDisbursedElementId = "Task_NotifyDisbursed";

    public static async Task HandleAsync(IJobClient jobClient, IJob job)
    {
        try
        {
            var message = job.ElementId switch
            {
                RejectApplicationElementId => BuildRejectionMessage(job.Variables),
                NotifyDisbursedElementId => BuildDisbursementMessage(job.Variables),
                _ => null,
            };

            if (message is null)
            {
                Console.WriteLine(
                    $"[notification-worker] jobKey={job.Key} elementId={job.ElementId} " +
                    "is an unrecognized call site for notificationService - completing with no action.");
            }
            else
            {
                Console.WriteLine($"[notification-worker] jobKey={job.Key} elementId={job.ElementId} -> {message}");
            }

            await jobClient.NewCompleteJobCommand(job).Send();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[notification-worker] jobKey={job.Key} FAILED: {ex.Message}");
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }

    private static string BuildRejectionMessage(string variablesJson)
    {
        var vars = JObject.Parse(variablesJson);
        var eligible = vars["eligibilityResult"]?["eligible"];

        // eligible == false -> rejected by the DMN before ever reaching underwriting; use its reason.
        // eligible == true -> the DMN approved them, so this rejection came from Task_UnderwriterReview
        // instead (approved == false) - the DMN's (positive) reason doesn't apply, use the underwriter's
        // comments instead.
        var reason = eligible?.Type == JTokenType.Boolean && eligible.Value<bool>() == false
            ? vars["eligibilityResult"]?["reason"]?.Value<string>()
            : vars["underwriterComments"]?.Value<string>();

        return string.IsNullOrWhiteSpace(reason)
            ? "Sending rejection notice: your car loan application was not approved."
            : $"Sending rejection notice: your car loan application was not approved ({reason}).";
    }

    private static string BuildDisbursementMessage(string variablesJson)
    {
        var transactionId = TryGetString(variablesJson, "transactionId");
        return transactionId is null
            ? "Sending disbursement notice: funds have been sent to the dealership."
            : $"Sending disbursement notice: funds have been sent to the dealership (transactionId={transactionId}).";
    }

    private static string? TryGetString(string variablesJson, params string[] path)
    {
        var token = (JToken?)JObject.Parse(variablesJson);
        foreach (var segment in path)
        {
            token = token?[segment];
        }

        return token?.Type == JTokenType.String ? token.Value<string>() : null;
    }
}
