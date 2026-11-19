# SideWidget

[Русский](../../README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · **日本語** · [Deutsch](README.de.md) · [Українська](README.uk.md) · [Беларуская](README.be.md) · [Español](README.es.md) · [Português (Brasil)](README.pt-BR.md)

SideWidget は、日常的に使うツールをまとめた Windows 向けのコンパクトなパネルです。画面上端の時計をクリックすると、ファイル検索やピン留めしたアプリなどにアクセスできます。既定のショートカットは `Ctrl+Alt+Space` です。

SideWidget は Windows の表示言語に合わせます。対応していない言語の場合は英語で起動します。**設定 → アプリの言語**で変更できます。

SideWidget は常識を大切にし、GPT-6.1 Sol の支援を受けて開発しています。

## スクリーンショット

<p align="center">
  <img src="images/sidewidget-home.png" width="760" alt="検索、プレーヤー、よく使うツールが並ぶ SideWidget のメインパネル">
</p>

<p align="center">
  <img src="images/sidewidget-commands-ja.png" width="590" alt="日本語表示でフォルダーアイコンを選ぶ SideWidget の画面">
</p>

プレーヤーは Windows で音楽を再生中に撮影しました。各ツールの画面にはサンプルデータを使用しています。

## 指示の例

**ファイルとフォルダー**を開き、操作を選びます。

- **ファイルを整理。** **請求書を年別に整理**を選び、スクリーンショットをそのまま残すルールを追加します。プレビューで対象ファイルを確認し、移動を確定します。
- **フォルダーアイコン。** デスクトップ上の複数のフォルダーをまとめて設定するか、フォルダーを一つ選びます。

## その他のツール

<details>
<summary><strong>電卓</strong></summary>

パーセントや括弧を含む式を計算し、結果をコピーしたり履歴を参照したりできます。

<img src="images/sidewidget-tool-calculator.png" width="520" alt="電卓">

</details>

<details>
<summary><strong>QR コード</strong></summary>

テキストやリンクを QR コードにして SVG、PNG、JPEG で保存できます。

<img src="images/sidewidget-tool-qr.png" width="520" alt="QR コード">

</details>

<details>
<summary><strong>単位変換</strong></summary>

距離や温度などを変換します。よく使う単位の組み合わせはお気に入りに登録できます。

<img src="images/sidewidget-tool-units.png" width="520" alt="単位変換">

</details>

<details>
<summary><strong>カラーピッカー</strong></summary>

画面から色を拾い、HEX、RGB、HSL でコピーしたりパレットに保存したりできます。

<img src="images/sidewidget-tool-colors.png" width="520" alt="カラーピッカー">

</details>

## 主な機能

- 履歴付きの電卓、SVG・PNG・JPEG 形式の QR コード、単位換算、画面からの色の取得、色形式の変換。
- 起動中の Everything を使ったファイル検索と、ピン留めしたアプリの起動。
- **ファイルとフォルダー。** デスクトップのファイル整理、フォルダーアイコンのプレビュー、共通スタイルまたはフォルダーごとのスタイル設定、直前のアイコン変更の取り消し。
- 対応プレーヤーの再生操作、音量ミキサー、PC のシステム情報の表示。
- カレンダー、ローカル Markdown 下書き、外部サービスによる翻訳。
- パネルのサイズやショートカットの変更、表示するモニターの選択。

## インストール

Windows 10 または 11 x64 が必要です。[Releases](https://github.com/Kelltomal/SideWidget/releases/latest) を開き、ZIP を選びます。

- **Lite**（`-lite-win-x64.zip`）をおすすめします。不足するランタイムは個別にダウンロードします。
- **フル版**（`-portable-win-x64.zip`）はランタイムを同梱し、オフラインで使えます。

ZIP を専用のフォルダーに展開し、`App` フォルダーの隣にある `SideWidget.exe` を実行します。新しい Lite 版では、不足するパッケージを GitHub から `Dependencies` フォルダーにダウンロードします。

### Lite の依存関係を手動でインストール

以下の x64 パッケージをインストールします。リンク先は Microsoft のインストーラーです。

- [.NET Runtime 10.0 x64](https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe)
- [ASP.NET Core Runtime 10.0 x64](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)
- [Windows App Runtime 1.8.12 x64](https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe)
- [Microsoft Edge WebView2 Evergreen](https://go.microsoft.com/fwlink/p/?LinkId=2124703)
- [Microsoft Visual C++ Redistributable v14 x64](https://aka.ms/vc14/vc_redist.x64.exe)

起動時に GitHub の安定版を確認します。**設定 → 更新**からも確認できます。Lite の更新には Lite ZIP を使います。フル版から切り替えるには、トレイから終了し、Lite の `SideWidget.exe` を実行します。 共有ランタイムが揃っている場合は、フル版のアップデートにも Lite が選ばれます。

[Everything](../../README.md#everything) · [voidtools x64](https://www.voidtools.com/downloads/)

## プロジェクトのドキュメント

[ビルドとコントリビュート](../../CONTRIBUTING.md) · [技術資料（ロシア語）](technical.ru.md) · [不具合を報告](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [脆弱性を報告](../../SECURITY.md) · [Weblate で翻訳](translating.md)
