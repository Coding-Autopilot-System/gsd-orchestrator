import os
base = "C:/PersonalRepo/portfolio/gsd-orchestrator/src/GsdOrchestrator.Tests"
content = open("C:/PersonalRepo/portfolio/gsd-orchestrator/validating_template.cs").read()
with open(f"{base}/States/ValidatingStateTests.cs", "w") as f:
    f.write(content)
print("done")
