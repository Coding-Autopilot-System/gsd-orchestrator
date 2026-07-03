# Security Policy

## Reporting a Vulnerability

Do not report vulnerabilities in public issues, discussions, pull requests, or logs.

Use GitHub's private vulnerability reporting feature on the affected repository when
available. Otherwise, email `OgeonX@gmail.com` with:

- affected repository, version, branch, or commit;
- impact and realistic attack scenario;
- minimal reproduction steps or proof of concept;
- suggested mitigation, if known;
- whether the report may be shared publicly after remediation.

Do not include active credentials, personal data, or unrelated private information.

## Response Targets

The project aims to:

- acknowledge a report within 3 business days;
- provide an initial assessment within 7 business days;
- coordinate remediation and disclosure based on severity and exploitability.

These are targets for a maintainer-led portfolio, not a commercial support agreement.

## Scope

Security reports are especially relevant for:

- authentication, authorization, and identity boundaries;
- prompt injection or tool-use escalation;
- secret exposure and unsafe logging;
- workflow permission or supply-chain weaknesses;
- sandbox escape, arbitrary code execution, or unsafe autonomous actions.

Unsupported branches, intentionally vulnerable demonstrations, social engineering, and
denial-of-service testing against shared infrastructure are out of scope unless a
repository states otherwise.

## Safe Harbor

Good-faith research that avoids privacy violations, data destruction, service
degradation, and unauthorized persistence will be treated as authorized within the
scope of this policy.
