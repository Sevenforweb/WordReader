using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]

namespace QuietReader
{
    sealed class UpdateRelease
    {
        public Version Version { get; set; }
        public string TagName { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseNotes { get; set; }
        public string ReleaseUrl { get; set; }
        public string PackageName { get; set; }
        public string PackageUrl { get; set; }
        public string PackageDigest { get; set; }
        public string ChecksumUrl { get; set; }
        public long PackageSize { get; set; }
    }

    static class UpdateService
    {
        const string LatestReleaseApi = "https://api.github.com/repos/Sevenforweb/WordReader/releases/latest";
        const string GitHubHost = "github.com";
        static readonly HttpClient Client = CreateClient();

        sealed class GitHubRelease
        {
            public string tag_name { get; set; }
            public string name { get; set; }
            public string body { get; set; }
            public string html_url { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public GitHubAsset[] assets { get; set; }
        }

        sealed class GitHubAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
            public string digest { get; set; }
            public long size { get; set; }
        }

        static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WordReader/" + CurrentVersion.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }

        public static Version CurrentVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0); }
        }

        public static async Task<UpdateRelease> CheckLatestAsync()
        {
            string json = await Client.GetStringAsync(LatestReleaseApi);
            GitHubRelease release = new JavaScriptSerializer().Deserialize<GitHubRelease>(json);
            if (release == null || release.draft || release.prerelease) return null;

            Version latestVersion;
            if (!TryParseReleaseVersion(release.tag_name, out latestVersion))
                throw new InvalidDataException("The latest GitHub release has an invalid version tag.");
            if (latestVersion <= CurrentVersion) return null;

            GitHubAsset[] assets = release.assets ?? new GitHubAsset[0];
            GitHubAsset package = assets.FirstOrDefault(asset =>
                asset != null &&
                !String.IsNullOrWhiteSpace(asset.name) &&
                asset.name.StartsWith("WordReader-", StringComparison.OrdinalIgnoreCase) &&
                asset.name.EndsWith("-win-x64-portable.zip", StringComparison.OrdinalIgnoreCase));
            if (package == null || !IsTrustedDownloadUrl(package.browser_download_url))
                throw new InvalidDataException("The latest release does not contain a trusted Windows portable package.");

            GitHubAsset checksum = assets.FirstOrDefault(asset =>
                asset != null &&
                !String.IsNullOrWhiteSpace(asset.name) &&
                asset.name.IndexOf("SHA256", StringComparison.OrdinalIgnoreCase) >= 0 &&
                asset.name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

            return new UpdateRelease
            {
                Version = latestVersion,
                TagName = release.tag_name,
                ReleaseName = release.name,
                ReleaseNotes = release.body,
                ReleaseUrl = release.html_url,
                PackageName = package.name,
                PackageUrl = package.browser_download_url,
                PackageDigest = package.digest,
                ChecksumUrl = checksum != null && IsTrustedDownloadUrl(checksum.browser_download_url)
                    ? checksum.browser_download_url
                    : null,
                PackageSize = package.size
            };
        }

        internal static bool TryParseReleaseVersion(string tag, out Version version)
        {
            version = null;
            if (String.IsNullOrWhiteSpace(tag)) return false;
            Match match = Regex.Match(tag.Trim(), @"^[vV]?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?$");
            if (!match.Success) return false;

            int major;
            int minor;
            int build;
            int revision = 0;
            if (!Int32.TryParse(match.Groups[1].Value, out major) ||
                !Int32.TryParse(match.Groups[2].Value, out minor) ||
                !Int32.TryParse(match.Groups[3].Value, out build) ||
                (match.Groups[4].Success && !Int32.TryParse(match.Groups[4].Value, out revision))) return false;
            try
            {
                version = new Version(major, minor, build, revision);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public static async Task DownloadAndStartUpdaterAsync(UpdateRelease release, IProgress<int> progress)
        {
            if (release == null) throw new ArgumentNullException("release");
            string updateRoot = Path.Combine(Path.GetTempPath(), "WordReader", "updates", release.Version.ToString());
            Directory.CreateDirectory(updateRoot);
            string packagePath = Path.Combine(updateRoot, "update.zip");
            string updaterPath = Path.Combine(updateRoot, "apply-update.ps1");

            await DownloadFileAsync(release.PackageUrl, packagePath, release.PackageSize, progress);
            string expectedHash = await ResolveExpectedHashAsync(release);
            string actualHash = ComputeSha256(packagePath);
            if (!String.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(packagePath);
                throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
            }

            File.WriteAllText(updaterPath, BuildUpdaterScript(), new UTF8Encoding(false));
            StartUpdater(updaterPath, packagePath, updateRoot);
        }

        static async Task DownloadFileAsync(string url, string destination, long expectedSize, IProgress<int> progress)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            using (HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? expectedSize;
                using (Stream input = await response.Content.ReadAsStreamAsync())
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        received += read;
                        if (progress != null && total > 0)
                            progress.Report((int)Math.Max(0, Math.Min(100, received * 100L / total)));
                    }
                }
            }
            if (progress != null) progress.Report(100);
        }

        static async Task<string> ResolveExpectedHashAsync(UpdateRelease release)
        {
            Match digest = Regex.Match(release.PackageDigest ?? String.Empty, @"^sha256:([0-9a-fA-F]{64})$");
            if (digest.Success) return digest.Groups[1].Value;
            if (String.IsNullOrWhiteSpace(release.ChecksumUrl))
                throw new InvalidDataException("The release does not provide a SHA-256 checksum.");

            string checksumText = await Client.GetStringAsync(release.ChecksumUrl);
            Match checksum = Regex.Match(checksumText ?? String.Empty, @"(?im)^([0-9a-f]{64})\s+\*?" + Regex.Escape(release.PackageName) + @"\s*$");
            if (!checksum.Success)
                throw new InvalidDataException("The release checksum file does not match the portable package.");
            return checksum.Groups[1].Value;
        }

        static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", String.Empty);
            }
        }

        static bool IsTrustedDownloadUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                String.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase);
        }

        static void StartUpdater(string updaterPath, string packagePath, string updateRoot)
        {
            string installRoot = GetInstallRoot();
            string executablePath = Process.GetCurrentProcess().MainModule.FileName;
            string executableRelativePath = executablePath.Substring(installRoot.TrimEnd(Path.DirectorySeparatorChar).Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = powershell,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(updaterPath) +
                    " -PackagePath " + QuoteArgument(packagePath) +
                    " -UpdateRoot " + QuoteArgument(updateRoot) +
                    " -InstallRoot " + QuoteArgument(installRoot) +
                    " -ExecutableRelativePath " + QuoteArgument(executableRelativePath) +
                    " -ProcessId " + Process.GetCurrentProcess().Id,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = updateRoot
            };
            Process.Start(startInfo);
        }

        static string GetInstallRoot()
        {
            DirectoryInfo baseDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (baseDirectory.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                baseDirectory.Parent != null &&
                File.Exists(Path.Combine(baseDirectory.Parent.FullName, "run.ps1"))) return baseDirectory.Parent.FullName;
            return baseDirectory.FullName;
        }

        static string QuoteArgument(string value)
        {
            return "\"" + (value ?? String.Empty).Replace("\"", "\\\"") + "\"";
        }

        static string BuildUpdaterScript()
        {
            return @"param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$UpdateRoot,
    [Parameter(Mandatory=$true)][string]$InstallRoot,
    [Parameter(Mandatory=$true)][string]$ExecutableRelativePath,
    [Parameter(Mandatory=$true)][int]$ProcessId
)
$ErrorActionPreference = 'Stop'
$createdFiles = New-Object System.Collections.Generic.List[string]
$backupRoot = Join-Path $UpdateRoot 'backup'
$stageRoot = Join-Path $UpdateRoot 'stage'

function Get-RelativePath([string]$Root, [string]$Path) {
    return $Path.Substring($Root.TrimEnd('\').Length).TrimStart([char[]]@('\','/'))
}

function Restore-Backup {
    if (Test-Path -LiteralPath $backupRoot) {
        foreach ($file in Get-ChildItem -LiteralPath $backupRoot -Recurse -File) {
            $relative = Get-RelativePath $backupRoot $file.FullName
            $target = Join-Path $InstallRoot $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    for ($index = $createdFiles.Count - 1; $index -ge 0; $index--) {
        if (Test-Path -LiteralPath $createdFiles[$index]) {
            Remove-Item -LiteralPath $createdFiles[$index] -Force -ErrorAction SilentlyContinue
        }
    }
}

try {
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedUpdateRoot = [IO.Path]::GetFullPath($UpdateRoot)
    $resolvedInstallRoot = [IO.Path]::GetFullPath($InstallRoot)
    if (-not $resolvedUpdateRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The update workspace is outside the temporary directory.'
    }
    if ($resolvedInstallRoot -eq [IO.Path]::GetPathRoot($resolvedInstallRoot) -or
        -not (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot $ExecutableRelativePath))) {
        throw 'The WordReader installation directory is invalid.'
    }

    $running = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($running) { $running | Wait-Process -Timeout 120 }
    if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        throw 'WordReader did not close before the update timeout.'
    }

    foreach ($path in @($stageRoot, $backupRoot)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $stageRoot)

    $payloadRoot = $stageRoot
    if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'bin\QuietReader.exe'))) {
        $candidates = @(Get-ChildItem -LiteralPath $stageRoot -Directory | Where-Object {
            Test-Path -LiteralPath (Join-Path $_.FullName 'bin\QuietReader.exe')
        })
        if ($candidates.Count -ne 1) { throw 'The update package has an unexpected directory layout.' }
        $payloadRoot = $candidates[0].FullName
    }

    foreach ($file in Get-ChildItem -LiteralPath $payloadRoot -Recurse -File) {
        $relative = Get-RelativePath $payloadRoot $file.FullName
        if ($relative -eq '.aioa\hosting.json' -or $relative.StartsWith('.git\')) { continue }
        $target = Join-Path $resolvedInstallRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        if (Test-Path -LiteralPath $target) {
            $backup = Join-Path $backupRoot $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
            Copy-Item -LiteralPath $target -Destination $backup -Force
        } else {
            $createdFiles.Add($target)
        }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }

    $restartPath = Join-Path $resolvedInstallRoot $ExecutableRelativePath
    if (-not (Test-Path -LiteralPath $restartPath)) { throw 'The updated executable is missing.' }
    Start-Process -FilePath $restartPath -WorkingDirectory $resolvedInstallRoot
}
catch {
    try { Restore-Backup } catch {}
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show(
        ('WordReader update failed.' + [Environment]::NewLine + [Environment]::NewLine + $_.Exception.Message),
        'WordReader Update',
        [Windows.Forms.MessageBoxButtons]::OK,
        [Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}
";
        }
    }

    sealed class UpdatePromptForm : Form
    {
        readonly UpdateRelease release;
        readonly bool chinese;
        readonly Label statusLabel = new Label();
        readonly ProgressBar progressBar = new ProgressBar();
        readonly Button updateButton = new Button();
        readonly Button laterButton = new Button();

        public UpdatePromptForm(UpdateRelease release, bool chinese)
        {
            this.release = release;
            this.chinese = chinese;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            ClientSize = new Size(520, 382);
            Font = new Font("Microsoft YaHei", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = chinese ? "WordReader 更新" : "WordReader Update";

            Label title = new Label
            {
                AutoSize = false,
                Location = new Point(28, 24),
                Size = new Size(464, 31),
                Font = new Font("Microsoft YaHei", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 31, 31),
                Text = chinese ? "发现新版本 " + FormatVersion(release.Version) : "Version " + FormatVersion(release.Version) + " is available"
            };
            Label summary = new Label
            {
                AutoSize = false,
                Location = new Point(30, 62),
                Size = new Size(460, 42),
                ForeColor = Color.FromArgb(80, 80, 80),
                Text = chinese
                    ? "当前版本 " + FormatVersion(UpdateService.CurrentVersion) + "。更新为可选操作，下载并校验完成后软件会自动重启。"
                    : "Current version: " + FormatVersion(UpdateService.CurrentVersion) + ". Updating is optional; WordReader restarts after a verified download."
            };
            TextBox notes = new TextBox
            {
                Location = new Point(30, 112),
                Size = new Size(460, 164),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(248, 248, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Text = String.IsNullOrWhiteSpace(release.ReleaseNotes)
                    ? (chinese ? "此版本未提供更新说明。" : "No release notes were provided.")
                    : release.ReleaseNotes
            };

            LinkLabel releaseLink = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(30, 287),
                Text = chinese ? "查看 GitHub 发布页面" : "View release on GitHub"
            };
            releaseLink.LinkClicked += delegate
            {
                try { Process.Start(release.ReleaseUrl); }
                catch { }
            };

            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(30, 312);
            statusLabel.Size = new Size(280, 24);
            statusLabel.ForeColor = Color.FromArgb(80, 80, 80);
            progressBar.Location = new Point(30, 337);
            progressBar.Size = new Size(280, 8);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Visible = false;

            laterButton.Location = new Point(320, 322);
            laterButton.Size = new Size(80, 32);
            laterButton.Text = chinese ? "稍后" : "Later";
            laterButton.DialogResult = DialogResult.Cancel;
            updateButton.Location = new Point(410, 322);
            updateButton.Size = new Size(80, 32);
            updateButton.BackColor = Color.FromArgb(43, 87, 154);
            updateButton.FlatStyle = FlatStyle.Flat;
            updateButton.FlatAppearance.BorderSize = 0;
            updateButton.ForeColor = Color.White;
            updateButton.Text = chinese ? "更新" : "Update";
            updateButton.Click += async delegate { await BeginUpdateAsync(); };

            Controls.Add(title);
            Controls.Add(summary);
            Controls.Add(notes);
            Controls.Add(releaseLink);
            Controls.Add(statusLabel);
            Controls.Add(progressBar);
            Controls.Add(laterButton);
            Controls.Add(updateButton);
            AcceptButton = updateButton;
            CancelButton = laterButton;
            Shown += delegate
            {
                notes.SelectionStart = 0;
                notes.SelectionLength = 0;
                updateButton.Focus();
            };
        }

        async Task BeginUpdateAsync()
        {
            updateButton.Enabled = false;
            laterButton.Enabled = false;
            ControlBox = false;
            progressBar.Visible = true;
            statusLabel.Text = chinese ? "正在下载更新（0%）" : "Downloading update (0%)";
            try
            {
                Progress<int> progress = new Progress<int>(value =>
                {
                    progressBar.Value = Math.Max(0, Math.Min(100, value));
                    statusLabel.Text = chinese ? "正在下载更新（" + value + "%）" : "Downloading update (" + value + "%)";
                });
                await UpdateService.DownloadAndStartUpdaterAsync(release, progress);
                statusLabel.Text = chinese ? "校验完成，正在重启……" : "Verified. Restarting...";
                DialogResult = DialogResult.OK;
                Close();
                Application.Exit();
            }
            catch (Exception error)
            {
                progressBar.Visible = false;
                statusLabel.Text = chinese ? "更新未完成。" : "The update was not completed.";
                updateButton.Enabled = true;
                laterButton.Enabled = true;
                ControlBox = true;
                MessageBox.Show(this,
                    (chinese ? "无法完成更新。\n\n" : "The update could not be completed.\n\n") + error.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static string FormatVersion(Version version)
        {
            if (version == null) return "?";
            return version.Revision > 0 ? version.ToString(4) : version.ToString(3);
        }
    }
}
