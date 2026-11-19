# QR Sticker Print Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax for tracking.

**Goal:** Print local QR codes on physical sticker pages.
**Architecture:** A dedicated React section builds a self-contained vector print document. A separate, restricted Windows WebView2 presents printing without a host bridge; browser preview uses a separate window.
**Tech Stack:** React, TypeScript, qrcode, WinUI 3, WebView2.
**Spec:** docs/superpowers/specs/2026-11-05-qr-sticker-print-design.md (approved by user).

## Global Constraints

60 × 60 mm default, padding 2 mm, 1–200 separate pages. Custom width 20–200 mm, height 16–200 mm, padding 1–10 mm. Preserve original QR content, four-module quiet zone, optional caption and existing exports. No CRM changes. Work in existing shared checkout: current product files are untracked and absent from HEAD, so a new git worktree would omit them; no staging or commits of other chats' work.

## Review Focus

Caption with HTML delimiters must remain text. Dense QR must reject insufficient physical size. Empty numeric inputs must disable printing. Popup refusal must report recovery. Closing the print window during initialization must release WebView resources.

### Task 1: Vector sticker document

**Files:** Create src/SideWidget.Web/src/qr-stickers.ts and qr-stickers.test.ts.
**Interfaces:** stickerLayout(text, options), stickerDocument(text, options) returning SVG geometry and a self-contained HTML document.

- [ ] Write tests for dimensions, page count, escaping, preserved multiline Unicode and quiet zone, and too-small modules.
- [ ] Run focused tests and observe missing module failure.
- [ ] Generate SVG directly with qrcode; integer module coordinates, physical layout and four-module white margin. Reject modules below 0.25 mm. Build bounded print pages without scripts or resources.
- [ ] Run focused tests, including decode after rasterization.

### Task 2: UI and Windows printing

**Files:** Create QrStickerPrint.tsx, qr-stickers.css, QrPrinting.cs. Modify LocalTools.tsx, Controller.cs, MainWindow.cs, Protocol.cs.
**Interfaces:** qr.print {html}; native returns {opened:true}, never printed:true.

- [ ] Add labelled format/custom dimensions/padding/count/caption inputs and proportionate preview, reusing current tokens.
- [ ] Browser opens local document synchronously from click; Windows invokes qr.print.
- [ ] Restrict print WebView: no JavaScript, host objects, messages, external navigation, permissions, downloads or network resources; fixed CSP before supplied HTML. Print button and auto-open print UI after navigation. Closing cancels initialization and disposes WebView.
- [ ] Add UI tests for blank/invalid numeric fields, native invocation, failure and popup refusal.
- [ ] Run web tests/build and isolated native build. Inspect rendered panel at 360/444/799 px and printed page geometry. Run focused Impeccable detector.
- [ ] Record evidence and physical-printer limitations. No installation/restart while another chat owns native release.
