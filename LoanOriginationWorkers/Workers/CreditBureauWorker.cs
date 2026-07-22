using LoanOriginationWorkers.Mocks;
using LoanOriginationWorkers.Models;
using Newtonsoft.Json;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace LoanOriginationWorkers.Workers;

public static class CreditBureauWorker
{
    public const string JobType = "creditBureauService";

    public static async Task HandleAsync(IJobClient jobClient, IJob job)
    {
        try
        {
            var input = JsonConvert.DeserializeObject<CreditCheckInputVariables>(job.Variables);
            var (creditScore, debtToIncomeRatio) = CreditBureauMockGenerator.Generate();

            Console.WriteLine(
                $"[credit-bureau-worker] jobKey={job.Key} applicant={input?.ApplicantName} " +
                $"-> creditScore={creditScore}, debtToIncomeRatio={debtToIncomeRatio}");

            var output = new CreditCheckOutputVariables
            {
                CreditScore = creditScore,
                DebtToIncomeRatio = debtToIncomeRatio,
            };

            await jobClient.NewCompleteJobCommand(job)
                .Variables(JsonConvert.SerializeObject(output))
                .Send();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[credit-bureau-worker] jobKey={job.Key} FAILED: {ex.Message}");
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
