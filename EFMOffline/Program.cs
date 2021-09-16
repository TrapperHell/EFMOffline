using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using EFMOffline.Models;
using Microsoft.Extensions.Configuration;

namespace EFMOffline
{
    class Program
    {
        static readonly AppOptions appOptions;
        static readonly PicturaeOptions picturaeOptions;

        static Program()
        {
            // Simple approach to configuration binding (instead of using Host)
            var configBuilder = new ConfigurationBuilder();
            var jsonSource = new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource { Path = "appsettings.json" };
            configBuilder.Add(jsonSource);
            var config = configBuilder.Build();
            appOptions = config.Get<AppOptions>();
            picturaeOptions = config.GetSection("Picturae").Get<PicturaeOptions>();
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Embassy of the Free Mind - Book Downloader for Offline Use";

            Console.WriteLine($"{nameof(EFMOffline)} v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion} - Provided freely for personal, educational use. ");
            Console.WriteLine($"This project is not endorsed by or affiliated with Embassy of the Free Mind");
            Console.WriteLine();

            await GetAllMediaAsync(DownloadMediaAsync);
            Console.ReadLine();
        }

        private static async Task GetAllMediaAsync(Func<Media, Task> action)
        {
            for (var pageNumber = 1; ; pageNumber++)
            {
                var publications = await GetPublicationsAsync(pageNumber);

                if (publications == null)
                {
                    Console.Write("Unable to retrieve list of books. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                foreach (var media in publications.Media)
                    await action?.Invoke(media);

                if (publications.Media.Count < picturaeOptions.PageSize || publications.Pagination.CurrentPage == publications.Pagination.Pages)
                    break;
            }
        }

        private static async Task<MediaPage> GetPublicationsAsync(int pageNumber = 1)
        {
            var client = new HttpClient { BaseAddress = new Uri("https://webservices.picturae.com/mediabank/") };

            var queryStringParams = new Dictionary<string, string>
            {
                { "apiKey", picturaeOptions.ApiKey },
                { "fq[]", picturaeOptions.PublicationSearchParam == null ? null : $"search_s_digitized_publication:\"{picturaeOptions.PublicationSearchParam}\"" },
                { "page", pageNumber.ToString() },
                { "rows", picturaeOptions.PageSize.ToString() }
            };

            var response = await client.GetAsync($"media?{string.Join("&", queryStringParams.Select(x => $"{x.Key}={x.Value}"))}");
            if (response.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<MediaPage>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return null;
        }

        private static async Task DownloadMediaAsync(Media media)
        {
            var title = media.Title.Length > 30 ? media.Title.Substring(0, media.Title.IndexOf(' ', 30)) : media.Title;
            Console.Write($"Downloading {title}...".PadRight(55));
            var consoleLeft = Console.CursorLeft;

            if (!media.Asset.Any())
            {
                Console.WriteLine("\t[Unavailable]");
                return;
            }

            var dirTitle = GetSafeDirectoryName(media.Title);
            dirTitle = Path.Combine(appOptions.DownloadsDirectory, dirTitle);
            if (!Directory.Exists(dirTitle))
                Directory.CreateDirectory(dirTitle);
            else
            {
                // HACK: Naive way of determining whether the book is already downloaded or not
                Console.WriteLine("\t[Already Downloaded]");
                return;
            }

            for (int assetIx = 0; assetIx < media.Asset.Count; assetIx++)
            {
                Console.CursorLeft = consoleLeft;
                Console.Write($"\tPage: {assetIx + 1} / {media.Asset.Count}");

                using (var fullImg = new Bitmap(media.Asset[assetIx].Width, media.Asset[assetIx].Height))
                using (var gfx = Graphics.FromImage(fullImg))
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("Connection", "Keep-Alive");

                        var newLeft = Console.CursorLeft;

                        // Hack: We're assuming that the images are all 256x256
                        for (var y = 0; (y * 256) < media.Asset[assetIx].Height; y++)
                        {
                            var tasks = new List<Task<HttpResponseMessage>>();

                            for (var x = 0; (x * 256) < media.Asset[assetIx].Width; x++)
                            {
                                var imgUrl = $"https://images.memorix.nl/rit/deepzoom/{media.Asset[assetIx].Uuid}_files/{picturaeOptions.ImageZoomLevel}/{x}_{y}.jpg";

                                tasks.Add(httpClient.GetAsync(imgUrl));
                            }

                            await Task.WhenAll(tasks.ToArray());

                            Console.CursorLeft = newLeft;
                            Console.Write($"\t[{(y * 256 * 100) / media.Asset[assetIx].Height}%]\t");

                            for (var ix = 0; ix < tasks.Count; ix++)
                            {
                                if (tasks[ix].Result.IsSuccessStatusCode)
                                {
                                    using (var img = Image.FromStream(await tasks[ix].Result.Content.ReadAsStreamAsync()))
                                    {
                                        gfx.DrawImage(img, new Point(ix * 256, y * 256));
                                    }
                                }

                                tasks[ix].Result.Dispose();
                            }
                        }
                    }

                    var fileName = $"{(assetIx + 1).ToString().PadLeft(media.Asset.Count.ToString().Length, '0')}.jpg";
                    fullImg.Save(Path.Combine(dirTitle, fileName), ImageFormat.Jpeg);
                }
            }

            Console.CursorLeft = consoleLeft;
            Console.WriteLine(string.Empty.PadRight(30));
        }



        private static string GetSafeDirectoryName(string directoryName)
        {
            var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct();

            return new string(directoryName.Where(c => !invalidChars.Contains(c)).ToArray());
        }
    }
}
