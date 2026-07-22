using LoanOriginationWorkers.Workers;
using Zeebe.Client;
using Zeebe.Client.Api.Worker;

var gatewayAddress = Environment.GetEnvironmentVariable("ZEEBE_GATEWAY_ADDRESS") ?? "localhost:26500";
var usePlaintext = (Environment.GetEnvironmentVariable("ZEEBE_PLAINTEXT") ?? "true")
    .Equals("true", StringComparison.OrdinalIgnoreCase);

Console.WriteLine($"Connecting to Zeebe gateway at {gatewayAddress} (plaintext={usePlaintext})...");

var transportBuilder = ZeebeClient.Builder().UseGatewayAddress(gatewayAddress);
using var client = usePlaintext
    ? transportBuilder.UsePlainText().Build()
    : transportBuilder.UseTransportEncryption().Build();

var workers = new List<IJobWorker>
{
    OpenWorker(CreditBureauWorker.JobType, "credit-bureau-worker", CreditBureauWorker.HandleAsync),
    OpenWorker(DocumentGenerationWorker.JobType, "document-generation-worker", DocumentGenerationWorker.HandleAsync),
    OpenWorker(PaymentWorker.JobType, "payment-worker", PaymentWorker.HandleAsync),
    OpenWorker(NotificationWorker.JobType, "notification-worker", NotificationWorker.HandleAsync),
};

Console.WriteLine("All workers open. Waiting for jobs. Press Ctrl+C to exit.");

var shutdown = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.TrySetResult();
};

await shutdown.Task;

Console.WriteLine("Shutting down workers...");
foreach (var worker in workers)
{
    worker.Dispose();
}

IJobWorker OpenWorker(string jobType, string workerName, AsyncJobHandler handler) =>
    client.NewWorker()
        .JobType(jobType)
        .Handler(handler)
        .Name(workerName)
        .MaxJobsActive(8)
        .PollInterval(TimeSpan.FromMilliseconds(300))
        .Timeout(TimeSpan.FromSeconds(20))
        .Open();
