using System.Diagnostics;

const int PollingIntervalMs = 2000;
const int FfmpegStopTimeoutMs = 5000;
const int AdDetectThreshold = 2;
const string OutputDir = "shared";
const string AdTitle = "Advertisement";

Directory.CreateDirectory(OutputDir);

var state = State.Idle;
var detectCount = 0;
Process? recorder = null;

Console.WriteLine("[spotify-ad-recorder] 起動しました。Ctrl+C で終了。");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (recorder is { HasExited: false })
    {
        Console.WriteLine("[INFO] 終了シグナル受信 — 録音を停止します。");
        StopRecorder(recorder);
    }
    Environment.Exit(0);
};

while (true)
{
    await Task.Delay(PollingIntervalMs);

    var title = GetSpotifyTitle();

    if (title == AdTitle)
    {
        detectCount++;
        Console.WriteLine($"[INFO] Advertisement 検知 ({detectCount}/{AdDetectThreshold})");

        if (state == State.Idle && detectCount >= AdDetectThreshold)
        {
            recorder = StartRecorder();
            state = State.Recording;
        }
    }
    else
    {
        if (detectCount > 0)
            Console.WriteLine($"[INFO] タイトル変化: \"{title}\" — カウントリセット");

        detectCount = 0;

        if (state == State.Recording)
        {
            StopRecorder(recorder!);
            recorder = null;
            state = State.Idle;
        }
    }
}

static string GetSpotifyTitle()
{
    try
    {
        var proc = Process.GetProcessesByName("Spotify").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        return proc?.MainWindowTitle ?? "";
    }
    catch
    {
        return "";
    }
}

static Process StartRecorder()
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    var outputPath = Path.Combine(OutputDir, $"spotify_ad_{timestamp}.wav");

    var psi = new ProcessStartInfo("ffmpeg",
        $"-f wasapi -i loopback \"{outputPath}\"")
    {
        RedirectStandardInput = true,
        UseShellExecute = false,
        RedirectStandardError = true,
    };

    var proc = Process.Start(psi)!;
    Console.WriteLine($"[REC] 録音開始 → {outputPath}");
    return proc;
}

static void StopRecorder(Process proc)
{
    try
    {
        proc.StandardInput.WriteLine("q");
        if (!proc.WaitForExit(FfmpegStopTimeoutMs))
        {
            Console.WriteLine("[WARN] タイムアウト — ffmpeg を強制終了します。");
            proc.Kill();
            proc.WaitForExit();
        }
        Console.WriteLine("[REC] 録音停止");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[ERROR] 録音停止に失敗: {ex.Message}");
    }
}

enum State { Idle, Recording }
