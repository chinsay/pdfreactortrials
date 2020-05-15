using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PdfReactorTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

            var files = new List<string>()
            {
                "LegalReqSampleForTesting.htm"
            };

#if PERFMONITOR
            var stopWatch = new Stopwatch();
            stopWatch.Start();
#endif
            const int numberOfIterations = 100;
            const int numberOfPendingConversionsMax = 2;
            var pdfReactorClient = new PDFReactorClient();

            for (var i = 0; i < numberOfIterations; i++)
            {
                foreach (var file in files)
                {
                    SpinWait.SpinUntil(() =>
                        pdfReactorClient.GetCount() <= numberOfPendingConversionsMax);
                    var i1 = i;
                    var task = Task.Run(() =>
                    {
                        pdfReactorClient.ConvertProcess(file, i1);
                    });
                    await task;
                }
            }

#if PERFMONITOR
            Console.WriteLine($"Number of Requests: {files.Count * numberOfIterations}{Environment.NewLine}" +
                              $"Time taken to Issue Requests: {stopWatch.ElapsedMilliseconds} ms");
#endif

            pdfReactorClient.SetCheckValue(false);
            SpinWait.SpinUntil(() => pdfReactorClient.GetCount() == 0);

#if PERFMONITOR
            stopWatch.Stop();

            Console.WriteLine($"Total Execution Time: {stopWatch.ElapsedMilliseconds} ms.{Environment.NewLine}" +
                              $"Avg Time Taken per PDF: {stopWatch.ElapsedMilliseconds / (files.Count * numberOfIterations)}");

#endif
            Console.ReadKey();
        }
    }
}
