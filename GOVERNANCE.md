# Project governance

Miu starts as a project maintained by the owner of [KumarManuSaraswat/miu](https://github.com/KumarManuSaraswat/miu). There is no steering committee, paid support team, or voting membership. This document describes how decisions are made; it does not promise continued maintenance or a release schedule.

## Decisions and changes

The maintainer sets scope, reviews and merges contributions, moderates project spaces, and decides when to release. Contributions and constructive disagreement are welcome. Discuss substantial features in an issue before investing heavily; small fixes can arrive as a pull request. The deciding criteria are user benefit, reliability, privacy, accessibility, idle resource use, and the ability to keep local features usable without payment.

Record significant behavior, dependency, data-flow, and compatibility decisions in the relevant issue or pull request with the reason and alternatives considered. The maintainer has final responsibility for the shipped result. When a proposal is declined, explain the relevant constraint; an accepted proposal is not a delivery deadline. Contributor credit should remain in commit history and any existing attribution.

## Review and releases

Changes should use a pull request with relevant validation. The initial maintainer may review and merge their own work because a second maintainer is not assumed; do not describe that as independent review. For urgent security fixes, protect the private report, record the checks performed, and publish an appropriate advisory when disclosure is coordinated. [SECURITY.md](SECURITY.md) covers reporting, and [the maintainer guide](docs/MAINTAINING.md) covers review.

Only the repository owner, or an explicitly authorized maintainer, publishes official releases or changes repository access. Use the [release checklist](docs/RELEASE.md), identify the source commit, and state any checks that were not performed. Funding does not grant a sponsor control over review, private user data, or the roadmap. No sponsorship is implied by this policy.

## Future maintainers and continuity

Additional maintainers may be invited after sustained useful contributions and a discussion of responsibilities. Record any appointment and the permissions granted in a public project issue or pull request, without exposing account details. Grant only the access required. If ownership or maintenance status changes, update this file and the README; do not silently promise a successor or support arrangement. The MIT license permits continued forks subject to its terms, without implying endorsement by this repository.

Governance changes use a pull request and an explanation of their effect. Conduct in project spaces follows [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
