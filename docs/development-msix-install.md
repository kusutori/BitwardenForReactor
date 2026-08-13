# Development MSIX installation

The GitHub Release MSIX is a development build signed with the project's self-signed `CN=kusutori` certificate. Windows does not trust this certificate by default. Microsoft Store packages use Microsoft's Store signature instead and do not require these steps.

## Verify and trust the certificate

1. Download `BitwardenForReactor-Development.cer` from the same GitHub Release as the MSIX.
2. Open the certificate and confirm that **Issued to** and **Issued by** are both `kusutori`.
3. On the **Details** tab, confirm the SHA-1 thumbprint is `96C08C0E5244F2825325D1958ECD821724093985`.
4. Select **Install Certificate**, choose **Local Machine**, then place it in **Trusted People**. Administrator approval is required.
5. Install the MSIX matching the computer architecture: `x64` for normal Intel/AMD Windows PCs or `ARM64` for Windows on Arm.

Only trust the certificate when the files were downloaded directly from the `kusutori/BitwardenForReactor` GitHub repository. Remove it from the Local Machine `Trusted People` certificate store when development builds are no longer needed.

This development certificate is valid from 2026-08-13 through 2031-08-13. A future certificate rotation requires trusting the replacement certificate before installing releases signed with it.
