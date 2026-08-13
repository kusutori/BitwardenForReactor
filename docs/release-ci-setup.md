# Release CI setup

The repository contains two release workflows:

- `release.yml` creates x64 and ARM64 Native AOT portable ZIPs and attaches them to a GitHub Release when a `v1.2.3` tag is pushed. Signed MSIX packages are optional.
- `store.yml` creates an unsigned x64/ARM64 `.msixupload` for Partner Center whenever a release tag is pushed. It can also be run manually and optionally submit the package.

Both workflows restore the published `BitwardenCli.Core` package from NuGet.org.

## Direct-download release

No Secrets or repository variables are required for portable ZIP releases. Push a release tag after the workflow is on the default branch:

```powershell
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```

The portable ZIP is unpackaged and self-contained. Users extract it and run `BitwardenForReactor.exe`; Bitwarden CLI remains a separate prerequisite. Native AOT debugging symbols are published as a separate symbols ZIP for crash analysis.

### Self-signed development MSIX

GitHub development releases may include a self-signed MSIX. This is separate from the Microsoft Store package and is not a publicly trusted production signature. Configure these repository variables:

| Variable | Value |
| --- | --- |
| `PACKAGE_IDENTITY_NAME` | Stable package identity for the direct-download channel |
| `PACKAGE_PUBLISHER` | Exact certificate subject, for example `CN=Your Company` |
| `PACKAGE_PUBLISHER_DISPLAY_NAME` | Publisher name shown to users |
| `ENABLE_SIGNED_MSIX` | Set to `true` to append signed MSIX packages to GitHub Releases |

Create these repository secrets:

| Secret | Value |
| --- | --- |
| `MSIX_CERTIFICATE_BASE64` | Base64 encoding of the production PFX file |
| `MSIX_CERTIFICATE_PASSWORD` | PFX private-key password |

Encode a PFX without writing the result to the terminal:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\secure\release.pfx")) |
    Set-Clipboard
```

The certificate subject must exactly equal `PACKAGE_PUBLISHER`. Never commit a PFX, its password, or its Base64 representation.

The workflow exports the public `.cer` alongside the signed packages. Users must manually place that certificate in the Local Machine **Trusted People** store before installing the MSIX. The private PFX remains only in GitHub Actions Secrets.

Current development certificate:

- Subject: `CN=kusutori`
- SHA-1 thumbprint: `96C08C0E5244F2825325D1958ECD821724093985`
- Valid through: 2031-08-13

## Microsoft Store package

Reserve the app name in Partner Center, then create these repository variables using the exact values from **Product identity**:

| Variable | Value |
| --- | --- |
| `STORE_IDENTITY_NAME` | Package/Identity/Name |
| `STORE_PUBLISHER` | Package/Identity/Publisher |
| `STORE_PUBLISHER_DISPLAY_NAME` | Publisher display name |
| `STORE_PRODUCT_ID` | Partner Center product ID used by `msstore publish` |

Generating and downloading the `.msixupload` artifact does not require Partner Center API credentials. To enable the optional **publish** checkbox, associate an Entra application with Partner Center and add:

| Secret | Value |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Entra application/client ID |
| `AZURE_AD_APPLICATION_SECRET` | Entra client secret |
| `SELLER_ID` | Partner Center seller ID |

The first Store submission should be completed manually in Partner Center, including the age-rating questionnaire and listing information. Enable automated submission only after that package has been certified successfully.

## Version rules

Release versions must use `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH.REVISION`. The CI converts `v1.2.3` to MSIX version `1.2.3.0`. Prerelease suffixes such as `v1.2.3-beta.1` are intentionally rejected because MSIX manifest versions contain four numeric components.
