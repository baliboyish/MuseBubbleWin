using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MuseBubbleWin
{
    internal static class MuseOfficialIcon
    {
        // Source: https://muse.ai/manifest.json (purpose=any, 512x512).
        internal const string SourceUrl = "https://muse.ai/images/pwa_icons/any/512.png?v=3";
        static readonly string CachePath = Path.Combine(Application.StartupPath, "muse-official-icon.png");
        static Image image;
        static bool cacheChecked;

        static Image Decode(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var original = Image.FromStream(stream, true, true))
            {
                if (original.Width < 32 || original.Height < 32 || original.Width > 2048 || original.Height > 2048)
                    throw new InvalidDataException("Unexpected Muse icon dimensions.");
                return new Bitmap(original);
            }
        }

        internal static bool LoadCached()
        {
            if (image != null) return true;
            if (cacheChecked) return false;
            cacheChecked = true;
            try { if (File.Exists(CachePath)) image = Decode(File.ReadAllBytes(CachePath)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            return image != null;
        }

        internal static async Task<bool> LoadAsync()
        {
            if (LoadCached()) return true;
            using (var client = new WebClient())
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                client.Headers[HttpRequestHeader.UserAgent] = "MuseBubbleWin";
                Task<byte[]> download = client.DownloadDataTaskAsync(new Uri(SourceUrl));
                if (await Task.WhenAny(download, Task.Delay(15000)) != download)
                {
                    client.CancelAsync();
                    // Observe the cancelled request so a late network fault is not unobserved.
                    try { await download; } catch (WebException) { } catch (TaskCanceledException) { }
                    throw new TimeoutException("Downloading the official Muse icon timed out.");
                }
                byte[] bytes = await download;
                Image decoded = Decode(bytes);
                // A read-only installation can still display the icon for this session.
                try { File.WriteAllBytes(CachePath, bytes); }
                catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex); }
                catch (UnauthorizedAccessException ex) { System.Diagnostics.Debug.WriteLine(ex); }
                image = decoded;
                return true;
            }
        }

        internal static Bitmap Render(int size, bool light)
        {
            LoadCached();
            var result = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float inset = size * .07f;
                var area = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);
                using (var shadow = new SolidBrush(Color.FromArgb(35, 25, 30, 60)))
                    g.FillEllipse(shadow, inset, inset + size * .035f, area.Width, area.Height);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(area);
                    var state = g.Save(); g.SetClip(path);
                    using (var background = new SolidBrush(light ? Color.White : Color.FromArgb(32, 34, 45)))
                        g.FillEllipse(background, area);
                    if (image != null)
                    {
                        // Fit the authentic artwork intact; the circular window clips only its outer background.
                        float ratio = Math.Min(area.Width / image.Width, area.Height / image.Height);
                        float width = image.Width * ratio, height = image.Height * ratio;
                        g.DrawImage(image, new RectangleF(area.X + (area.Width - width) / 2,
                            area.Y + (area.Height - height) / 2, width, height));
                    }
                    else
                    {
                        // Neutral loading/offline placeholder, never a fabricated official logo.
                        using (var font = new Font(Ui.FontName, size * .26f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var ink = new SolidBrush(light ? Color.FromArgb(32, 33, 42) : Color.White))
                        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("M", font, ink, area, format);
                    }
                    g.Restore(state);
                }
                using (var pen = new Pen(Color.FromArgb(180, Color.White), Math.Max(1, size * .012f)))
                    g.DrawEllipse(pen, area);
            }
            return result;
        }
    }
}
