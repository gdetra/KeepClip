# GitHub branch protection configuration

This is the intended repository configuration. Committing this file or `CODEOWNERS` does not activate GitHub protection rules.

## Contribution policy

Protect `main`. Working branches and forks remain writable so contributors can prepare pull requests.

Create a branch ruleset named **Main contributions** in **Settings → Rules → Rulesets**:

| Setting | Value |
| --- | --- |
| Enforcement | Active after the workflow and CODEOWNERS changes are merged |
| Target | `refs/heads/main` |
| Bypass actor | Individual user `gdetra` only |
| Bypass mode | Always allow |
| Require a pull request before merging | Enabled |
| Required approving reviews | 1 |
| Require review from Code Owners | Enabled |
| Dismiss stale approvals when new commits are pushed | Enabled |
| Require conversation resolution | Enabled |
| Require status checks | Enabled |
| Require branches to be up to date | Enabled |

Required checks from Windows CI:

- `Formatting`
- `Tests (x64)`
- `Tests (x86)`
- `Release and packaging script checks`

Select the checks emitted by the actual workflow run and restrict them to GitHub Actions as the expected source when the setting is available.

The `Always allow` exception lets `gdetra` push directly or merge a maintainer-authored PR without needing to approve their own changes. Do not grant bypass to all writers, all administrators, or contributor teams.

The base branch must contain `.github/CODEOWNERS` with `* @gdetra` before Code Owner reviews can be enforced. Confirm that this account retains the required repository permissions.

## Main history

Create a separate branch ruleset named **Main history**, targeting `refs/heads/main`, with no bypass actors:

- Restrict deletions.
- Block force pushes.

Keeping history protection separate means the maintainer's normal PR bypass does not also permit accidental force pushes or deletion of `main`.

## Activation order

1. Review and merge the CI, contribution guide, PR template, and CODEOWNERS changes.
2. Let Windows CI complete on `main` so all required check names are registered.
3. Add `gdetra` as the individual bypass actor for **Main contributions**, using **Always allow**, before activating that ruleset.
4. Configure and activate **Main contributions** and **Main history**.
5. Verify that another contributor cannot push directly to `main` and can open a PR from a working branch or fork. Confirm that an unapproved or failing PR cannot be merged.
6. Verify that `gdetra` retains direct-push bypass for the contribution rules, while history protection remains enabled.

Ruleset availability depends on repository visibility and the GitHub plan. GitHub supports individual-user bypass actors on repository rulesets. Check for existing branch protections or inherited rules before adding these rulesets, since applicable rules are combined.

References: [Creating repository rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository), [individual-user bypass support](https://github.blog/changelog/2026-05-07-repository-rulesets-user-bypass-and-branch-renaming/).
