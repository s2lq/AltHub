# Code signing with SignPath

Windows shows a SmartScreen warning for unsigned programs. [SignPath Foundation](https://signpath.org/) signs open-source
projects for free. AltHub's release workflow (`.github/workflows/release.yml`) already contains the signing steps; they run
as soon as the two secrets below exist. Until then releases are built and published unsigned.

## What you have to do once (it needs your GitHub account, so I cannot do it for you)

1. **Apply** at <https://signpath.org/apply> (SignPath Foundation, free for open source). Give them the repository
   `https://github.com/s2lq/AltHub` and say it is a GPL-3.0 fork of Roblox MultiHub with its own changes. They review each
   project by hand; approval is not guaranteed (a fork has to show real work of its own).
2. After approval, in the SignPath web app create the **project** with the slug `AltHub`, add a **signing policy** with the
   slug `release-signing`, and an **artifact configuration** that signs a single file, `AltHub.exe`.
3. Install the **SignPath GitHub App / connect the repository** as they describe, and create an **API token** for the CI user.
4. In your repository go to *Settings > Secrets and variables > Actions* and add two secrets:
   - `SIGNPATH_API_TOKEN` - the API token
   - `SIGNPATH_ORGANIZATION_ID` - your organization id (shown in the SignPath web app)
5. Run the **Release** workflow. The log shows *Sign with SignPath*, and the published `AltHub.exe` is signed.

If your slugs differ, change `project-slug` / `signing-policy-slug` in `release.yml`.

## Text SignPath requires on the project page

Paste this into the README **after** you are approved (SignPath checks it):

> ## Code signing policy
> Free code signing for Windows builds is provided by [SignPath.io](https://signpath.io), certificate by the
> [SignPath Foundation](https://signpath.org). Releases are built from the source in this repository by the public
> [GitHub Actions workflow](.github/workflows/release.yml) and signed there.
>
> - **Committers and reviewers:** [s2lq](https://github.com/s2lq)
> - **Approvers:** [s2lq](https://github.com/s2lq)
>
> **Privacy:** AltHub stores your accounts only on your PC (encrypted) and sends nothing anywhere except to Roblox (and to
> GitHub when it checks for updates). See the README.
