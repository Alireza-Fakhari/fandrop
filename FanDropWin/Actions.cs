using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace FanDropWin
{
    public enum FileKind { Image, Video, Audio, Pdf, Document, Other }
    public enum FanMode { Convert, Actions, Tools }

    public record FanAction(string Label, string Icon, Func<string[], Task<string>> Run);

    public static class Log
    {
        public static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fandrop.log");
        public static void Write(string msg)
        {
            try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}"); } catch { }
        }
    }

    public static class Actions
    {
        static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".heic", ".ico" };
        static readonly string[] VideoExt = { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".wmv", ".flv" };
        static readonly string[] AudioExt = { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".aac", ".wma", ".opus" };
        static readonly string[] DocExt = { ".txt", ".md", ".rtf", ".doc", ".docx", ".html", ".htm", ".csv", ".json" };

        public static FileKind KindOf(string path)
        {
            var e = Path.GetExtension(path).ToLowerInvariant();
            if (ImageExt.Contains(e)) return FileKind.Image;
            if (VideoExt.Contains(e)) return FileKind.Video;
            if (AudioExt.Contains(e)) return FileKind.Audio;
            if (e == ".pdf") return FileKind.Pdf;
            if (DocExt.Contains(e)) return FileKind.Document;
            return FileKind.Other;
        }

        public static string OutputDir
        {
            get
            {
                var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FanDrop");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static List<FanAction> For(FanMode mode, FileKind kind)
        {
            if (mode == FanMode.Tools) return Tools();
            if (mode == FanMode.Convert)
            {
                switch (kind)
                {
                    case FileKind.Image: return new() { Img("PNG", "png"), Img("JPG", "jpg"), Img("WEBP", "webp"), Img("BMP", "bmp"), Img("ICO", "ico", "-vf scale=256:256:force_original_aspect_ratio=decrease") };
                    case FileKind.Video: return new() { Ff("MP4", "mp4", "-c:v libx264 -pix_fmt yuv420p -c:a aac"), Ff("MOV", "mov", "-c:v libx264 -pix_fmt yuv420p -c:a aac"), Ff("WEBM", "webm", "-c:v libvpx-vp9 -b:v 0 -crf 33 -c:a libopus"), Ff("GIF", "gif", "-vf \"fps=12,scale=480:-1:flags=lanczos\" -an"), Ff("MP3", "mp3", "-vn -c:a libmp3lame -q:a 2") };
                    case FileKind.Audio: return new() { Ff("MP3", "mp3", "-vn -c:a libmp3lame -q:a 2"), Ff("M4A", "m4a", "-vn -c:a aac -b:a 192k"), Ff("WAV", "wav", "-vn"), Ff("FLAC", "flac", "-vn"), Ff("OGG", "ogg", "-vn -c:a libvorbis -q:a 5") };
                    default: return new();
                }
            }
            switch (kind)
            {
                case FileKind.Image: return new() {
                    Ff("Resize 1280", "", "-vf \"scale='min(1280,iw)':-2\"", "resized"),
                    Ff("Compress", "jpg", "-q:v 6", "compressed"),
                    Ff("Rotate", "", "-vf transpose=1", "rotated"),
                    Ff("Gray", "", "-vf format=gray", "gray"),
                    Ff("Strip metadata", "", "-map_metadata -1", "clean") };
                case FileKind.Video: return new() {
                    Ff("Compress", "mp4", "-c:v libx264 -crf 28 -preset faster -c:a aac -b:a 128k", "compressed"),
                    Ff("Mute", "", "-c:v copy -an", "muted"),
                    Ff("Trim 30s", "", "-t 30 -c copy", "trim"),
                    Ff("Frame", "png", "-frames:v 1", "frame"),
                    Ff("Gray", "", "-vf format=gray -c:a copy", "gray") };
                case FileKind.Audio: return new() {
                    Ff("Normalize", "", "-af loudnorm", "normalized"),
                    Ff("Mono", "", "-ac 1", "mono"),
                    Ff("Trim 30s", "", "-t 30", "trim"),
                    Ff("Strip metadata", "", "-map_metadata -1 -c copy", "clean") };
                default: return Tools();
            }
        }

        static List<FanAction> Tools() => new()
        {
            new("Copy path", "📋", files => { System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, files)); return Task.FromResult("Copied path(s) to clipboard"); }),
            new("ZIP", "🗜", files => Task.Run(() => Zip(files))),
            new("Reveal", "📂", files => { Process.Start("explorer.exe", $"/select,\"{files[0]}\""); return Task.FromResult("Revealed"); }),
            new("Open", "↗", files => { foreach (var f in files) Process.Start(new ProcessStartInfo(f) { UseShellExecute = true }); return Task.FromResult("Opened"); }),
            new("Recycle", "🗑", files => { foreach (var f in files) Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); return Task.FromResult("Moved to Recycle Bin"); }),
        };

        static FanAction Img(string label, string ext, string extra = "") => Ff(label, ext, extra);

        // ext == "" keeps the source extension; suffix distinguishes action outputs.
        static FanAction Ff(string label, string ext, string args, string suffix = null) =>
            new(label, "", async files =>
            {
                var ff = FindFfmpeg();
                if (ff == null) return "ffmpeg not found (winget install Gyan.FFmpeg)";
                int ok = 0; string last = null;
                foreach (var f in files)
                {
                    var outExt = ext == "" ? Path.GetExtension(f) : "." + ext;
                    var name = Path.GetFileNameWithoutExtension(f) + (suffix != null ? "-" + suffix : "");
                    var dst = Unique(Path.Combine(OutputDir, name + outExt));
                    var code = await RunProc(ff, $"-hide_banner -loglevel error -y -i \"{f}\" {args} \"{dst}\"");
                    Log.Write($"ffmpeg {label} exit {code}: {f} -> {dst}");
                    if (code == 0) { ok++; last = dst; }
                }
                if (last != null) Process.Start("explorer.exe", $"/select,\"{last}\"");
                return $"{label}: {ok}/{files.Length} done";
            });

        static string Zip(string[] files)
        {
            var baseName = files.Length == 1 ? Path.GetFileNameWithoutExtension(files[0]) : "Archive";
            var dst = Unique(Path.Combine(OutputDir, baseName + ".zip"));
            using (var z = ZipFile.Open(dst, ZipArchiveMode.Create))
                foreach (var f in files)
                {
                    if (Directory.Exists(f))
                        foreach (var sub in Directory.EnumerateFiles(f, "*", SearchOption.AllDirectories))
                            z.CreateEntryFromFile(sub, Path.Combine(Path.GetFileName(f), Path.GetRelativePath(f, sub)));
                    else z.CreateEntryFromFile(f, Path.GetFileName(f));
                }
            Process.Start("explorer.exe", $"/select,\"{dst}\"");
            return "ZIP created";
        }

        static string Unique(string p)
        {
            if (!File.Exists(p)) return p;
            var dir = Path.GetDirectoryName(p); var n = Path.GetFileNameWithoutExtension(p); var e = Path.GetExtension(p);
            for (int i = 2; ; i++) { var c = Path.Combine(dir, $"{n} {i}{e}"); if (!File.Exists(c)) return c; }
        }

        static string FindFfmpeg()
        {
            var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe") };
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                if (d.Length > 0) candidates.Add(Path.Combine(d.Trim(), "ffmpeg.exe"));
            var links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "ffmpeg.exe");
            candidates.Add(links);
            var pkgs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(pkgs))
                candidates.AddRange(Directory.EnumerateDirectories(pkgs, "Gyan.FFmpeg*").SelectMany(d => Directory.EnumerateFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)));
            return candidates.FirstOrDefault(File.Exists);
        }

        static Task<int> RunProc(string exe, string args) => Task.Run(() =>
        {
            var p = Process.Start(new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true });
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (err.Length > 0) Log.Write(err.Trim());
            return p.ExitCode;
        });
    }
}
