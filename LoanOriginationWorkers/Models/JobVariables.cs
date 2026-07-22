using Newtonsoft.Json;

namespace LoanOriginationWorkers.Models;

public class CreditCheckInputVariables
{
    [JsonProperty("applicantName")]
    public string? ApplicantName { get; set; }

    [JsonProperty("annualIncome")]
    public double AnnualIncome { get; set; }

    [JsonProperty("requestedLoanAmount")]
    public double RequestedLoanAmount { get; set; }

    [JsonProperty("vehicleMake")]
    public string? VehicleMake { get; set; }

    [JsonProperty("vehicleModel")]
    public string? VehicleModel { get; set; }

    [JsonProperty("vehiclePrice")]
    public double VehiclePrice { get; set; }
}

public class CreditCheckOutputVariables
{
    [JsonProperty("creditScore")]
    public int CreditScore { get; set; }

    [JsonProperty("debtToIncomeRatio")]
    public double DebtToIncomeRatio { get; set; }
}

public class DocumentGenerationOutputVariables
{
    [JsonProperty("documentId")]
    public string DocumentId { get; set; } = "";
}

public class PaymentOutputVariables
{
    [JsonProperty("transactionId")]
    public string TransactionId { get; set; } = "";
}
