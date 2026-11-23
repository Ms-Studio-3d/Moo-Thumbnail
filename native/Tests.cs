using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MooThumbnail
{
    internal sealed class FakeHandler : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, HttpResponseMessage> Respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(Respond(request)); }
    }
    internal sealed class TestDesktop : IDesktopActions
    {
        internal string Destination, Copied, Opened;
        public string ChooseSavePath(IWin32Window owner, string name) { return Destination; }
        public void Copy(string text) { Copied = text; }
        public void OpenFolder(string file) { Opened = file; }
    }
    internal static class Tests
    {
        private static int count;
        private static readonly string id = "R97EoVD1nr4";
        private static string root;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("FAIL: " + message); count++; Console.WriteLine("PASS " + message); }
        private static byte[] Jpeg(int width, int height)
        {
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var memory = new MemoryStream())
            { graphics.Clear(Theme.Orange); bitmap.Save(memory, ImageFormat.Jpeg); return memory.ToArray(); }
        }
        private static HttpResponseMessage Response(byte[] bytes, string type = "image/jpeg")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type); return response;
        }
        private static FakeHandler ValidHandler()
        {
            return new FakeHandler { Respond = request => {
                Quality quality = Youtube.Qualities.Single(q => request.RequestUri.AbsolutePath.EndsWith("/" + q.Key + ".jpg"));
                return Response(Jpeg(quality.MinWidth, quality.MinHeight));
            } };
        }
        private static async Task ErrorCode(ThumbnailService service, string code)
        {
            try { await service.LookupAsync("https://youtu.be/" + id, CancellationToken.None); throw new Exception("Expected " + code); }
            catch (AppException error) { Check(error.Code == code, code + " reported clearly"); }
        }
        private static async Task CoreAsync()
        {
            foreach (string url in new[] { "https://www.youtube.com/watch?v=" + id + "&t=20", "https://youtu.be/" + id + "?si=abc",
                "https://youtube.com/shorts/" + id, "https://youtube.com/live/" + id, "https://www.youtube-nocookie.com/embed/" + id,
                "https://m.youtube.com/watch?v=" + id, "https://music.youtube.com/watch?v=" + id, "youtu.be/" + id,
                "youtube.com/watch?v=" + id, " https://youtube.com/v/" + id + " " }) Check(Youtube.VideoId(url) == id, "parse " + url);
            foreach (string url in new[] { "", null, "invalid", new string('x', 5000), "https://evilyoutube.com/watch?v=" + id,
                "https://youtube.com.evil.test/watch?v=" + id, "https://youtube.com@evil.test/watch?v=" + id,
                "https://user@youtube.com/watch?v=" + id, "ftp://youtube.com/watch?v=" + id, "https://youtube.com:8443/watch?v=" + id,
                "https://youtu.be/" + id + "/extra", "https://youtube.com/playlist?list=abc", "https://youtube.com/watch?v=short", "file:///C:/test.txt" })
            {
                bool rejected = false; try { Youtube.VideoId(url); } catch (AppException error) { rejected = error.Code == "INVALID_URL"; }
                Check(rejected, "reject " + (url == null ? "null" : url.Substring(0, Math.Min(url.Length, 80))));
            }
            using (var service = new ThumbnailService(ValidHandler()))
            {
                LookupResult data = await service.LookupAsync("https://youtu.be/" + id, CancellationToken.None);
                Check(data.Images.Count(x => x.Available) == 6, "all six qualities");
                Check(data.Best.Quality.Key == "maxresdefault", "best image uses actual dimensions");
                foreach (var image in data.Images)
                {
                    Check(image.Width == image.Quality.MinWidth && image.Height == image.Quality.MinHeight, "dimensions " + image.Quality.Key);
                    string file = Path.Combine(root, "صورة-" + image.Quality.Key + ".jpg"); ImageFile.Save(file, image.Bytes);
                    Check(File.ReadAllBytes(file).SequenceEqual(image.Bytes), "save exact bytes " + image.Quality.Key);
                    ImageFile.Save(file, image.Bytes);
                }
                Check(!Directory.GetFiles(root, "*.tmp").Any(), "atomic save leaves no temporary files");
            }
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => Response(Jpeg(120, 90)) }))
            {
                var data = await service.LookupAsync("https://youtu.be/" + id, CancellationToken.None);
                Check(data.Images.Count(x => x.Available) == 1 && data.Best.Quality.Key == "default", "reject placeholders for larger qualities");
            }
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => new HttpResponseMessage(HttpStatusCode.NotFound) })) await ErrorCode(service, "NOT_FOUND");
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => { throw new HttpRequestException("offline"); } })) await ErrorCode(service, "NETWORK");
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => Response(new byte[] { 1, 2, 3 }, "text/html") })) await ErrorCode(service, "NETWORK");
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => Response(new byte[ThumbnailService.MaxBytes + 1]) })) await ErrorCode(service, "NETWORK");
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => Response(new byte[] { 255, 216, 0, 0 }) })) await ErrorCode(service, "NETWORK");
            using (var service = new ThumbnailService(new FakeHandler { Respond = r => {
                if (!r.RequestUri.AbsolutePath.EndsWith("/default.jpg")) throw new HttpRequestException("offline"); return Response(Jpeg(120, 90));
            } }))
            {
                var data = await service.LookupAsync("https://youtu.be/" + id, CancellationToken.None);
                Check(data.Best.Quality.Key == "default" && data.Images.Count(x => x.Failure == "network") == 5, "partial network failure retains valid image");
            }
            using (var service = new ThumbnailService(ValidHandler()))
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool canceled = false;
                try { await service.LookupAsync("https://youtu.be/" + id, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
                Check(canceled, "cancellation propagates");
            }
            bool invalidName = false; try { ImageFile.Save(Path.Combine(root, "bad.png"), Jpeg(120, 90)); } catch (AppException) { invalidName = true; }
            Check(invalidName, "JPG extension required");
        }
        private static async Task LiveAsync()
        {
            using (var service = new ThumbnailService())
            {
                foreach (string video in new[] { id, "dQw4w9WgXcQ", "jNQXAC9IVRw", "aqz-KE-bpKQ" })
                {
                    LookupResult result = await service.LookupAsync("https://youtu.be/" + video, CancellationToken.None);
                    Check(result.Images.Any(x => x.Available), "live " + video + " qualities=" + result.Images.Count(x => x.Available) + " best=" + result.Best.Dimensions);
                    foreach (Thumbnail image in result.Images.Where(x => x.Available))
                    {
                        string file = Path.Combine(root, video + "-" + image.Quality.Key + ".jpg"); ImageFile.Save(file, image.Bytes);
                        Check(File.ReadAllBytes(file).SequenceEqual(image.Bytes), "live save " + video + " " + image.Quality.Key);
                    }
                }
                try { await service.LookupAsync("https://youtu.be/00000000000", CancellationToken.None); throw new Exception("Unknown video accepted"); }
                catch (AppException error) { Check(error.Code == "NOT_FOUND", "live nonexistent video rejected"); }
            }
        }
        private static int UserInterface(bool realImages = false)
        {
            int exitCode = 1;
            using (var service = new ThumbnailService(realImages ? new FakeHandler { Respond = request => {
                string[] parts = request.RequestUri.AbsolutePath.Split('/');
                string file = Path.Combine(root, parts[2] + "-" + parts[3]);
                return File.Exists(file) ? Response(File.ReadAllBytes(file)) : new HttpResponseMessage(HttpStatusCode.NotFound);
            } } : ValidHandler()))
            using (var form = new MainForm(service, new TestDesktop()))
            {
                var desktop = (TestDesktop)typeof(MainForm).GetField("desktop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form);
                form.Shown += async delegate {
                    try
                    {
                        await form.ExtractAsync("https://evilyoutube.com/watch?v=" + id);
                        Check(form.Selected == null && form.Status.ForeColor == Theme.Red, "UI rejects invalid link");
                        await form.ExtractAsync("https://youtu.be/" + id);
                        Check(form.Selected.Quality.Key == "maxresdefault" && form.Preview.Image.Width == 1280, "UI preview and best quality");
                        form.Options[4].Checked = true;
                        Check(form.Selected.Width == 320 && form.Preview.Image.Width == 320, "UI quality selection");
                        form.CopyButton.PerformClick(); Check(desktop.Copied == "https://i.ytimg.com/vi/" + id + "/mqdefault.jpg", "UI copy exact URL");
                        form.SaveButton.PerformClick(); Check(form.Status.Text.Contains("إلغاء") && !form.Busy, "UI save cancellation");
                        desktop.Destination = Path.Combine(root, "صورة من الواجهة.jpg"); form.SaveButton.PerformClick();
                        Check(File.ReadAllBytes(desktop.Destination).SequenceEqual(form.Selected.Bytes), "UI save cached preview bytes");
                        form.FolderButton.PerformClick(); Check(desktop.Opened == desktop.Destination, "UI open saved folder request");
                        foreach (int width in new[] { 1120, 900, 820 })
                        {
                            form.Size = new Size(width, 830); form.PerformLayout(); await Task.Delay(100);
                            Check(form.Preview.Width > 250 && form.Options[0].Width > 180 && form.SaveButton.Width > 80, "UI layout width " + width);
                        }
                        form.ClientSize = new Size(1120, 810); form.PerformLayout(); await Task.Delay(100);
                        if (realImages)
                        {
                            foreach (string video in new[] { id, "dQw4w9WgXcQ" })
                            {
                                form.UrlInput.Text = "https://youtu.be/" + video;
                                await form.ExtractAsync(form.UrlInput.Text); await Task.Delay(100);
                                Check(form.Selected != null && form.Preview.Image.Width == 1280, "real downloaded photo visible in UI " + video);
                                Check(form.Selected.Bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(root, video + "-maxresdefault.jpg"))), "UI matches downloaded real photo " + video);
                                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(root, "preview-" + video + ".png")); }
                            }
                        }
                        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(root, "native-preview.png")); }
                        exitCode = 0;
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { form.Close(); }
                };
                Application.Run(form);
            }
            return exitCode;
        }
        [STAThread]
        internal static int Main(string[] args)
        {
            root = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results")); Directory.CreateDirectory(root);
            try
            {
                if (args.Length > 0 && args[0] == "--live") LiveAsync().GetAwaiter().GetResult();
                else if (args.Length > 0 && (args[0] == "--ui" || args[0] == "--preview"))
                {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    if (UserInterface(args[0] == "--preview") != 0) return 1;
                }
                else CoreAsync().GetAwaiter().GetResult();
                Console.WriteLine("SUCCESS: " + count + " checks"); return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
