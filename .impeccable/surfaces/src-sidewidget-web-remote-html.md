---
version: 1
slug: "src-sidewidget-web-remote-html"
primary_target: "src/SideWidget.Web/remote.html"
related_targets: ["src/SideWidget.Web/src/remote/PhoneRemoteApp.tsx","src/SideWidget.Web/src/PhoneRemoteSettings.tsx"]
---

# SideWidget phone remote — Operate

Audience: owner of an Android phone controlling music on their Windows PC in a trusted home LAN. Primary task: pause, skip or adjust PC volume without returning to the keyboard. Existing SideWidget identity and the approved screen structure are fixed; no new visual world is introduced.

## Direction contract

**THESIS.** Music stays on the PC; the phone is a remembered remote. Real playback and connection state lead the screen.

**OWN-WORLD.** Existing Medeo, graphite and light neutral palettes, quiet surfaces, restrained 10/16px radii, familiar transport icons. Album art belongs to the actual session, no decorative stock imagery.

**STORY.** Scan once, trust your own HTTPS certificate, compare pairing code and approve on PC. Open installed remote later; connection restores automatically. Revocation and missing source remain explicit.

**FIRST VIEWPORT.** Compact SideWidget/connection header, centered album art, wrapping title/artist, seek timing, large previous/play/next controls, PC volume. Installation and network help follow below. Main playback/input targets at least48px; primary play80px. Help disclosures use44px.

**FORM.** User-approved phone music remote extending the existing Windows curtain, code-led implementation. No new palette/type choice or concept roll claimed.

**SIGNATURE INTERACTION.** Controls disable immediately on lost connection and recover from real state when the page returns online; no replay of unknown-outcome next/previous commands.

**FINISH.** Actual Chromium views inspected at360/412/768, dark/light and large text. Desktop QR inspected360/444. Persistent Chromium installability errors=[] with fixture-only certificate pin. Physical Android CA/PWA and live LAN acceptance explicitly pending in docs/PHONE_REMOTE.md. PWA PNG icons derive from the existing sidemenu.png; no external raster assets.
