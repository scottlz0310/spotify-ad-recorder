using System.Diagnostics;

// User PATH が現在プロセスに反映されていない場合に備えて統合する
Environment.SetEnvironmentVariable("PATH",
    Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + ";" +
    Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));

const int PollingIntervalMs = 2000;
const int FfmpegStopTimeoutMs = 5000;
const int AdDetectThreshold = 2;
const string OutputDir = "shared";
const string LogDir = "logs";
// Spotify のロケールにより広告中タイトルが異なるため複数定義
var adTitles = new HashSet<string>(StringComparer.Ordinal)
{
    "Advertisement",          // 英語ロケール
    "広告ナシで音楽を聴こう。",  // 日本語ロケール
};

Directory.CreateDirectory(OutputDir);
Directory.CreateDirectory(LogDir);
var logFile = Path.Combine(LogDir, $"recorder_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

void Log(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
    Console.WriteLine(line);
    File.AppendAllText(logFile, line + Environment.NewLine);
}

var state = State.Idle;
var detectCount = 0;
Process? recorder = null;

Log("[spotify-ad-recorder] 起動しました。Ctrl+C で終了。");
Log($"[INFO] ログファイル: {logFile}");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (recorder is { HasExited: false })
    {
        Log("[INFO] 終了シグナル受信 — 録音を停止します。");
        StopRecorder(recorder, Log);
    }
    Environment.Exit(0);
};

while (true)
{
    await Task.Delay(PollingIntervalMs);

    var title = GetSpotifyTitle();
    Log($"[DEBUG] タイトル: \"{title}\"");

    if (adTitles.Contains(title))
    {
        detectCount++;
        Log($"[INFO] Advertisement 検知 ({detectCount}/{AdDetectThreshold}) title=\"{title}\"");

        if (state == State.Idle && detectCount >= AdDetectThreshold)
        {
            recorder = StartRecorder(Log);
            state = State.Recording;
        }
    }
    else
    {
        if (detectCount > 0)
            Log($"[INFO] タイトル変化: \"{title}\" — カウントリセット");

        detectCount = 0;

        if (state == State.Recording)
        {
            StopRecorder(recorder!, Log);
            recorder = null;
            state = State.Idle;
        }
    }
}

static string GetSpotifyTitle()
{
    try
    {
        var proc = Process.GetProcessesByName("Spotify")
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle));
        return proc?.MainWindowTitle ?? "";
    }
    catch
    {
        return "";
    }
}

static Process StartRecorder(Action<string> log)
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    var outputPath = Path.Combine(OutputDir, $"spotify_ad_{timestamp}.wav");

    var psi = new ProcessStartInfo("ffmpeg",
        $"-f dshow -i \"audio=CABLE Output (VB-Audio Virtual Cable)\" \"{outputPath}\"")
    {
        RedirectStandardInput = true,
        UseShellExecute = false,
        RedirectStandardError = true,
    };

    var proc = Process.Start(psi)!;
    proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) log($"[FFMPEG] {e.Data}"); };
    proc.BeginErrorReadLine();
    log($"[REC] 録音開始 → {outputPath}");
    return proc;
}

static void StopRecorder(Process proc, Action<string> log)
{
    try
    {
        if (proc.HasExited)
        {
            log("[WARN] ffmpeg はすでに終了しています。");
            log("[REC] 録音停止");
            return;
        }
        proc.StandardInput.WriteLine("q");
        if (!proc.WaitForExit(FfmpegStopTimeoutMs))
        {
            log("[WARN] タイムアウト — ffmpeg を強制終了します。");
            proc.Kill();
            proc.WaitForExit();
        }
        log("[REC] 録音停止");
    }
    catch (Exception ex)
    {
        log($"[ERROR] 録音停止に失敗: {ex.Message}");
    }
}

enum State { Idle, Recording }
