# SafeWeb Gateway Code Signing Policy

**Project:** SafeWeb Gateway  
**Repository:** https://github.com/ModiyaBhargav/SafeWeb-Gateway  
**Current release line:** v4.5  
**Policy status:** Initial policy; subject to revision if the project gains additional maintainers or a signing provider requires changes.

## 1. Maintainer and responsibility

SafeWeb Gateway is currently maintained by one person.

- **Main author and source-code maintainer:** GitHub account `@ModiyaBhargav`
- **Code reviewer:** The maintainer performs a documented self-review because no second maintainer is currently available.
- **Release approver:** The maintainer.
- **Signing approver:** The maintainer, subject to the signing provider's approval controls and requirements.

The project does not claim independent two-person review or separation of duties. If a signing provider requires independent approval, the project will add an eligible independent reviewer before enabling production signing, or will not use that signing workflow until the requirement is met.

## 2. Source and release integrity

- Releases must be built from source code committed to this public repository.
- Each release must identify its source commit and version/tag.
- Release artifacts must be produced by a documented build workflow.
- Build logs and test results should be retained with the release record.
- Changes to release scripts, dependencies, and signing configuration must be reviewed before use.
- No private signing key or certificate secret may be committed to the repository.

## 3. Release checks

Before publishing a release, the maintainer must:

1. Review the source changes and dependency updates.
2. Check the third-party notices and applicable licences.
3. Build the Windows x64 release from the tagged source.
4. Run the available build and diagnostic tests.
5. Verify the produced files and record their SHA-256 hashes.
6. Confirm that the release notes accurately state known limitations.
7. Approve publication and, if enabled, request signing through the authorized signing workflow.

## 4. Signing controls

- SafeWeb will use only a signing certificate and signing service authorized for the project.
- Signing credentials must be stored in the provider's protected secret store or equivalent secure key-management system.
- Private keys must not be exported into the source repository or ordinary build artifacts.
- Signing must be limited to release artifacts built from reviewed, versioned source.
- Signing requests and approvals must be auditable.
- A successful signature does not guarantee that Windows Smart App Control or any other application-control policy will trust or allow the file.

## 5. Third-party components

The MIT licence in this repository applies only to SafeWeb's original project code. Third-party dependencies, including WinDivert and NuGet packages, remain subject to their own licences and notices. SafeWeb will not represent third-party binaries as original project code or sign/redistribute them without checking the applicable terms and signing-provider rules.

## 6. Security and incident response

If a signing credential, release artifact, or build workflow is suspected to be compromised, the maintainer will suspend releases and signing, investigate the affected versions, revoke or rotate affected credentials where possible, and publish an incident notice and corrected release when appropriate.

## 7. Changes to this policy

This policy will be updated when the project adds maintainers, adopts independent review, changes its release process, or receives additional requirements from a signing provider.
