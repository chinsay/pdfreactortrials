using RealObjects.PDFreactor.Webservice.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PdfReactorTestApp
{
    public class PDFReactorClient
    {
        private readonly PDFreactor _pdfReactor = null;
        private static readonly Queue<Tuple<string, string>> QueueDocumentIds = new Queue<Tuple<string, string>>();
        private Thread _dataThread = null;
        private static bool _continueChecking = true;

        public PDFReactorClient()
        {
            _pdfReactor = new PDFreactor("https://givepathhere.azurewebsites.net/service/rest")
            {
                Timeout = 60000
            };
        }

        public int GetCount()
        {
            return QueueDocumentIds.Count;
        }

        private void GetStatus()
        {
            bool isStatusOk;
            var retryCount = 5;
            do
            {
                try
                {
                    _pdfReactor.GetStatus();
                    isStatusOk = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    isStatusOk = false;
                    if (retryCount == 0)
                    {
                        throw;
                    }
                    Thread.Sleep(500);
                    retryCount--;
                }
            } while (!isStatusOk);
        }

        public void ConvertProcess(string url, int iteration)
        {
            try
            {
#if PERFMONITOR2
                var stopWatch = new Stopwatch();
                stopWatch.Start();
#endif
                var html = File.ReadAllText(url);

                var config = new Configuration
                {
                    Author = "Pdf Service",
                    Creator = "Pdf Service",
                    Title = $"{Path.GetFileNameWithoutExtension(url)} PDF Document",

                    AddLinks = true,
                    AddBookmarks = true,
                    AddTags = true,

                    // The input document can be either url or xml or html source string
                    Document = html,
                    //Document = "http://www.pdfreactor.com/product/samples/textbook/textbook.html"

                    //Compress the file to reduce file size
                    FullCompression = false,

                    PagesPerSheetProperties = new PagesPerSheetProperties()
                    {
                        Cols = 1,
                        Rows = 1,
                        //SheetMargin = "50mm 0mm 50mm 0mm",
                        //SheetSize = "A4",
                        //Spacing = "5mm 2mm"
                        Direction = PagesPerSheetDirection.RIGHT_DOWN,
                    },

                    // Digital Signature
                    //SignPDF = new SignPDF()
                    //{
                    //},

                    //IntegrationStyleSheets = new List<Resource>()
                    //{
                    //    new Resource()
                    //    {
                    //        Content = File.ReadAllText("common.css")
                    //    }
                    //},

                    //capture all the debug data for PDF Reactor Support
                    //DebugSettings = new DebugSettings
                    //{
                    //    All = true
                    //}
                };

                //config.Callbacks.Add(new Callback()
                //{
                //    Type = CallbackType.FINISH,
                //    ContentType = ContentType.JSON,
                //    Url = "https://XXXXXXX.servicebus.windows.net/pdf-reactor-callback"
                //});

                //_pdfReactor.GetStatus();
                GetStatus();
                //Console.WriteLine("Get Status is 200 OK");
                var fileName = $"Iter{iteration}_{Path.GetFileName(url)}_{DateTime.Now:ddMMyyyy-hhmmss}.pdf";

                var data = _pdfReactor.ConvertAsync(config);

                QueueDocumentIds.Enqueue(new Tuple<string, string>(data, fileName));

                if (_dataThread == null)
                {
                    SetCheckValue(true);
                    _dataThread = new Thread(RetrieveDocuments);
                    _dataThread.Start();
                }

#if PERFMONITOR2
                stopWatch.Stop();
                Console.WriteLine($"Request Queued url: {url}. {Environment.NewLine}" +
                                  $"Elapsed Time: {stopWatch.ElapsedMilliseconds} ms");
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void RetrieveDocuments()
        {
            while (QueueDocumentIds.Count > 0 || _continueChecking)
            {
                Thread.Sleep(500);
                if (QueueDocumentIds.Count > 0)
                {
                    var document = QueueDocumentIds.Peek();
                    try
                    {
                        var progress = _pdfReactor.GetProgress(document.Item1);
                        if (progress != null && progress.Finished)
                        {
                            var result = _pdfReactor.GetDocument(document.Item1);
                            using (var binWriter = new BinaryWriter(new FileStream(
                                document.Item2,
                                FileMode.Create,
                                FileAccess.Write)))
                            {
                                binWriter.Write(result.Document);
                                binWriter.Close();
                            }

                            QueueDocumentIds.Dequeue();
                            //Console.WriteLine($"PDF saved locally for {documentIds[i].Item2}");
                        }
                    }
                    catch (DocumentNotFoundException)
                    {
                        Console.WriteLine($"Document Not found: {document.Item1}:{document.Item2}");
                        QueueDocumentIds.Dequeue();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
        }

        public void SetCheckValue(bool val)
        {
            _continueChecking = val;
        }
    }
}
