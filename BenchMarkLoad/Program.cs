using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AsciiChart.Sharp;
namespace BenchMarkLoad;
class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: BenchmarkTool <url> <numRequests> <jsonFolderPath>");
            return;
        }

        string url = args[0];
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            Console.WriteLine("Invalid URL");
            return;
        }

        if (!int.TryParse(args[1], out int numRequests) || numRequests <= 0)
        {
            Console.WriteLine("Invalid number of requests");
            return;
        }

        string? jsonFolderPath = args.Length > 2 ? args[2] : null;

        Console.WriteLine($"Benchmarking {url} with {numRequests} requests...");

        await RunBenchmark(url, numRequests, jsonFolderPath);
    }



    static async Task RunBenchmark(string url, int numRequests, string? jsonFolderPath)
    {
        using HttpClient client = new();
        Stopwatch stopwatch = new();
        float averageResponseTime = 0;
        float averageResponseTimePerShowResult = 0;
        int successfulRequests = 0;
        int failedRequests = 0;
        int numRequestsPerXTime;
        int numShowResultPerXTime = numRequests / (numRequests < 100 ? numRequests : numRequests / 100);

        if (numRequests < 100)
        {
            numRequestsPerXTime = numRequests;
        }
        else
        {
            numRequestsPerXTime = numRequests / 100;
        }

        float averageResponseTimeAfterXRequest = 0;

        string[] failedRequestsLog = [];

        double[][] series = [new double[100], new double[100]];

        int seriesPosition = 0;

        string[] jsonFiles = jsonFolderPath != null ? Directory.GetFiles(jsonFolderPath, "*.json") : [];

        if (jsonFiles.Length == 0 && jsonFolderPath != null)
        {
            Console.WriteLine("No JSON files found in the specified folder.");
            return;
        }

        for (int i = 0; i < numRequests; i++)
        {

            if (i % numShowResultPerXTime == 0)
            {
                Console.WriteLine($"After {i} Request| Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTimePerShowResult / numShowResultPerXTime} ms");
                averageResponseTimePerShowResult = 0;
            }
            stopwatch.Restart();
            HttpResponseMessage response;
            if (jsonFolderPath != null && i < jsonFiles.Length)
            {
                response = await SendRequestWithJson(client, url, jsonFiles[i]);
            }
            else
            {
                response = await client.GetAsync(url);
            }
            stopwatch.Stop();

            if (numRequests < 100)
            {
                if (response.IsSuccessStatusCode)
                {

                    series[0][seriesPosition] = stopwatch.ElapsedMilliseconds;
                    successfulRequests++;
                    seriesPosition++;

                }
                else
                {
                    var statusCode = response.StatusCode.ToString();
                    if (!failedRequestsLog.Contains(statusCode))
                    {
                        failedRequestsLog = [.. failedRequestsLog, statusCode];
                    }
                    series[1][seriesPosition] = stopwatch.ElapsedMilliseconds;
                    failedRequests++;
                    seriesPosition++;

                }
            }
            else
            {
                if (response.IsSuccessStatusCode)
                {
                    if (i % numRequestsPerXTime == 0)
                    {
                        series[0][seriesPosition] = averageResponseTimeAfterXRequest / numRequestsPerXTime;
                        averageResponseTimeAfterXRequest = 0;
                        seriesPosition++;
                    }
                    successfulRequests++;
                }
                else
                {
                    var statusCode = response.StatusCode.ToString();
                    if (!failedRequestsLog.Contains(statusCode))
                    {
                        failedRequestsLog = [.. failedRequestsLog, statusCode];
                    }
                    if (i % numRequestsPerXTime == 0)
                    {
                        series[1][seriesPosition] = averageResponseTimeAfterXRequest / numRequestsPerXTime;
                        averageResponseTimeAfterXRequest = 0;
                        seriesPosition++;
                    }
                    failedRequests++;
                }
            }


            averageResponseTimeAfterXRequest += stopwatch.ElapsedMilliseconds;
            averageResponseTimePerShowResult += stopwatch.ElapsedMilliseconds;
            averageResponseTime += stopwatch.ElapsedMilliseconds;
        }

        Console.WriteLine($"\nSummary:");
        Console.WriteLine($"Total Requests: {numRequests}");
        Console.WriteLine($"Successful Requests: {successfulRequests}");
        Console.WriteLine($"Failed Requests: {failedRequests}");
        Console.WriteLine($"Average response time: {averageResponseTime / numRequests} ms");

        if (failedRequestsLog.Length != 0)
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
                    AxisLabelFormat = "000.0 ms",
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

    static async Task<HttpResponseMessage> SendRequestWithJson(HttpClient client, string url, string jsonFileName)
    {
        // Tạo một đối tượng StringContent để chứa dữ liệu JSON
        var jsonContent = string.Empty;

        // Nếu có tên file JSON, đọc nội dung của file
        if (!string.IsNullOrEmpty(jsonFileName) && File.Exists(jsonFileName))
        {
            jsonContent = File.ReadAllText(jsonFileName);
        }

        // Gửi request POST với dữ liệu JSON
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return await client.PostAsync(url, content);
    }
}