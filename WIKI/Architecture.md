# Architecture

GSD Orchestrator uses a durable state machine in C# to manage the Issue-to-PR lifecycle.

## Pipeline Flow

\\\mermaid
graph TD;
    Issue[GitHub Issue] --> Ingestion[Context Ingestion]
    Ingestion --> Planning[Architecture Planning]
    Planning --> Branching[Branch Creation]
    Branching --> Execution[Code Editing]
    Execution --> Validation[Test Execution]
    Validation --> PR[Pull Request Creation]
    PR --> Merge[Auto-Merge on CI Pass]
\\\

## Resilience
The pipeline is wrapped in Polly retry policies, ensuring that API rate limits from GitHub or Claude never kill a long-running resolution task.
