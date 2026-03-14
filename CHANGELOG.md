# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.0.1] — 2026-03-14

### Changed
- ログを変化時のみ出力するよう改善（2秒ごとの冗長な垂れ流しを解消）
  - タイトルが変化した時のみ `[INFO] タイトル: "..."` をログ出力
  - 広告検知ログを閾値到達まで（`1/2`, `2/2`）に制限
  - 重複タイトル変化ログを削除

---

## [Unreleased] — 2026-03-13

### Added
- `recorder.csproj` — .NET 10 / self-contained exe プロジェクト定義
- `Program.cs` — 実装スタブ（TODO コメント付きのビルド可能な最小コード）
- `README.md` — セットアップ・ビルド・発行・WASAPI デバイス確認手順
- `renovate.json` — scottlz0310 共有プリセット（C#・automerge・schedule・security）
- `spotify-ad-recorder.slnx` — Visual Studio ソリューションファイル（.slnx 形式）
- `.github/copilot-instructions.md` — AI エージェント向けプロジェクト規約

### Changed
- `.gitignore` — `shared/`（録音出力先）の除外を追加
- `spotify-ad-detector.md` — ffmpeg 停止手順（`q` 送信・5秒タイムアウト後 `Kill`）と publish コマンドの記述を精緻化

---

## [0.0.1] — 2026-03-13

### Added
- `LICENSE` — MIT ライセンス
- `.gitignore` — .NET 標準 gitignore
- `spotify-ad-detector.md` — プロジェクト実装仕様書（Phase 1）
