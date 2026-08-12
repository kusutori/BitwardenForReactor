# Release CI setup

The repository contains two release workflows:

- `release.yml` creates signed x64 and ARM64 MSIX packages and attaches them to a GitHub Release when a `v1.2.3` tag is pushed.
- `store.yml` creates an unsigned x64/ARM64 `.msixupload` for Partner Center whenever a release tag is pushed. It can also be run manually and optionally submit the package.

Both workflows check out `kusutori/BitwardenCli.Core` beside this repository because the application currently uses a sibling `ProjectReference`.

## Direct-download release

Create these repository variables under **Settings > Secrets and variables > Actions > Variables**:

| Variable | Value |
| --- | --- |
| `PACKAGE_IDENTITY_NAME` | Stable package identity for the direct-download channel |
| `PACKAGE_PUBLISHER` | Exact certificate subject, for example `CN=Your Company` |
| `PACKAGE_PUBLISHER_DISPLAY_NAME` | Publisher name shown to users |
| `ENABLE_DIRECT_RELEASE` | Set to `true` to create signed GitHub Releases on tag pushes |

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

Push a release tag after configuring the values. The direct-release job stays skipped until `ENABLE_DIRECT_RELEASE` is `true`:

```powershell
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

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
