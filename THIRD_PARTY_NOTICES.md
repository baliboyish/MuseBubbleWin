# Third-party components and branding

- Microsoft.Web.WebView2 is restored from NuGet and remains subject to Microsoft's
  package license. The Microsoft Edge WebView2 Runtime is installed separately.
  See https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2903.40.
- Microsoft.NETFramework.ReferenceAssemblies supplies build-time reference assemblies
  under its own package license; it is not this project's application code.
- Muse, muse.ai, Meta, their website content, and the official app icon belong to
  their respective owners. This project's MIT license does not grant rights to
  those trademarks, artwork, or service content.
- The official icon is fetched at runtime from the icon URL listed in
  https://muse.ai/manifest.json. It is cached locally and is not committed here.
- Inter is used only if installed by the user; otherwise the app uses Segoe UI.
  Neither font is redistributed in this repository.

MuseBubbleWin is an independent, unofficial wrapper and is not affiliated with
or endorsed by Meta or Muse.
