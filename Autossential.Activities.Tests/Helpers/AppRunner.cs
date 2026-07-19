using System.Activities;

namespace Autossential.Activities.Tests.Helpers
{
    internal static class AppRunner
    {
        public static WorkflowApplication Run(Activity root, Action<WorkflowApplicationCompletedEventArgs> onCompleted = null)
        {
            var app = new WorkflowApplication(root)
            {
                Aborted = _ => { },
                Completed = onCompleted,
                PersistableIdle = _ => PersistableIdleAction.Unload
            };
            app.Run();
            return app;
        }
    }
}
