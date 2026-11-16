# Contributing

Fork this repository, create a branch, make a focused change, and open a pull request.
Build with `./build.ps1` on Windows and run the checks described in the README.

For UI changes, check both themes at 100%, 125%, and 150% scaling. Include a screenshot
without private conversations. Exercise hover, keyboard focus, and repeated repainting;
one `DrawToBitmap` preview is insufficient to establish live repaint correctness.

Keep authentication in WebView2. Never add credentials, cookies, profile directories,
personal screenshots, access tokens, or generated diagnostic logs to a commit.
Do not replace native browser permissions with an unconditional allow handler.

The attachment workaround uses an experimental Chrome DevTools Protocol chooser
interception API. Changes should preserve cancellation and target the original input.
Do not test uploads against someone's signed-in account without their permission.
