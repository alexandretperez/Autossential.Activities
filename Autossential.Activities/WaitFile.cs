using Autossential.Activities.Base;
using Autossential.Activities.Extensions;
using Autossential.Activities.Properties;
using System.Activities;
using System.Diagnostics;

namespace Autossential.Activities
{
    public sealed class WaitFile : AsyncActivity
    {
        public InArgument<bool> ContinueOnError { get; set; }
        public InArgument<double> TimeoutSeconds { get; set; } = 30;
        public InArgument<string> FilePath { get; set; }
        public InArgument<double> PollingIntervalSeconds { get; set; } = 0.5;
        public InArgument<bool> WaitForExist { get; set; } = true;
        public InArgument<string> DirectoryPath { get; set; }
        public InArgument<string> SearchPattern { get; set; }
        public bool DynamicFile { get; set; } = false;
        public OutArgument<FileInfo> Result { get; set; }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (DynamicFile)
            {
                if (DirectoryPath == null)
                    metadata.AddValidationError(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.WaitFile_DirectoryPath_DisplayName));
            }
            else
            {
                if (FilePath == null)
                    metadata.AddValidationError(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.WaitFile_FilePath_DisplayName));
            }
        }

        protected override Task<Action<AsyncCodeActivityContext>> RunAsync(AsyncCodeActivityContext context, CancellationToken token)
        {
            var timeoutSeconds = TimeoutSeconds.Get(context);
            if (timeoutSeconds <= 0)
                timeoutSeconds = 30;

            var continueOnError = ContinueOnError.Get(context);

            if (DynamicFile)
                return WaitForDynamicFile(context, token)
                    .WithTimeout(TimeSpan.FromSeconds(timeoutSeconds), token)
                    .ContinueOnError(continueOnError);

            return WaitForFile(context, token)
                .WithTimeout(TimeSpan.FromSeconds(timeoutSeconds), token)
                .ContinueOnError(continueOnError);
        }

        private Task<Action<AsyncCodeActivityContext>> WaitForFile(AsyncCodeActivityContext context, CancellationToken token)
        {
            var filePath = FilePath.Get(context) ?? throw new InvalidOperationException(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.WaitFile_FilePath_DisplayName));
            var pollingInterval = TimeSpan.FromSeconds(Math.Max(PollingIntervalSeconds.Get(context), 0.1)); // Minimum 0.1 seconds

            if (!WaitForExist.Get(context) && !File.Exists(filePath))
                throw new FileNotFoundException();

            return Task.Run<Action<AsyncCodeActivityContext>>(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (token.IsCancellationRequested)
                            token.ThrowIfCancellationRequested();

                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (e is OperationCanceledException || e is ObjectDisposedException)
                            break;
                        else
                            Thread.Sleep(pollingInterval);
                    }
                }

                return ctx =>
                {
                    if (File.Exists(filePath))
                        Result.Set(ctx, new FileInfo(filePath));
                };

            }, token);
        }

        private Task<Action<AsyncCodeActivityContext>> WaitForDynamicFile(AsyncCodeActivityContext context, CancellationToken token)
        {
            var dirPath = DirectoryPath.Get(context) ?? throw new InvalidOperationException(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.WaitFile_DirectoryPath_DisplayName));
            var searchPattern = SearchPattern.Get(context) ?? "*.*";
            var pollingInterval = TimeSpan.FromSeconds(Math.Max(PollingIntervalSeconds.Get(context), 0.5)); // Minimum 0.1 seconds
            string filePath = null;

            if (!WaitForExist.Get(context) && !Directory.EnumerateFiles(dirPath, "*", SearchOption.TopDirectoryOnly).Any(path => Path.GetFileName(path).IsMatch(searchPattern)))
                throw new FileNotFoundException();

            return Task.Run<Action<AsyncCodeActivityContext>>(() =>
            {
               while (true)
               {
                   try
                   {
                       if (token.IsCancellationRequested)
                           token.ThrowIfCancellationRequested();

                       if (filePath is null || !File.Exists(filePath))
                       {
                           filePath = Directory
                               .EnumerateFiles(dirPath, "*", SearchOption.TopDirectoryOnly)
                               .FirstOrDefault(path => Path.GetFileName(path).IsMatch(searchPattern));
                       }
                       else
                       {
                           using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                           break;
                       }

                       Thread.Sleep(pollingInterval);
                   }
                   catch (Exception e)
                   {
                       if (e is OperationCanceledException || e is ObjectDisposedException)
                           break;
                       else
                           Thread.Sleep(pollingInterval);
                   }
               }

               return ctx =>
               {
                   if (filePath != null && File.Exists(filePath))
                       Result.Set(ctx, new FileInfo(filePath));
               };
            }, token);
        }
    }
}
