using LoanOriginationWorkers.Models;
using Newtonsoft.Json;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace LoanOriginationWorkers.Workers;

public static class DocumentGenerationWorker
{
    public const string JobType = "documentGenerationService";

    public static async Task HandleAsync(IJobClient jobClient, IJob job)
    {
        try
        {
            var documentId = $"loan-doc-{Guid.NewGuid():N}";
            Console.WriteLine($"[document-generation-worker] jobKey={job.Key} -> generated documentId={documentId}");

            var output = new DocumentGenerationOutputVariables { DocumentId = documentId };

            await jobClient.NewCompleteJobCommand(job)
                .Variables(JsonConvert.SerializeObject(output))
                .Send();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[document-generation-worker] jobKey={job.Key} FAILED: {ex.Message}");
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
