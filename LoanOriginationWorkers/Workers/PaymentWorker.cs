using LoanOriginationWorkers.Models;
using Newtonsoft.Json;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace LoanOriginationWorkers.Workers;

public static class PaymentWorker
{
    public const string JobType = "paymentService";

    public static async Task HandleAsync(IJobClient jobClient, IJob job)
    {
        try
        {
            var transactionId = $"txn-{Guid.NewGuid():N}";
            Console.WriteLine($"[payment-worker] jobKey={job.Key} -> disbursed funds, transactionId={transactionId}");

            var output = new PaymentOutputVariables { TransactionId = transactionId };

            await jobClient.NewCompleteJobCommand(job)
                .Variables(JsonConvert.SerializeObject(output))
                .Send();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[payment-worker] jobKey={job.Key} FAILED: {ex.Message}");
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
