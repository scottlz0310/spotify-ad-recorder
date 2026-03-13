# Spotify広告検知・録音ツール 仕様書

## 1. 概要・目的

Spotify再生中に流れる広告を自動検知し、音声ファイルとして録音・保存するツール。

主な用途は以下のとおりです。

- 広告音声の収集
- ナレーター特定のための声紋分析（将来フェーズ）
- 文字起こしによる広告スクリプト検索（将来フェーズ）

本仕様書はPhase 1（録音機能のみ）の実装を対象とします。

---

## 2. リポジトリ構成

システムは2つのリポジトリに分割します。

| リポジトリ | 言語・環境 | 役割 |
|-----------|----------|------|
| `spotify-ad-recorder` | C# / .NET（Windows） | 広告検知・録音 |
| `spotify-ad-analyzer` | Python / Docker（Linux） | 文字起こし・声紋分析 |

**連携インターフェース**：共有ディレクトリ上の `.wav` ファイルのみ。
ファイル命名規則が両リポジトリ間の唯一の契約です。

```
spotify-ad-recorder  →  shared/  →  spotify-ad-analyzer
（.wavを書き込む）     （.wavファイル群）  （ディレクトリを監視・処理）
```

---

## 3. 動作環境・前提条件

### spotify-ad-recorder（本仕様書の対象）

| 項目 | 内容 |
|------|------|
| OS | Windows 11 ネイティブ |
| 言語 | C# / .NET 10 |
| 外部依存 | ffmpeg（システムPATHに存在すること）、VB-Cable（およびWindows音声設定でCABLE Inputをデフォルト出力に設定済み） |
| Spotify | Freeアカウント（広告が流れること） |
| 非対応環境 | WSL2・Docker（Windows APIの制約により不可） |

Spotify Web API は使用しません。
アカウント認証・APIキー・OAuth設定は一切不要です。

### spotify-ad-analyzer（参考）

| 項目 | 内容 |
|------|------|
| 環境 | Docker（Linux コンテナ） |
| 言語 | Python |
| 入力 | 共有ディレクトリ上の `.wav` ファイル |

---

## 4. システム構成

```
Spotify Desktop Client
        │
        │ ウィンドウタイトル（Windows API）
        ▼
spotify-ad-recorder（C# 常駐サービス）
        │
        ├── 広告検知 → 録音開始トリガ
        └── 通常再生検知 → 録音停止トリガ
                │
                ▼
        ffmpeg（dshow ループバック via VB-Cable）
                │
                ▼
        録音ファイル（shared/spotify_ad_*.wav）
                │
                ▼
        spotify-ad-analyzer（Python / Docker）
        （ディレクトリ監視 → 文字起こし・声紋分析）
```

---

## 5. コンポーネント

### 5.1 Spotifyクライアント

Spotifyデスクトップアプリ（Windows版）。

検知はウィンドウタイトルの読み取りのみのため、ログイン状態を問わず動作します（ただし広告はFreeアカウントのみ）。

### 5.2 広告検知サービス（Ad Detector）

**役割**

1. Spotifyウィンドウタイトルを定期取得（`System.Diagnostics.Process` 経由）
2. 広告再生を検知
3. ffmpegプロセスを制御

**実装言語：C# / .NET 10**

理由：
- `Process.GetProcessesByName` でウィンドウタイトルを1行取得
- Windows機能との統合が自然
- `dotnet publish recorder.csproj --self-contained -p:PublishSingleFile=true -r win-x64 -c Release` で単一exe配布可能

### 5.3 録音モジュール

実際の音声録音はffmpegに委譲します。

**ツール：ffmpeg**

理由：
- dshow （DirectShow）でシステム音声をキャプチャ（全公開 ffmpeg ビルドは WASAPI 非対応のため）
- VB-Cable ・ CABLE Output デバイスを入力に指定
- CLIで制御可能・自動化に向く

---

## 6. 要件

### 機能要件

1. Spotifyウィンドウタイトルを定期的に取得できること
2. 広告再生を確実に検知できること
3. 広告の開始・終了に連動して録音を開始・停止できること
4. 広告1件につき1つの音声ファイルを生成すること

### 非機能要件

- 常駐プロセスとして継続稼働できること
- Spotifyが起動していない場合も安全に動作すること（エラーなくスキップ）
- 録音ファイルはタイムスタンプ付きのファイル名で保存されること

---

## 7. 広告検知の仕組み

### 検知方式

`System.Diagnostics.Process` でSpotifyのウィンドウタイトルを取得します。

```csharp
var proc = Process.GetProcessesByName("Spotify").FirstOrDefault();
var title = proc?.MainWindowTitle ?? "";
```

### ウィンドウタイトルの変化

| 再生状態 | ウィンドウタイトル例 |
|---------|-------------------|
| 楽曲再生中 | `曲名 - アーティスト名` |
| 広告再生中 | `Advertisement` |
| 停止・非アクティブ | `Spotify` |

### 広告判定条件

```csharp
title == "Advertisement"
```

---

## 8. 設計

### 8.1 状態機械

| 現在状態 | ウィンドウタイトル | 動作 |
|---------|-----------------|------|
| IDLE | `Advertisement` | 録音開始 → RECORDING へ遷移 |
| IDLE | その他 | 何もしない |
| RECORDING | `Advertisement` | 録音継続 |
| RECORDING | `Advertisement` 以外 | 録音停止 → IDLE へ遷移 |

### 8.2 誤検知対策

**連続2回 `Advertisement` を検知した場合のみ録音開始**します。

```
ポーリング1回目: Advertisement検知 → DETECTING状態へ
ポーリング2回目: Advertisement継続 → 録音開始・RECORDING状態へ
ポーリング2回目: それ以外        → IDLE状態へ戻る（誤検知として破棄）
```

### 8.3 疑似コード

```csharp
var state = State.Idle;
var detectCount = 0;
Process? recorder = null;

while (true)
{
    await Task.Delay(2000);

    var title = GetSpotifyTitle(); // "" if not running

    if (title == "Advertisement")
    {
        detectCount++;
        if (state == State.Idle && detectCount >= 2)
        {
            recorder = StartRecorder();
            state = State.Recording;
        }
    }
    else
    {
        detectCount = 0;
        if (state == State.Recording)
        {
            StopRecorder(recorder);
            state = State.Idle;
        }
    }
}
```

### 8.4 録音方式

ffmpegの dshow（DirectShow）で VB-Cable 経由のシステム音声をキャプチャします。

> **前提**: VB-Cable（https://vb-audio.com/Cable/）をインストールし、Windows のサウンド設定で「CABLE Input (VB-Audio Virtual Cable)」をデフォルト出力デバイスに設定すること。

```bash
# 録音開始
ffmpeg -f dshow -i "audio=CABLE Output (VB-Audio Virtual Cable)" spotify_ad_<timestamp>.wav

# デバイス一覧確認（初期設定時）
ffmpeg -f dshow -list_devices true -i ""
```

録音停止はffmpegの標準入力に `q` を送ることで**正常終了**させます。
`WaitForExit` で一定時間（目安：5秒）待機し、タイムアウト後にのみ `Kill` で強制終了します。
これによりWAVファイルヘッダが正常に書き込まれ、ファイル破損を防ぎます。

> **ffmpegプロセス起動時に必須の設定**：`StandardInput` へのアクセスには `ProcessStartInfo` で
> `RedirectStandardInput = true` および `UseShellExecute = false` を設定する必要があります。

```csharp
// プロセス起動設定（必須）
var psi = new ProcessStartInfo("ffmpeg", /* args */)
{
    RedirectStandardInput = true,
    UseShellExecute = false,
};
var recorder = Process.Start(psi)!;

// 録音停止
recorder.StandardInput.WriteLine("q");
if (!recorder.WaitForExit(5000))
{
    recorder.Kill();
    recorder.WaitForExit(); // Kill 後も確実に終了を待つ
}
```

### 8.5 ポーリング間隔

推奨値：**2秒**

| 間隔 | メリット | デメリット |
|------|---------|-----------|
| 1秒 | 検知が速い | CPU負荷やや増 |
| 2秒 | バランス良い | わずかな遅延 |
| 5秒 | 負荷低 | 短い広告を逃す可能性 |

Spotify広告は通常15〜30秒のため、2秒ポーリングで十分です。
ウィンドウタイトル取得はAPIと異なりレート制限がないため、間隔を短くしても安全です。

### 8.6 ファイル名形式

```
spotify_ad_2026-03-08_21-33-05.wav
```

---

## 9. 実装制約

AIエージェントが実装する際は以下の制約を厳守してください。

### やること

- `Program.cs` 単一ファイルで実装する（C# top-level statements）
- 外部NuGetパッケージは使用しない（標準ライブラリのみ）
- ffmpeg停止時は標準入力に `q` を送り正常終了させる（タイムアウト後のみ `Kill`）
- Spotifyウィンドウが見つからない場合はスキップして継続する

### やらないこと

- Spotify Web API・OAuth認証は実装しない
- クラスファイルの分割はしない
- GUIは実装しない
- テストコードは生成しない
- 設定ファイルの読み込み機能は実装しない（定数で管理）
- データベースへの保存は実装しない

---

## 10. 期待するファイル構成

```
spotify-ad-recorder/
├── Program.cs        （唯一の実装ファイル）
├── recorder.csproj
└── README.md         （セットアップ手順のみ、簡潔に）
```

---

## 11. 想定コード量

| モジュール | 行数目安 |
|-----------|---------|
| ウィンドウタイトル取得 | 約10行 |
| ポーリングループ・状態機械 | 約50行 |
| ffmpegプロセス制御 | 約30行 |
| ファイル名生成・定数定義 | 約20行 |
| **合計** | **約110〜120行** |

---

## 12. 将来フェーズ（Phase 1スコープ外）

以下はPhase 1では実装しません。

### Phase 2（分析パイプライン）― `spotify-ad-analyzer` リポジトリ

| 機能 | ツール |
|------|--------|
| 音声文字起こし | faster-whisper（ローカル実行） |
| 声紋・話者分離 | pyannote-audio 3.x |
| Voice Embedding | resemblyzer |

実装：Python / Docker（Linux）。共有ディレクトリを `watchdog` 等で監視し、新しい `.wav` を自動処理。

### Phase 3（LLM分析）

Phase 2で生成したテキストデータをLLMに渡して分析・分類。
音声は直接LLMに渡さず、**テキスト変換後のデータのみをAPIに送信**することでコストを最小化します。

### Phase 4（広告分析）

収集した広告データから以下の分析が可能：

- 再生時間帯
- ユーザー属性との相関
- 広告頻度・パターン

---

## 13. まとめ

Spotifyデスクトップアプリのウィンドウタイトルを監視し、広告が流れたときだけ音声を録音するシステムです。

**設計の特徴**

- API・認証不要（ウィンドウタイトル監視のみ）
- C# 1ファイル・約120行の最小実装
- 録音処理の外部ツール（ffmpeg）への委譲
- `.wav` ファイルを境界として `spotify-ad-analyzer`（Python/Docker）と疎結合

**リポジトリ分割の根拠**

両コンポーネントは実行環境・言語・ライフサイクルがすべて異なり、共有するのはファイル命名規則のみです。分割することで各リポジトリが独立して開発・デプロイできます。
