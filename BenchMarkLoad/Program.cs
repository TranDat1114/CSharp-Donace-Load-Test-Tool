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
            string? jWTBearer;
            if (args.Length == 0)
            {
                numRequests = 10;
                //jsonFolderPath = @"D:\Code\DoAnTotNghiep\tool\LoadTest\User";
                jsonFolderPath = @"D:\Code\DoAnTotNghiep\tool\LoadTest\Create calendar\CalendarJson";
                url = @"http://171.245.205.120:8082/api/Calendar/create-calendar";
                jWTBearer = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJ1c2VyXzFAZGVtby5jb20iLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6ImE5MzMzMjM1LWJhNzgtNDQwMy05MjcwLTA0ZWFmYzBmOWIwZCIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiJ1c2VyXzFAZGVtby5jb20iLCJleHAiOjI1MzQwMjI3NTYwMCwiaXNzIjoiaHR0cHM6Ly9sb2NhbGhvc3Q6NzI3MiIsImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0OjcyNzIifQ.dvDGaGd5UlKftz8EBhY002LWcrhwsoffDGYkPrsvCr0";
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

                jWTBearer = args.Length > 2 ? args[2] : null;
                jsonFolderPath = args.Length > 3 ? args[3] : null;
            }


            Console.WriteLine($"Benchmarking {url} with {numRequests} requests...");

            await RunBenchmark(url, numRequests, jsonFolderPath, jWTBearer);

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
            return await client.PostAsync(url, content);
        }

        static async Task RunBenchmark(string url, int numRequests, string? jsonFolderPath, string? jWTBearer = null)
        {
            using HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(200);
            client.DefaultRequestHeaders.Add("Access-Control-Allow-Origin", "*");

            float averageResponseTime = 0;

            int successfulRequests = 0;
            int failedRequests = 0;

            List<string> failedRequestsLog = [];

            double[][] series = [new double[100], new double[100]];

            int numRequestsPerXTime = numRequests < 100 ? 1 : numRequests / 100;

            int seriesIndex = 0;

            string[] jsonFiles = jsonFolderPath != null ? Directory.GetFiles(jsonFolderPath, "*.json") : [];

            if (jsonFiles.Length == 0 && jsonFolderPath != null)
            {
                Console.WriteLine("No JSON files found in the specified folder.");
                return;
            }

            var tasks = new List<Task<ReponseTime>>();

            for (int i = 0; i < numRequests; i++)
            {
                tasks.Add(SendRequestAsync(client, url, jsonFolderPath, jsonFiles, i, jWTBearer));

                if (tasks.Count == Environment.ProcessorCount || i == numRequests - 1)
                {
                    await Task.WhenAll(tasks);
                    foreach (var task in tasks)
                    {
                        ReponseTime responseTime = await task;

                        if (responseTime.Response!.IsSuccessStatusCode)
                        {
                            if (responseTime.Index % numRequestsPerXTime == 0)
                            {
                                Console.WriteLine($@"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTime / successfulRequests + failedRequests} ms");
                                series[0][seriesIndex] = responseTime.Time;
                                seriesIndex++;
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
                                Console.WriteLine($@"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTime / successfulRequests + failedRequests} ms");
                                series[1][seriesIndex] = responseTime.Time;
                                seriesIndex++;

                            }
                            failedRequests++;
                        }
                        averageResponseTime += responseTime.Time;
                    }

                    tasks.Clear();
                }
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
        }

        static async Task<ReponseTime> SendRequestAsync(HttpClient client, string url, string? jsonFolderPath, string[] jsonFiles, int index, string? JWTBearer = null)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            HttpResponseMessage response;


            if (JWTBearer != null)
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {JWTBearer}");
            }
            if (jsonFolderPath != null && jsonFiles.Length == 1)
            {
                response = await SendRequestWithJson(client, url, jsonFiles[0]);
            }
            else if (jsonFolderPath != null && index < jsonFiles.Length)
            {
                response = await SendRequestWithJson(client, url, jsonFiles[index]);
            }
            else
            {
                response = await client.GetAsync(url);
            }
            Console.WriteLine(response.Headers);
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
