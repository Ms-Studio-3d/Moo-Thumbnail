using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MooThumbnail
{
    internal sealed class AppException : Exception
    {
        internal string Code { get; private set; }
        internal AppException(string code, string message) : base(message) { Code = code; }
    }
    internal sealed class Quality
    {
        internal readonly string Key, Name;
        internal readonly int MinWidth, MinHeight;
        internal Quality(string key, string name, int width, int height)
        { Key = key; Name = name; MinWidth = width; MinHeight = height; }
    }
    internal sealed class Thumbnail
    {
        internal Quality Quality;
        internal string Url, Failure;
        internal byte[] Bytes;
        internal int Width, Height;
        internal bool Available { get { return Bytes != null; } }
        internal string Dimensions { get { return Width + " × " + Height; } }
    }
    internal sealed class LookupResult
    {
        internal string VideoId;
        internal Thumbnail[] Images;
        internal Thumbnail Best { get { return Images.Where(x => x.Available).OrderByDescending(x => (long)x.Width * x.Height).First(); } }
    }
    internal static class Youtube
    {
        internal static readonly Quality[] Qualities = {
            new Quality("maxresdefault", "أعلى جودة", 1280, 720),
            new Quality("hq720", "جودة HD", 1280, 720),
            new Quality("sddefault", "جودة SD", 640, 480),
            new Quality("hqdefault", "جودة عالية", 480, 360),
            new Quality("mqdefault", "جودة متوسطة", 320, 180),
            new Quality("default", "الجودة الأساسية", 120, 90)
        };
        internal static string VideoId(string input)
        {
            if (String.IsNullOrWhiteSpace(input) || input.Length > 4096) throw Invalid();
            string value = input.Trim();
            if (Regex.IsMatch(value, @"^(?:(?:www|m|music)\.)?(?:youtube\.com|youtu\.be)/", RegexOptions.IgnoreCase)) value = "https://" + value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || !String.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) throw Invalid();
            string host = uri.IdnHost.ToLowerInvariant();
            string[] parts = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string id = null;
            if ((host == "youtu.be" || host == "www.youtu.be") && parts.Length == 1) id = parts[0];
            else if (new[] { "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com" }.Contains(host))
            {
                if (uri.AbsolutePath.TrimEnd('/') == "/watch")
                {
                    foreach (string pair in uri.Query.TrimStart('?').Split('&'))
                    {
                        string[] entry = pair.Split(new[] { '=' }, 2);
                        if (Uri.UnescapeDataString(entry[0]) == "v") { id = entry.Length == 2 ? Uri.UnescapeDataString(entry[1]) : ""; break; }
                    }
                }
                else if (parts.Length == 2 && new[] { "shorts", "embed", "live", "v" }.Contains(parts[0])) id = parts[1];
            }
            if (id == null || !Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{11}\z")) throw Invalid();
            return id;
        }
        private static AppException Invalid() { return new AppException("INVALID_URL", "أدخل رابط فيديو YouTube صحيحًا. يدعم البرنامج الفيديوهات وShorts وLive."); }
        internal static string ImageUrl(string id, Quality quality)
        {
            if (!Regex.IsMatch(id ?? "", @"\A[A-Za-z0-9_-]{11}\z") || !Qualities.Contains(quality)) throw Invalid();
            return "https://i.ytimg.com/vi/" + id + "/" + quality.Key + ".jpg";
        }
    }
    internal sealed class ThumbnailService : IDisposable
    {
        internal const int MaxBytes = 8 * 1024 * 1024;
        private readonly HttpClient client;
        internal ThumbnailService(HttpMessageHandler handler = null)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = Math.Max(12, ServicePointManager.DefaultConnectionLimit);
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false });
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg");
        }
        internal async Task<LookupResult> LookupAsync(string input, CancellationToken cancellation)
        {
            string id = Youtube.VideoId(input);
            var tasks = Youtube.Qualities.Select(q => FetchAsync(id, q, cancellation)).ToArray();
            Thumbnail[] images = await Task.WhenAll(tasks).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!images.Any(x => x.Available))
            {
                if (images.Any(x => x.Failure == "network")) throw new AppException("NETWORK", "تعذّر الاتصال بخادم الصور. تحقق من الإنترنت ثم حاول مرة أخرى.");
                throw new AppException("NOT_FOUND", "لم نعثر على صور لهذا الفيديو. قد يكون الرابط غير متاح أو الفيديو محذوفًا.");
            }
            return new LookupResult { VideoId = id, Images = images };
        }
        private async Task<Thumbnail> FetchAsync(string id, Quality quality, CancellationToken cancellation)
        {
            var image = new Thumbnail { Quality = quality, Url = Youtube.ImageUrl(id, quality), Failure = "missing" };
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(12000);
                try
                {
                    using (HttpResponseMessage response = await client.GetAsync(image.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) return image;
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentType == null || response.Content.Headers.ContentType.MediaType != "image/jpeg" || response.Content.Headers.ContentLength > MaxBytes) throw new IOException("Invalid JPEG response");
                        using (timeout.Token.Register(() => response.Dispose()))
                        using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var memory = new MemoryStream())
                        {
                            byte[] block = new byte[16384];
                            int count;
                            while ((count = await stream.ReadAsync(block, 0, block.Length, timeout.Token).ConfigureAwait(false)) != 0)
                            {
                                if (memory.Length + count > MaxBytes) throw new IOException("Image too large");
                                memory.Write(block, 0, count);
                            }
                            byte[] bytes = memory.ToArray();
                            if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216) throw new IOException("Invalid JPEG");
                            memory.Position = 0;
                            using (Image decoded = Image.FromStream(memory, true, true))
                            {
                                if (decoded.RawFormat.Guid != ImageFormat.Jpeg.Guid) throw new IOException("Invalid format");
                                if (decoded.Width < quality.MinWidth || decoded.Height < quality.MinHeight) return image;
                                image.Width = decoded.Width; image.Height = decoded.Height;
                            }
                            image.Bytes = bytes; image.Failure = null;
                        }
                    }
                }
                catch (Exception error)
                {
                    if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
                    if (!(error is HttpRequestException || error is IOException || error is OperationCanceledException ||
                        error is ArgumentException || error is ObjectDisposedException || error is System.Runtime.InteropServices.ExternalException)) throw;
                    image.Failure = "network";
                }
            }
            return image;
        }
        public void Dispose() { client.Dispose(); }
    }
    internal static class ImageFile
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string destination, int flags);
        internal static void Save(string filePath, byte[] bytes)
        {
            string extension = Path.GetExtension(filePath);
            if (!String.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) && !String.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                throw new AppException("FILENAME", "اختر اسم ملف ينتهي بـ .jpg أو .jpeg.");
            string destination = Path.GetFullPath(filePath);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), ".moo-thumbnail-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes, 0, bytes.Length);
                if (!MoveFileEx(temporary, destination, 1 | 8))
                {
                    int code = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    if (code == 5) throw new UnauthorizedAccessException();
                    if (code == 3) throw new DirectoryNotFoundException();
                    throw new IOException(new System.ComponentModel.Win32Exception(code).Message);
                }
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
        }
        internal static string ErrorMessage(Exception error)
        {
            if (error is AppException) return error.Message;
            if (error is UnauthorizedAccessException) return "لا يمكن الكتابة في هذا المجلد. اختر مكانًا آخر للحفظ.";
            if (error is DirectoryNotFoundException) return "مجلد الحفظ غير موجود. اختر مجلدًا آخر.";
            if (error is IOException) return "تعذّر الوصول إلى الملف. تحقق من المساحة وأن الملف غير مستخدم، ثم حاول مرة أخرى.";
            return "حدث خطأ غير متوقع. أعد المحاولة.";
        }
    }
}
