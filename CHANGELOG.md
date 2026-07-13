# Changelog

## [4.1.0](https://github.com/Coding-Autopilot-System/gsd-orchestrator/compare/v4.0.0...v4.1.0) (2026-07-13)


### Features

* **26-01:** add coverlet.runsettings with declarative boilerplate exclusions ([2feb2c9](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/2feb2c95243377a2bddd0f2569d395dfa5dee373))
* **26-01:** rewrite CI gate to ratcheted branch-coverage threshold, add coverage tests ([bffdee7](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/bffdee793f6b9683e30d40c04cd6c2037765df5e))
* **27-02:** map loop failures to typed FailureState records ([#21](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/21)) ([439e75d](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/439e75d5591526eb1ed02493ce5246e5bca39b29))


### Bug Fixes

* add JsonStringEnumConverter and flush-to-disk before atomic rename to prevent checkpoint corruption and replay errors ([795242f](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/795242f014da04cef1c7d5ca5670d65f8125c61a))
* add JsonStringEnumConverter and flush-to-disk before atomic rename to prevent checkpoint corruption and replay errors ([#17](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/17)) ([335e4d3](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/335e4d39370f9e8af7d1af21fbc36ad5754963e6))
* audit sweep ([#11](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/11)) ([526e4e5](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/526e4e5ea75c5221ae1631ef81d95391f7eac8bc))
* **ci:** repin release-please reusable workflow to reachable SHA ([#25](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/25)) ([8146354](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/8146354dda900b2309e0035e833efc103aa2ffcb))
* **ci:** wire release-please PAT into reusable workflow caller ([#30](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/30)) ([f2d4800](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/f2d480078a6540e563b297eb6b6bb875f0dd7a6d))
* **powershell:** mark non-ASCII scripts as UTF-8 ([#29](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/29)) ([edfa2e1](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/edfa2e19c4bf36c44e27ac342be83fd816074eca))
* **recovery:** preserve failed MCP state for fault injection ([#20](https://github.com/Coding-Autopilot-System/gsd-orchestrator/issues/20)) ([2080c44](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/2080c440cd0865b83a042d7d5e5d9a60fb5a3184))
* **validating:** guard null fileResult from MCP before ParseInnerJson ([42d80bc](https://github.com/Coding-Autopilot-System/gsd-orchestrator/commit/42d80bcdc34ef3813a91a878f8c78c5ba801414a))
