using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DownloadFileMultiThread
{
    public class DownloadFileMultiThread
    {
        public string DownloadUrl { get; private set; }
        public string SavePath { get; private set; }
        public int NumberOfThreads { get; private set; }

        private static readonly HttpClient client = new HttpClient();
        private long _fileSize;

        public delegate void ProgressChangedHandler(int threadIndex, int progress);
        public event ProgressChangedHandler ProgressChanged;

        public DownloadFileMultiThread(string url, string path, int threads)
        {
            DownloadUrl = url;
            SavePath = path;
            NumberOfThreads = threads;
        }

        public void GetFileSize()
        {
            var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Head, DownloadUrl)).Result;
            response.EnsureSuccessStatusCode();
            _fileSize = response.Content.Headers.ContentLength ?? 0;

            if (_fileSize == 0)
            {
                throw new Exception("Could not get file size.");
            }
        }

        private (long start, long end)[] DivideFileIntoParts()
        {
            long partSize = _fileSize / NumberOfThreads;
            var parts = new (long start, long end)[NumberOfThreads];

            for (int i = 0; i < NumberOfThreads; i++)
            {
                long start = i * partSize;
                long end = start + partSize - 1;

                if (i == NumberOfThreads - 1)
                {
                    end = _fileSize - 1;
                }

                parts[i] = (start, end);
            }

            return parts;
        }

        private void DownloadPart((long start, long end) range, int partIndex)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(range.start, range.end);

            var response = client.SendAsync(request).Result;
            response.EnsureSuccessStatusCode();

            long downloadedBytes = 0;
            using (var contentStream = response.Content.ReadAsStreamAsync().Result)
            using (var fileStream = new FileStream($"{SavePath}.part{partIndex}", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = contentStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    // Tính toán tiến trình và bắn sự kiện ProgressChanged
                    int progress = (int)((double)downloadedBytes / (range.end - range.start + 1) * 100);
                    ProgressChanged?.Invoke(partIndex, progress);
                }
            }
        }

        private void MergeParts()
        {
            using (var outputStream = new FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                for (int i = 0; i < NumberOfThreads; i++)
                {
                    string tempFile = $"{SavePath}.part{i}";
                    using (var inputStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        inputStream.CopyTo(outputStream);
                    }

                    File.Delete(tempFile);
                }
            }
        }

        public void StartDownload()
        {
            if (_fileSize == 0)
                throw new Exception("File size is not set. Call GetFileSize() first.");

            var parts = DivideFileIntoParts();
            Thread[] threads = new Thread[NumberOfThreads];

            for (int i = 0; i < NumberOfThreads; i++)
            {
                int partIndex = i;
                threads[i] = new Thread(() => DownloadPart(parts[partIndex], partIndex));
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            MergeParts();
        }


    }
}
