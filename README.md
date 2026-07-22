# Car Loan Origination Process — Demo

A Camunda 8 process that takes a car loan application from intake through underwriting,
document signing, and fund disbursement — with an automated eligibility check (DMN), two
human review steps (Camunda Forms), and four mocked backend integrations (C# job workers).

## What's in here

| File | What it is |
|---|---|
| `Loan Application Process.bpmn` | The process definition (`carLoanOriginationProcess`) |
| `assessCarLoanEligibility.dmn` | Decision table used by the `Assess Eligibility` business rule task |
| `collectApplicantVehicleInfo.form` | Form for the intake step (`Task_CollectInfo`) |
| `underwriterReview.form` | Form for the underwriter step (`Task_UnderwriterReview`) |
| `signLoanAgreement.form` | Form for the signing step (`Task_SignAgreement`) |
| `LoanOriginationWorkers/` | C# console app implementing the four service-task job workers (see its own [README](LoanOriginationWorkers/README.md) for worker-specific details, env vars, and error handling) |

## How the process flows

```
Start -> Collect Applicant & Vehicle Info (form)
      -> Perform Credit Check (worker, mocked)
      -> Assess Eligibility (DMN)
      -> Eligible? --no--> Notify Applicant of Rejection (worker) -> End
         |yes
         v
      Underwriter Review (form)
      -> Approved? --no--> Notify Applicant of Rejection (worker) -> End
         |yes
         v
      Generate Loan Documents (worker, mocked)
      -> Sign Loan Agreement (form)
      -> Disburse Funds to Dealership (worker, mocked)
      -> Notify Applicant of Disbursement (worker) -> End
```

The three service integrations behind the `worker (mocked)` steps — a credit bureau, a document
generator, and a payment processor — don't exist as real systems. `LoanOriginationWorkers/`
simulates all of them so the process can run end-to-end locally.

## Prerequisites

- A running Camunda 8 cluster — easiest locally via **C8 Run** (gRPC gateway on `:26500`, REST on
  `:8080`, Tasklist/Operate on `:8080/tasklist` and `:8080/operate`)
- `zbctl` (or `c8ctl`) to deploy resources
- .NET 8 SDK to run the job workers (`dotnet --version` should print `8.x`)

## Quick start

1. **Start Camunda 8 Run**, then confirm it's up (Operate/Tasklist reachable at `localhost:8080`).

2. **Deploy all five resources** from this directory:
   ```bash
   zbctl deploy resource \
     "Loan Application Process.bpmn" \
     assessCarLoanEligibility.dmn \
     collectApplicantVehicleInfo.form \
     underwriterReview.form \
     signLoanAgreement.form \
     --insecure
   ```

3. **Start the job workers** (in a separate terminal):
   ```bash
   cd LoanOriginationWorkers
   dotnet run
   ```
   Expect four "worker open" log lines, then it blocks waiting for jobs. Leave it running.

4. **Start a process instance** for `carLoanOriginationProcess` (via `zbctl create instance carLoanOriginationProcess --insecure`, Operate's "Start instance" if enabled, or your preferred method).

5. **Complete `Task_CollectInfo`** in Tasklist (`localhost:8080/tasklist`) — fill in applicant name,
   annual income, requested loan amount, and vehicle details.

6. **Watch it run automatically** through the credit check and the DMN eligibility decision. Check
   Operate — only `creditScore` and `debtToIncomeRatio` should appear as new process variables from
   the credit check (everything else the mock worker computes is filtered out by design).

7. **If eligible**, complete `Task_UnderwriterReview` in Tasklist:
   - Check **Approved** to proceed toward disbursement.
   - Leave it unchecked (optionally with a comment) to reject — this routes to the same rejection
     path as a DMN rejection, but the notification worker knows the difference and uses your
     underwriter comment instead of the DMN's reasoning.

8. **If approved**, document generation runs automatically, then complete `Task_SignAgreement` in
   Tasklist with a typed signature — the rest (fund disbursement, notification) completes
   automatically.

9. **Watch the worker console output** — it narrates every mocked step, including which
   notification message went out and why.

Repeat with varied inputs (a few low credit scores, a high debt-to-income ratio, a large loan
amount, an underwriter rejection) to exercise all the branches.

## Process variable reference

| Variable | Set by | Read by |
|---|---|---|
| `applicantName`, `annualIncome`, `requestedLoanAmount`, `vehicleMake`, `vehicleModel`, `vehiclePrice` | `Task_CollectInfo` form | Credit check worker (context), DMN, underwriter/signing form displays |
| `creditScore`, `debtToIncomeRatio` | `Task_PerformCreditCheck` worker (mocked; only these two survive the task's output mapping) | `assessCarLoanEligibility` DMN |
| `eligibilityResult` (`eligible`, `reason`) | `Task_AssessEligibility` DMN | `Gateway_IsEligible`, rejection notification (when DMN-rejected) |
| `approved`, `underwriterComments` | `Task_UnderwriterReview` form | `Gateway_IsApproved`, rejection notification (when underwriter-rejected) |
| `documentId` | `Task_GenerateDocs` worker (mocked) | — (informational only) |
| `signatureName` | `Task_SignAgreement` form | — (informational only) |
| `transactionId` | `Task_DisburseFunds` worker (mocked) | Disbursement notification (informational) |

See [`LoanOriginationWorkers/README.md`](LoanOriginationWorkers/README.md) for the worker
implementation, environment variables (`ZEEBE_GATEWAY_ADDRESS`, `ZEEBE_PLAINTEXT`), mock data
ranges, and error-handling behavior.
