# Third-party notices

MirrorPulse's Apache-2.0 license does not replace dependency licenses. The
[generated inventory](dependencies/NOTICE.md) records exact NuGet versions,
license declarations, and original license/NOTICE files. The machine-readable
[inventory](dependencies/inventory.json) and [CycloneDX baseline](dependencies/bom.cdx.json)
contain archive hashes and no local user paths.

The inventory covers all restored production project graphs and x64/ARM64 SDK
runtime downloads. It also lists build tools, which are not necessarily
redistributed. CLI, Host, and MSIX outputs include this directory as `ThirdParty`.
Independent official Adapter releases retain their own notices; a release
candidate must inventory their actual payloads before redistribution.

## Regeneration

After a locked solution restore, run:

```powershell
pwsh ./eng/collect-dependency-notices.ps1 -OutputDirectory artifacts/licenses
```

Review changes and replace `docs/dependencies` with the generated files. The
collector verifies archive hashes against the NuGet cache and content hashes
against restore metadata. These hashes differ for signed packages because the
content hash excludes the signature. See the
[NuGet metadata specification](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File).
Missing declarations or missing exact license sources stop collection.

Most licenses come directly from the restored packages. When a package declares
an SPDX expression without shipping the license text, the exact upstream source
is recorded in `eng/dependency-license-sources.json` and `inventory.json`. Preserve
upstream notices, including runtime third-party notices, when updating versions.
Windows SDK packages using legacy license URLs remain explicitly identified as
build inputs in the inventory; their use does not grant general redistribution
rights for the SDK itself.

This dependency baseline is not a final release provenance attestation. The
candidate inventory must additionally reflect bundled Adapter packages, final
runtime payloads, platform prerequisites, and any assets introduced later.
