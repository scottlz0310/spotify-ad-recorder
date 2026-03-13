# spotify-ad-recorder

Spotifyデスクトップアプリのウィンドウタイトルを監視し、広告再生中のみシステム音声を録音するツール。

- API・OAuth 認証不要（ウィンドウタイトル監視のみ）
- C# 単一ファイル・約120行の最小実装
- 録音は ffmpeg (WASAPI ループバック) に委譲

詳細仕様は [spotify-ad-detector.md](spotify-ad-detector.md) を参照。

---

## 前提条件

| 要件 | 内容 |
|------|------|
| OS | Windows 11 |
| ランタイム | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |
| ffmpeg | システムの PATH に含まれていること |
| Spotify | デスクトップアプリ（Free アカウント） |

> WSL2・Docker 環境は WASAPI の制約により非対応。

---

## セットアップ

```powershell
# ffmpeg が PATH にあるか確認
ffmpeg -version

# ビルド
dotnet build recorder.csproj

# 実行（Spotify を起動した状態で）
dotnet run --project recorder.csproj
```

---

## 単一 exe として発行

```powershell
dotnet publish --self-contained -p:PublishSingleFile=true -r win-x64 -c Release
```

発行先：`bin\Release\net10.0\win-x64\publish\recorder.exe`

---

## 出力

広告録音ファイルは実行ディレクトリ直下の `shared/` に保存されます。

```
shared/
└── spotify_ad_2026-03-08_21-33-05.wav
```

---

## WASAPI デバイス確認（初回のみ）

```powershell
ffmpeg -f wasapi -list_devices true -i dummy
```

録音には `loopback` デバイスを使用します（コード内定数）。
