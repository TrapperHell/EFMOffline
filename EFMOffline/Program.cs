using EFMOffline.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EFMOffline
{
    class Program
    {
        public static void Main()
        {
            Console.BackgroundColor = ConsoleColor.Cyan;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();

            Console.Title = "Embassy of the Free Mind - Book Downloader for Offline Use";

            Console.WriteLine($"{nameof(EFMOffline)} v. {FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion} - Provided freely for personal, educational use. ");
            Console.WriteLine($"This project is not endorsed by or affiliated with Embassy of the Free Mind");
            Console.WriteLine();

            GetAllMediaAsync(DownloadMediaAsync).Wait();

            Console.ReadLine();
        }

        private static async Task GetAllMediaAsync(Func<Media, Task> action)
        {
            for (var pageNumber = 1; ; pageNumber++)
            {
                var result = await GetPublicationsAsync(pageNumber, Constants.PageSize, Constants.PublicationSearchParam);

                if (!result.IsSuccessful)
                {
                    Console.Write("Unable to retrieve list of books. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                var publications = result.Data;

                foreach (var media in publications.Media)
                    await action?.Invoke(media);

                if (publications.Media.Count < Constants.PageSize)
                    break;
            }
        }

        private static async Task<IRestResponse<MediaResponse>> GetPublicationsAsync(int pageNumber = 1, int pageSize = Constants.PageSize, string publicationSearchParam = null)
        {
            var client = new RestClient("https://webservices.picturae.com/mediabank");
            var request = new RestRequest("media");

            var queryStringParams = new Dictionary<string, string>
            {
                { "apiKey", Constants.PicturaeApiKey },
                { "fq[]", publicationSearchParam == null ? null : $"search_s_digitized_publication:\"{publicationSearchParam}\"" },
                { "page", pageNumber.ToString() },
                { "rows", pageSize.ToString() }
            };

            foreach (var queryStringParam in queryStringParams)
            {
                if (queryStringParam.Value == null)
                    continue;

                request.AddParameter(new Parameter
                {
                    Type = ParameterType.QueryString,
                    Name = queryStringParam.Key,
                    Value = queryStringParam.Value
                });
            }

            return await client.ExecuteGetTaskAsync<MediaResponse>(request);
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

                        for (var y = 0; (y * 256) < media.Asset[assetIx].Height; y++)
                        {
                            var tasks = new List<Task<HttpResponseMessage>>();

                            for (var x = 0; (x * 256) < media.Asset[assetIx].Width; x++)
                            {
                                var imgUrl = $"https://images.memorix.nl/rit/deepzoom/{media.Asset[assetIx].Uuid}_files/{Constants.ImageZoomLevel}/{x}_{y}.jpg";

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
                                        // Hack: We're assuming that the images are all 256x256
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
            Console.WriteLine(string.Empty.PadRight(20));
        }



        private static string GetSafeDirectoryName(string directoryName)
        {
            var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct();

            return new string(directoryName.Where(c => !invalidChars.Contains(c)).ToArray());
        }
    }
}