using System.Diagnostics.CodeAnalysis;

// Top-level statement programs generate a synthetic partial class named 'Program'.
// This file adds [ExcludeFromCodeCoverage] to that class so coverlet skips
// the entry-point bootstrap, arg-parsing, and host-wiring code.
[ExcludeFromCodeCoverage(Justification = "Entry-point bootstrap; not unit-testable")]
partial class Program { }
