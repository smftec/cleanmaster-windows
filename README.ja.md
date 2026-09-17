<div align="center">

# 🧹 CleanMaster（清理最適化マスター）

**安全・透明・復元可能な Windows クリーンアップ＆システム最適化ツール**

[![Release](https://img.shields.io/github/v/release/smftec/cleanmaster-windows)](../../releases/latest)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Windows-10%2022H2%20%2F%2011-0078D4.svg)](#-クイックスタート)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](#-クイックスタート)

[简体中文](README.md) | [English](README.en.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md)

**「クリーンは明快に、最適化は安心に」** —— すべて説明可能・プレビュー可能・復元可能。偽りの高速化はいたしません。

[⬇️ ダウンロード](../../releases/latest) · [🛠 ソースからビルド](#-クイックスタート) · [💬 お問い合わせ](#-お問い合わせ)

</div>

---

## ✨ スクリーンショット

| ライトテーマ | ダークテーマ |
|---|---|
| ![ライト](docs/images/home-light.png) | ![ダーク](docs/images/home-dark.png) |

## ⬇️ ダウンロード

[Releases](../../releases/latest) ページから入手できます（SHA256 チェックサムは `SHA256SUMS.txt` を参照）：

| ファイル | 対象 |
|---|---|
| `CleanMasterSetup-x.x.x.exe` | ほとんどのユーザー：日本語/中国語インストーラー、アンインストール対応 |
| `CleanMaster-x.x.x-win-x64.zip` | ポータブル版：解凍してすぐ使用可能（.NET ランタイム同梱） |

## 🧰 機能一覧

| モジュール | 機能 |
|---|---|
| **ホーム** | デバイス状態モデル（良好 / 要クリーンアップ / 空き容量不足）、ジャンク＆スタートアップ概観、ディスク使用状況、最近のアクティビティ、ワンクリック候補クリーンアップ |
| **スマートスキャン** | システムジャンク / ブラウザキャッシュ / アプリキャッシュ / ごみ箱を一括スキャン。リスク別にグループ化し、ファイル単位でプレビュー可能 |
| **クリーンアップ** | システムジャンク（テンポラリ、クラッシュダンプ、サムネイル、シェーダーキャッシュ、更新キャッシュ等）、アプリキャッシュ（WeChat / QQ / Discord / Teams / Steam / Epic / VS Code / JetBrains など 13 種以上）、ブラウザ（Edge / Chrome / Brave / Opera / Firefox）、プライバシー痕跡（デフォルト未選択）、ごみ箱 |
| **ディスク解析** | マルチスレッド全ディスク走査、カテゴリ別統計、フォルダ使用量ツリー、大容量ファイル検索（場所を開く / ごみ箱へ / ホワイトリスト） |
| **スタートアップ** | レジストリ Run / スタートアップフォルダ / ログオン時タスクの 3 系統。タスクマネージャーと互換性のある有効/無効化、すべての変更を復元可能 |
| **アプリ管理** | アンインストール情報の完全列挙、アプリ自身のアンインストーラーを起動、アンインストール後の残存ファイル検出（隔離され復元可能） |
| **ツールボックス** | 隔離区管理、ファイルシュレッダー、重複ファイル検出（内容ハッシュベース、ファイル名照合ではありません）、クリーンアップ履歴、Windows 公式ツールへのショートカット |
| **システム統合** | ライト / ダーク / システム追従テーマ、システムトレイ、初回起動ウィザード、自動クリーンアップ予約 |

## 🔒 安全性の設計（製品の中核）

- **統一削除ガード**：すべての削除は `SafetyGuard` の検証を通過。デスクトップ、ドキュメント、ピクチャ、ダウンロード、System32、WinSxS、Program Files などはハードコードされた拒否リストに入っており、どのルールも触れません
- **4 段階リスク**：安全 / 低リスクのみ既定で選択。ごみ箱・更新キャッシュ・プライバシー項目は自動選択されません
- **復元可能な隔離区**：キャッシュ以外のファイルは先に隔離区へ移動（既定 30 日保持）。いつでも復元でき、同名競合も自動処理
- **ジャンクション非追従**：走査・削除ともに Junction / シンボリックリンクを辿りません
- **説明可能性**：各項目に「なぜ削除できるか」「削除による影響」「復元可否」「どのプロセスが使用中か」を明記
- **自動安全セルフテスト**：`CleanMaster.exe --selftest` が 25 項目の安全アサーションを実行し、CI のリリースゲートとして機能

## 🚀 クイックスタート

必要環境：Windows 10 22H2+ / Windows 11、[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（サードパーティ NuGet 依存ゼロ）。

```bash
git clone https://github.com/smftec/cleanmaster-windows.git
cd cleanmaster-windows
dotnet build src/CleanMaster.App/CleanMaster.App.csproj -c Release
# 起動
src/CleanMaster.App/bin/Release/net8.0-windows/CleanMaster.exe
# セルフテスト
src/CleanMaster.App/bin/Release/net8.0-windows/CleanMaster.exe --selftest
```

`v*` タグを push すると、GitHub Actions が自動で：ビルド → セルフテスト → パブリッシュ → インストーラー作成 → チェックサム生成 → Release ドラフト作成まで実行します。

## 📦 パッケージング

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
```

ポータブルフォルダ、zip、中国語インストーラー（[Inno Setup](https://jrsoftware.org/isinfo.php) を自動検出、スクリプトは [scripts/setup.iss](scripts/setup.iss)）、`SHA256SUMS.txt` を出力します。

コード署名証明書をお持ちの場合、環境変数 2 つで署名済みアーティファクト一式を出力できます：

```powershell
$env:CM_SIGN = "1"; $env:CM_SIGN_SUBJECT = "CN=Your Name"
powershell -File scripts\publish.ps1
```

## 🗺️ ロードマップ

- [ ] SignPath Foundation によるオープンソース署名（申請準備中）
- [ ] 署名検証付きオンラインクリーニングルール更新
- [ ] 実測ベースの起動時間分析（ETW）
- [ ] Microsoft Store での配布

## 💬 お問い合わせ

- 🌐 ウェブサイト：<https://52xn.com>
- 📮 メール：<dev@52xn.com>
- 💚 WeChat 公式アカウント：WeChat で「**AI虚拟助理**」を検索

<div align="center">
  <img src="docs/images/wechat-search.png" alt="WeChat検索：AI虚拟助理" width="640"/>
</div>

## 📄 ライセンス

本プロジェクトは **GPL-3.0** で公開しています（[LICENSE](LICENSE) 参照）：自由な利用・改変・再配布が可能ですが、派生作品も同じくオープンソースにする必要があります。商用ライセンス・クローズドソースでの利用は <dev@52xn.com> までご相談ください。

リリース用バイナリのコード署名は、[SignPath Foundation](https://signpath.org) のオープンソース向け無料署名（統合準備中）を利用予定です。
