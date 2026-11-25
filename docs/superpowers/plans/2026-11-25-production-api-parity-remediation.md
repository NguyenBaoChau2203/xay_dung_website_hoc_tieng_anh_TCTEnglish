# Restore production API integrations

- [x] Validate the owner-authorized local OpenAI key against the three model metadata endpoints. Apply production text, transcription and voice options in the hosting configuration and verify the saved settings.
- [x] Generate a VAPID P-256 key pair for production, set the public origin and support contact, and verify the saved pair.
- [x] Submit one internal message through the live contact form. The SMTP relay accepted it. Inbox receipt is a separate check.
- [x] Disable the existing VNPay sandbox configuration for the public checkout. PayOS was disabled during this audit and was enabled in the [subsequent payment setup](2026-11-25-no-key-demo-and-production-payos.md).
- [x] Verify OpenAI over IPv4 TLS from hosting. Model metadata and a text response returned HTTP 200. Add `api.openai.com` to the production IIS process `NO_PROXY` environment setting. Verify Google login, an AI Chat request (HTTP 200), Voice topics (`available=true`), and the VAPID endpoint (`configured=true`). Remove the single test chat. Production readiness and release 123 remained available.
- [x] Reproduce the Facebook callback error. The live challenge requests `email`, and Graph reported that the permission was granted. Graph returned the account ID without `email`. Confirm the same profile has a directly assigned, verified email before retrying.
- [x] Save and verify the Meta privacy policy URL. Meta still requires a public data deletion instructions URL and business verification before the app can be published. The current Facebook permissions are ready for testing.
- [x] Add clear data deletion steps to the existing privacy page in the local checkout. The Release build succeeded. At 1440, 768 and 390 px the anchor loaded, the ordered steps fit the viewport, both links resolved, and keyboard focus moved from the profile link to Contact. The edited page is not deployed.
- [ ] Publish the deletion instructions through the release workflow and set the public section URL in Meta. Meta's verification prompt requires creating a Business Suite account for this app.
- [ ] Verify Facebook login after attaching and verifying an email for the Facebook profile. Complete provider-side requirements for public use.
- [ ] Verify receiving a browser notification, speaking with a microphone, and completing a real checkout once their respective prerequisites are available.
