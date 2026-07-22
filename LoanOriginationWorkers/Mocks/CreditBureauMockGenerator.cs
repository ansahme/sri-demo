namespace LoanOriginationWorkers.Mocks;

public static class CreditBureauMockGenerator
{
    // Ranges chosen to produce a realistic mix of eligible/ineligible outcomes against
    // assessCarLoanEligibility.dmn's thresholds (creditScore < 580, debtToIncomeRatio > 0.45).
    public static (int CreditScore, double DebtToIncomeRatio) Generate()
    {
        var creditScore = Random.Shared.Next(550, 801); // 550-800 inclusive
        var debtToIncomeRatio = Math.Round(0.10 + Random.Shared.NextDouble() * 0.50, 2); // 0.10-0.60
        return (creditScore, debtToIncomeRatio);
    }
}
