# Tasks

プロジェクトのタスク管理ファイル。完了・追加時は必ず日付を記入してください。

---

## 進行中

_なし_

---

## 未着手

### Phase 1 — 録音機能実装

- [ ] `Program.cs` に本体実装（仕様: `spotify-ad-detector.md`）
  - [ ] 定数定義（ポーリング間隔・出力先ディレクトリ・ffmpeg 引数）
  - [ ] `GetSpotifyTitle()` — `Process.GetProcessesByName("Spotify")` でタイトル取得
  - [ ] ポーリングループ（`Task.Delay(2000)` ベース）
  - [ ] 状態機械（IDLE → DETECTING → RECORDING）
  - [ ] `StartRecording()` — ffmpeg WASAPI ループバック録音開始
  - [ ] `StopRecording()` — 標準入力に `q` 送信、5秒タイムアウト後 `Kill`
  - [ ] `shared/` ディレクトリの自動作成
  - [ ] ファイル名生成（`spotify_ad_yyyy-MM-dd_HH-mm-ss.wav`）
- [ ] 動作確認（Spotify Free アカウントで広告を実際に録音）
- [ ] PR 作成・`main` マージ

### Phase 1 — リリース

- [ ] `v1.0.0` タグ付け
- [ ] GitHub Releases に `recorder.exe`（self-contained）を添付

---

## 完了

### リポジトリ初期整備 — 2026-03-13

- [x] `LICENSE`・`.gitignore` 追加（Initial commit）
- [x] `spotify-ad-detector.md` — 実装仕様書 作成
- [x] `recorder.csproj` 追加（.NET 10 / Exe）
- [x] `Program.cs` スタブ 追加
- [x] `README.md` 追加（セットアップ手順）
- [x] `spotify-ad-recorder.sln` 追加
- [x] `renovate.json` 追加（scottlz0310 共有プリセット）
- [x] `.gitignore` に `shared/` 除外追加
- [x] `.github/copilot-instructions.md` 追加（AI エージェント向け規約）
- [x] PR #2 `feat/repo-scaffold` 作成・プッシュ

---

## 将来フェーズ（スコープ外）

### Phase 2 — `spotify-ad-analyzer`（別リポジトリ）

- [ ] faster-whisper による音声文字起こし
- [ ] pyannote-audio による話者分離
- [ ] resemblyzer による Voice Embedding

### Phase 3 — LLM 分析

- [ ] テキスト変換済みデータの LLM API 送信・分類

### Phase 4 — 広告分析

- [ ] 再生時間帯・広告頻度・パターン分析
