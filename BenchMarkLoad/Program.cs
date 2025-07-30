using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using AsciiChart.Sharp;

namespace BenchMarkLoad
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            int numRequests;
            string url;
            string? jsonFolderPath;
            if (args.Length == 0)
            {
                numRequests = 1000;
                jsonFolderPath = @"D:\Code\DoAnTotNghiep\tool\LoadTest\User";
                url = @"http://171.245.205.120:8082/api/Authentication/register";
            }
            else
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: BenchmarkTool <url> <numRequests> <jsonFolderPath>");
                    return;
                }

                url = args[0];
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    Console.WriteLine("Invalid URL");
                    return;
                }

                if (!int.TryParse(args[1], out numRequests) || numRequests <= 0)
                {
                    Console.WriteLine("Invalid number of requests");
                    return;
                }

                jsonFolderPath = args.Length > 2 ? args[2] : null;
            }


            Console.WriteLine($"Benchmarking {url} with {numRequests} requests...");

            await RunBenchmark(url, numRequests, jsonFolderPath);

            Environment.Exit(0);
        }

        static async Task<HttpResponseMessage> SendRequestWithJson(HttpClient client, string url, string jsonFileName)
        {
            var jsonContent = string.Empty;

            if (!string.IsNullOrEmpty(jsonFileName) && File.Exists(jsonFileName))
            {
                jsonContent = await File.ReadAllTextAsync(jsonFileName);
            }

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Pragma", "no-cache");
            return await client.SendAsync(request);
        }

        static async Task RunBenchmark(string url, int numRequests, string? jsonFolderPath)
        {
            using HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(200);
            float averageResponseTime = 0;

            int successfulRequests = 0;
            int failedRequests = 0;


            List<string> failedRequestsLog = [];

            double[][] series = [new double[100], new double[100]];
            double[][] bandwidthSeries = [new double[100], new double[100]]; // Thêm series cho bandwidth

            int numRequestsPerXTime = numRequests < 100 ? 1 : numRequests / 100;

            int seriesIndex = 0;
            int bandwidthSeriesIndex = 0;

            string[] jsonFiles = jsonFolderPath != null ? Directory.GetFiles(jsonFolderPath, "*.json") : [];

            if (jsonFiles.Length == 0 && jsonFolderPath != null)
            {
                Console.WriteLine("No JSON files found in the specified folder.");
                return;
            }

            var tasks = new List<Task<ReponseTime>>();

            for (int i = 0; i < numRequests; i++)
            {
                tasks.Add(SendRequestAsync(client, url, jsonFolderPath, jsonFiles, i));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var responseTime in results)
            {
                // Tính bandwidth (MB) cho mỗi request
                double bandwidthMB = 0;
                if (responseTime.Response != null && responseTime.Response.Content != null)
                {
                    var contentBytes = await responseTime.Response.Content.ReadAsByteArrayAsync();
                    bandwidthMB = contentBytes.Length / 1024.0 / 1024.0;
                }

                if (responseTime.Response!.IsSuccessStatusCode)
                {
                    if (responseTime.Index % numRequestsPerXTime == 0)
                    {
                        Console.WriteLine($"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTime / (successfulRequests + failedRequests)} ms, Bandwidth: {bandwidthMB:F3} MB");
                        series[0][seriesIndex] = responseTime.Time;
                        bandwidthSeries[0][bandwidthSeriesIndex] = bandwidthMB;
                        seriesIndex++;
                        bandwidthSeriesIndex++;
                    }
                    successfulRequests++;
                }
                else
                {
                    var statusCode = responseTime.Response.StatusCode.ToString();
                    if (!failedRequestsLog.Contains(statusCode))
                    {
                        failedRequestsLog.Add(statusCode);
                    }
                    if (responseTime.Index % numRequestsPerXTime == 0)
                    {
                        Console.WriteLine($"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTime / (successfulRequests + failedRequests)} ms, Bandwidth: {bandwidthMB:F3} MB");
                        series[1][seriesIndex] = responseTime.Time;
                        bandwidthSeries[1][bandwidthSeriesIndex] = bandwidthMB;
                        seriesIndex++;
                        bandwidthSeriesIndex++;
                    }
                    failedRequests++;
                }
                averageResponseTime += responseTime.Time;
            }


            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"Total Requests: {numRequests}");
            Console.WriteLine($"Successful Requests: {successfulRequests}");
            Console.WriteLine($"Failed Requests: {failedRequests}");
            Console.WriteLine($"Average response time: {averageResponseTime / numRequests} ms");

            if (failedRequestsLog.Count != 0)
            {
                Console.WriteLine($"Failed requests log:");
            }
            foreach (var item in failedRequestsLog)
            {
                Console.WriteLine($"Failed request reason: {item}");
            }

            Console.WriteLine($"Chart: Reponse time per {numRequestsPerXTime} requests");
            Console.WriteLine(AsciiChart.Sharp.AsciiChart.Plot(
                    series,
                    new Options
                    {
                        AxisLabelFormat = "000.000 ms",
                        Fill = '·',
                        Height = 15,
                        SeriesColors =
                        [
                            AnsiColor.Green,
                            AnsiColor.Red,
                        ],
                        AxisLabelLeftMargin = 1,
                        LabelColor = AnsiColor.Aqua,
                    }));

            Console.WriteLine($"Chart: Bandwidth (MB) per {numRequestsPerXTime} requests");
            Console.WriteLine(AsciiChart.Sharp.AsciiChart.Plot(
                    bandwidthSeries,
                    new Options
                    {
                        AxisLabelFormat = "0.000 MB",
                        Fill = '·',
                        Height = 15,
                        SeriesColors =
                        [
                            AnsiColor.Yellow,
                            AnsiColor.Red,
                        ],
                        AxisLabelLeftMargin = 1,
                        LabelColor = AnsiColor.Aqua,
                    }));
        }

        static async Task<ReponseTime> SendRequestAsync(HttpClient client, string url, string? jsonFolderPath, string[] jsonFiles, int index)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            HttpResponseMessage response;

            if (jsonFolderPath != null && index < jsonFiles.Length)
            {
                response = await SendRequestWithJson(client, url, jsonFiles[index]);
            }
            else
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cache-Control", "no-cache");
                request.Headers.Add("Pragma", "no-cache");
                response = await client.SendAsync(request);
            }

            stopwatch.Stop();
            return new()
            {
                Response = response,
                Time = stopwatch.ElapsedMilliseconds,
                Index = index,
            };
        }
    }
    public class ReponseTime
    {
        public HttpResponseMessage? Response { get; set; }
        public float Time { get; set; }
        public int Index { get; set; }
    }
}
