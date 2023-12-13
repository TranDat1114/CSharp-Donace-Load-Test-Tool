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
        int successfulRequests = 0;
        int failedRequests = 0;

        int numRequestsPerXTime = numRequests / (numRequests / 100);

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

            if (numRequests <= 100)
            {
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Successful request {i + 1} - Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
                    series[0][i] = stopwatch.ElapsedMilliseconds;
                    successfulRequests++;

                }
                else
                {
                    Console.WriteLine($"Failed request {i + 1} - Status code: {response.StatusCode}");

                    if (!failedRequestsLog.Any(p => p == response.StatusCode.ToString()))
                    {
                        _ = failedRequestsLog.Append(response.StatusCode.ToString());
                    }
                    series[1][i] = stopwatch.ElapsedMilliseconds;
                    failedRequests++;
                }
            }
            else
            {
                if (response.IsSuccessStatusCode)
                {
                    if (i % numRequestsPerXTime == 0)
                    {
                        Console.WriteLine($"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTimeAfterXRequest / numRequestsPerXTime} ms");
                        series[0][seriesPosition] = averageResponseTimeAfterXRequest;
                        averageResponseTimeAfterXRequest = 0;
                        seriesPosition++;
                    }
                    successfulRequests++;
                }
                else
                {
                    if (!failedRequestsLog.Any(p => p == response.StatusCode.ToString()))
                    {
                        _ = failedRequestsLog.Append(response.StatusCode.ToString());
                    }
                    if (i % numRequestsPerXTime == 0)
                    {
                        Console.WriteLine($"Successful: {successfulRequests}, Failed: {failedRequests}, Average response time: {averageResponseTimeAfterXRequest / numRequestsPerXTime} ms");
                        series[1][seriesPosition] = averageResponseTimeAfterXRequest;
                        averageResponseTimeAfterXRequest = 0;
                        seriesPosition++;
                    }
                    failedRequests++;
                }
            }
            averageResponseTimeAfterXRequest += stopwatch.ElapsedMilliseconds;
            averageResponseTime += stopwatch.ElapsedMilliseconds;
        }

        Console.WriteLine($"\nSummary:");
        Console.WriteLine($"Total Requests: {numRequests}");
        Console.WriteLine($"Successful Requests: {successfulRequests}");
        Console.WriteLine($"Failed Requests: {failedRequests}");
        Console.WriteLine($"Average response time: {averageResponseTime / numRequests} ms");

        foreach (var item in failedRequestsLog)
        {
            Console.WriteLine($"Failed request reason: {item}");
        }

        Console.WriteLine(AsciiChart.Sharp.AsciiChart.Plot(
                series,
                new Options
                {
                    AxisLabelFormat = "000.000 ms",
                    Fill = '·',
                    Height = 10,
                    SeriesColors =
                    [
                        AnsiColor.Green,
                        AnsiColor.Red,
                    ],
                    AxisLabelLeftMargin = 3,
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