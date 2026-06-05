---
plan: 12-02
phase: 12-robustness-foundation
status: complete
completed: "2026-06-01"
commits:
  - sha: "84439f578cb45f8461054e073478cfa78bebf8a8"
    message: "feat(12-02): catch BrokenCircuitException in McpToolDispatcher, rethrow as McpException (ROB-03)"
  - sha: "ffc83ab0a03af7cf9ae7409bfa0b1cea1bbf5a13"
    message: "feat(12-02): add Polly circuit breaker as outermost strategy in mcp-tools pipeline (ROB-03)"
ci: green
requirements_satisfied:
  - ROB-03
---

# Plan 12-02 Summary — Polly Circuit Breaker

## What Was Built

Added Polly v8 ratio-based circuit breaker as outermost strategy in "mcp-tools" resilience pipeline.
McpToolDispatcher.CallAsync catches BrokenCircuitException → rethrows as McpException("MCP circuit breaker open — too many consecutive failures") per D-09.

## Files Modified

- Program.cs: added using Polly.CircuitBreaker + AddCircuitBreaker before AddRetry
- McpToolDispatcher.cs: added using Polly.CircuitBreaker + outer try/catch for BrokenCircuitException

## Circuit Breaker Config (D-08): FailureRatio=1.0, SamplingDuration=60s, MinimumThroughput=5, BreakDuration=30s

## Self-Check: PASSED

- [x] AddCircuitBreaker appears before AddRetry in Program.cs
- [x] McpToolDispatcher.CallAsync catches BrokenCircuitException → McpException with D-09 message
- [x] using Polly.CircuitBreaker in both files
- [x] ROB-03 satisfied
