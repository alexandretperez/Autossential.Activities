using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Diagnostics;

namespace Autossential.Activities
{
    public sealed class TimeLoop : NativeActivity<bool>
    {
        [RequiredArgument]
        public InArgument<TimeSpan> Timeout { get; set; }
        public InArgument<double> IntervalSeconds { get; set; }
        public OutArgument<int> IterationIndex { get; set; }

        [Browsable(false)]
        public ActivityAction Body { get; set; }

        private readonly Variable<TimeSpan> _timeoutVar = new();
        private readonly Variable<double> _intervalSecondsVar = new();
        private readonly Variable<int> _iterationIndexVar = new();
        private readonly Variable<DateTime> _startTimeVar = new();
        private readonly Variable<bool> _stopVar = new();

        protected override bool CanInduceIdle => true;

        public TimeLoop()
        {
            Body = new ActivityAction
            {
                Handler = new Sequence { DisplayName = "Do" }
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(_timeoutVar);
            metadata.AddImplementationVariable(_intervalSecondsVar);
            metadata.AddImplementationVariable(_iterationIndexVar);
            metadata.AddImplementationVariable(_startTimeVar);
            metadata.AddImplementationVariable(_stopVar);
        }

        protected override void Execute(NativeActivityContext context)
        {
            _timeoutVar.Set(context, Timeout.Get(context));
            _intervalSecondsVar.Set(context, IntervalSeconds.Get(context));
            _iterationIndexVar.Set(context, 0);
            _startTimeVar.Set(context, DateTime.UtcNow);
            _stopVar.Set(context, false);

            CreateExitBookmark(context);
            ExecuteInternal(context);
        }

        private void ExecuteInternal(NativeActivityContext context)
        {
            if (context.IsCancellationRequested)
            {
                context.MarkCanceled();
                return;
            }

            var elapsed = DateTime.UtcNow - _startTimeVar.Get(context);
            var timedOut = elapsed > _timeoutVar.Get(context);
            var stop = _stopVar.Get(context);

            if (timedOut || stop)
            {
                Result.Set(context, timedOut);
                return;
            }

            var index = _iterationIndexVar.Get(context);
            IterationIndex.Set(context, index);
            _iterationIndexVar.Set(context, index + 1);

            context.ScheduleAction(Body, OnIterationCompleted, OnIterationFaulted);
        }

        private void OnIterationFaulted(NativeActivityFaultContext faultContext, Exception propagatedException, ActivityInstance propagatedFrom)
        {
            faultContext.CancelChildren();
            throw propagatedException;
        }

        private void OnIterationCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            var intervalSeconds = _intervalSecondsVar.Get(context);
            if (intervalSeconds > 0 && !_stopVar.Get(context))
                Thread.Sleep(TimeSpan.FromSeconds(intervalSeconds));

            ExecuteInternal(context);
        }

        private void CreateExitBookmark(NativeActivityContext context)
        {
            var exitBookmark = context.CreateBookmark(OnExit, BookmarkOptions.NonBlocking);
            context.Properties.Add(Exit.BOOKMARK_NAME, exitBookmark);
        }

        private void OnExit(NativeActivityContext context, Bookmark bookmark, object value)
        {
            _stopVar.Set(context, true);
            context.CancelChildren();
            if (value is Bookmark b)
                context.ResumeBookmark(b, value);
        }
    }
}