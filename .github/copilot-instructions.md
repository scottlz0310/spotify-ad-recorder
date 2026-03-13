# Copilot Instructions — spotify-ad-recorder

## プロジェクト概要

Spotify デスクトップアプリのウィンドウタイトルを監視し、広告再生中のみシステム音声を録音する Windows ネイティブ C# ツール。

- 詳細仕様: [`spotify-ad-detector.md`](../spotify-ad-detector.md)
- API・OAuth 認証不要（ウィンドウタイトル監視のみ）
- 実行環境: Windows 11 ネイティブのみ（WSL2・Docker 非対応）

---

## ビルド・実行

```powershell
# ビルド
dotnet build recorder.csproj

# 実行（Spotify デスクトップアプリを起動した状態で）
dotnet run --project recorder.csproj

# 単一 exe として発行
dotnet publish recorder.csproj --self-contained -p:PublishSingleFile=true -r win-x64 -c Release
```

> **前提**: ffmpeg がシステムの PATH に含まれていること。

---

## アーキテクチャ

```
Spotify Desktop
    │ ウィンドウタイトル（Windows API）
    ▼
Program.cs（ポーリングループ・状態機械）
    │ ffmpeg プロセス制御
    ▼
ffmpeg（WASAPI ループバック録音）
    │
    ▼
shared/spotify_ad_<timestamp>.wav
```

### 状態機械

| 状態 | タイトル | 動作 |
|------|---------|------|
| IDLE | `Advertisement`（2回連続） | 録音開始 → RECORDING |
| RECORDING | `Advertisement` 以外 | 録音停止 → IDLE |

---

## 実装規約（厳守）

### やること

- `Program.cs` **単一ファイル**で実装（C# top-level statements）
- 外部 NuGet パッケージは**使用しない**（標準ライブラリのみ）
- ffmpeg 停止は標準入力に `q` を送り正常終了させる。5 秒タイムアウト後のみ `Kill`
- Spotify ウィンドウが見つからない場合はエラーにせずスキップして継続

### やらないこと

- Spotify Web API・OAuth 認証
- クラスファイルの分割
- GUI
- テストコード
- 設定ファイル読み込み（定数で管理）
- データベース保存

---

## キーポイント

| 項目 | 値 |
|------|---|
| ポーリング間隔 | 2 秒 |
| 広告判定 | `title == "Advertisement"` |
| 誤検知対策 | 2 回連続検知で録音開始 |
| ファイル名形式 | `spotify_ad_2026-03-08_21-33-05.wav` |
| 出力先 | `shared/`（`.gitignore` 除外済み） |
| 想定行数 | 約 110〜120 行 |

---

## Git ワークフロー

- `main` ブランチへの直接プッシュは行わない
- 作業は `feat/<topic>` ブランチで行い PR を作成する
- 依存関係の更新は Renovate が自動 PR を作成する（`renovate.json` 参照）
