# Loan Origination Workers

C# job workers for the four service tasks in `../Loan Application Process.bpmn`, using the
community-maintained [`zb-client`](https://github.com/camunda-community-hub/zeebe-client-csharp)
library (chosen over the official `Camunda.Orchestration.Sdk` because that SDK is a technical
preview requiring Camunda 8.9+, and this process declares execution platform 8.5.0).

| Job type | BPMN task(s) | Behavior |
|---|---|---|
| `creditBureauService` | Task_PerformCreditCheck | Mocked credit check. Generates a random `creditScore` (550-800) and `debtToIncomeRatio` (0.10-0.60) so the downstream DMN decision exercises both the eligible and ineligible branches across a few runs. |
| `documentGenerationService` | Task_GenerateDocs | Mocked. Completes with a synthetic `documentId`. |
| `paymentService` | Task_DisburseFunds | Mocked. Completes with a synthetic `transactionId`. |
| `notificationService` | Task_RejectApplication **and** Task_NotifyDisbursed | One worker, both call sites. Branches on `job.ElementId` to send the right message. |

All four are mocks — there are no real external systems (no credit bureau, payment gateway, or
notification service) behind this demo process. They log to the console instead of doing anything
real.

## Run

```bash
dotnet run
```

Connects to the Zeebe gateway at `localhost:26500` in plaintext by default. Override with:

- `ZEEBE_GATEWAY_ADDRESS` (default `localhost:26500`)
- `ZEEBE_PLAINTEXT` (default `true`; set to `false` for a TLS-secured cluster)

Note: this app only talks gRPC (job activation/completion). It doesn't touch the REST API
(`localhost:8080`) at all — that's only relevant for Operate/Tasklist UI and manual task
completion during testing.

Stop with Ctrl+C — it closes all workers and disconnects cleanly.

## End-to-end verification

1. Start a local Camunda 8 Run (gRPC `:26500`, REST `:8080`, no auth).
2. Deploy the process artifacts (BPMN, DMN, and all three forms) from the parent directory, e.g.
   `zbctl deploy resource "../Loan Application Process.bpmn" ../assessCarLoanEligibility.dmn ../collectApplicantVehicleInfo.form ../underwriterReview.form ../signLoanAgreement.form --insecure`.
   (See [`../README.md`](../README.md) for the full end-to-end walkthrough, including the two
   human review steps.)
3. `dotnet run` — expect 4 "worker open" style log lines, then it blocks waiting for jobs.
4. Start a process instance and complete `Task_CollectInfo` via Tasklist (`localhost:8080/tasklist`)
   with the 6 form fields (`applicantName`, `annualIncome`, `requestedLoanAmount`, `vehicleMake`,
   `vehicleModel`, `vehiclePrice`).
5. Watch it flow through `Task_PerformCreditCheck` — check in Operate that **only** `creditScore`
   and `debtToIncomeRatio` land as process variables (the BPMN's `zeebe:ioMapping` output mapping
   filters everything else) — then `Task_AssessEligibility` (DMN), then `Gateway_IsEligible`.
6. If eligible, complete `Task_UnderwriterReview` via Tasklist with the `approved` checkbox and
   optional `underwriterComments`. If `approved` is unchecked, the instance routes to
   `Task_RejectApplication` just like a DMN rejection would — the notification worker distinguishes
   the two cases and uses `underwriterComments` (not the DMN's `eligibilityResult.reason`) for the
   message in this case.
7. If approved, complete `Task_GenerateDocs` (worker-driven) then `Task_SignAgreement` via Tasklist
   with a `signatureName`, then watch `Task_DisburseFunds` and `Task_NotifyDisbursed` complete
   automatically.
8. Run 5-10 instances with varied inputs, covering DMN rejection, underwriter rejection, and full
   approval. Confirm the console shows distinct, correct messages for `Task_RejectApplication` vs
   `Task_NotifyDisbursed` in all cases, and Operate shows no unexpected incidents.

## Error handling

Each worker wraps its handler in try/catch. On success it completes the job; on any exception it
calls `NewFailCommand` with `retries - 1` and the exception message, which is the standard Zeebe
pattern — once retries are exhausted, Operate surfaces a visible incident. None of these four
tasks have boundary error events today, so BPMN errors (`NewThrowErrorCommand`) aren't used; add
that if/when a boundary error event is modeled for one of these service tasks.
