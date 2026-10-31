using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

namespace Mandible;

/// Checks GitHub for a newer Mandible release. On Windows it can download the new build, swap it in
/// beside the running game and restart; elsewhere it opens the download page (phones and Macs
/// install apps themselves).
///
/// Uses .NET's HttpClient rather than Godot's TLS: it goes through the operating system's own secure
/// connection stack and certificate store, so it works behind antivirus HTTPS scanning (Avast,
/// Kaspersky, ...) exactly where the player's browser works.
public partial class Updater : Node
{
    public const string Version = "0.5.0";
    const string Api = "https://api.github.com/repos/zaferdajani/Mandible/releases/latest";
    public const string Page = "https://github.com/zaferdajani/Mandible/releases/latest";

    public enum Phase { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }
    public Phase State { get; private set; } = Phase.Idle;
    public string Latest = "", Status = "";
    public event Action Changed;
    string _zipUrl = "", _zipPath = "";
    volatile float _progress = -1;
    static readonly HttpClient Http = MakeClient();

    static HttpClient MakeClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"Mandible-Game/{Version}");
        return c;
    }

    /// The version this copy reports (tests can pretend to be older to exercise the update path).
    static string Current => OS.GetEnvironment("MANDIBLE_FAKE_VERSION") is { Length: > 0 } fake ? fake : Version;
    static bool Debug => OS.GetEnvironment("MANDIBLE_FAKE_VERSION") != "";

    public bool CanInstallInPlace => OS.GetName() == "Windows" && !OS.HasFeature("editor");

    /// Download progress 0..1 (or -1 when unknown).
    public float Progress => State == Phase.Downloading ? _progress : -1;

    public override void _Ready()
    {
        if (Debug) GD.Print($"[updater] {Version} running from {OS.GetExecutablePath()}");
    }

    public void Check()
    {
        if (State is Phase.Checking or Phase.Downloading or Phase.Installing) return;
        Set(Phase.Checking, "Checking for updates...");
        Task.Run(async () =>
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, Api);
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var res = await Http.SendAsync(req);
                res.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                string latest = doc.RootElement.GetProperty("tag_name").GetString()!.TrimStart('v');
                string zip = "";
                foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
                    if (a.GetProperty("name").GetString() == "Mandible-Windows.zip") zip = a.GetProperty("browser_download_url").GetString()!;
                Main(() =>
                {
                    Latest = latest;
                    _zipUrl = zip;
                    if (Newer(Latest, Current)) Set(Phase.Available, $"Update available: {Latest} (you have {Current})");
                    else Set(Phase.UpToDate, $"Mandible {Current} is up to date");
                });
            }
            catch (Exception e)
            {
                if (Debug) GD.Print($"[updater] check failed: {e.Message}");
                Main(() => Set(Phase.Failed, "Could not reach the update server"));
            }
        });
    }

    /// Windows: download and install now. Other systems: open the download page.
    public void Install()
    {
        if (State != Phase.Available) return;
        if (!CanInstallInPlace || _zipUrl == "") { OS.ShellOpen(Page); return; }
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://update"));
        _zipPath = ProjectSettings.GlobalizePath("user://update/Mandible-Windows.zip");
        _progress = 0;
        Set(Phase.Downloading, $"Downloading {Latest}...");
        string url = _zipUrl, path = _zipPath;
        Task.Run(async () =>
        {
            try
            {
                using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                res.EnsureSuccessStatusCode();
                long total = res.Content.Headers.ContentLength ?? -1;
                await using (var src = await res.Content.ReadAsStreamAsync())
                await using (var dst = File.Create(path))
                {
                    var buf = new byte[1 << 16];
                    long got = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, n));
                        got += n;
                        if (total > 0) _progress = got / (float)total;
                    }
                }
                Main(() => { Set(Phase.Installing, "Installing and restarting..."); RunInstaller(); });
            }
            catch (Exception e)
            {
                if (Debug) GD.Print($"[updater] download failed: {e.Message}");
                Main(() => Set(Phase.Failed, "The download failed"));
            }
        });
    }

    /// Runs an action on the game's main thread (UI may only be touched there).
    static void Main(Action a) => Callable.From(a).CallDeferred();

    /// A running .exe cannot overwrite itself, so a small PowerShell script waits for the game to
    /// close, unpacks the new build over the game folder and starts the game again.
    void RunInstaller()
    {
        static string Q(string s) => s.Replace("'", "''"); // PowerShell single-quoted strings
        string exe = Q(OS.GetExecutablePath());
        string dir = Q(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()));
        string script = ProjectSettings.GlobalizePath("user://update/install.ps1");
        string log = Q(ProjectSettings.GlobalizePath("user://update/install.log"));
        string zip = Q(_zipPath);
        // restart with the same launch options the game was started with
        var relaunch = new System.Collections.Generic.List<string>(OS.GetCmdlineArgs());
        // test harnesses can add engine options for the restarted game (for example a silent audio driver)
        relaunch.InsertRange(0, OS.GetEnvironment("MANDIBLE_RELAUNCH_ARGS").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var user = OS.GetCmdlineUserArgs();
        if (user.Length > 0) { relaunch.Add("--"); relaunch.AddRange(user); }
        string argList = relaunch.Count == 0 ? "" : " -ArgumentList @(" + string.Join(", ", relaunch.ConvertAll(a => $"'{Q(a)}'")) + ")";
        File.WriteAllText(script, $@"
$ErrorActionPreference = 'Stop'
'installer started' | Out-File -LiteralPath '{log}'
try {{
  Wait-Process -Id {OS.GetProcessId()} -Timeout 60 -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 1500
  Expand-Archive -LiteralPath '{zip}' -DestinationPath '{dir}' -Force
  Remove-Item -LiteralPath '{zip}' -Force
  'updated to {Latest}' | Out-File -LiteralPath '{log}'
}} catch {{
  ('update failed: ' + $_) | Out-File -LiteralPath '{log}'
}}
$env:MANDIBLE_AUTO_INSTALL = $null
Start-Process -FilePath '{exe}'{argList}
");
        OS.CreateProcess("powershell.exe", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script });
        GetTree().Quit();
    }

    void Set(Phase p, string status)
    {
        State = p;
        Status = status;
        if (Debug) GD.Print($"[updater] {p}: {status}");
        Changed?.Invoke();
    }

    /// True when version a (like 0.5.1) is newer than b.
    public static bool Newer(string a, string b)
    {
        int[] Parts(string v)
        {
            var m = Regex.Matches(v, @"\d+");
            var r = new int[3];
            for (int i = 0; i < Math.Min(3, m.Count); i++) r[i] = int.Parse(m[i].Value);
            return r;
        }
        var x = Parts(a);
        var y = Parts(b);
        for (int i = 0; i < 3; i++) if (x[i] != y[i]) return x[i] > y[i];
        return false;
    }
}
