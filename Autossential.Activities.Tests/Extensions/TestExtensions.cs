using System.Activities;

namespace Autossential.Activities.Tests.Extensions
{
    public static class TestExtensions
    {
        extension(WorkflowInvoker)
        {
            public static (T, IDictionary<string, object>) InvokeOutputs<T>(Activity<T> activity, IDictionary<string, object> inputs = null)
            {
                var outputs = inputs is null
                    ? WorkflowInvoker.Invoke((Activity)activity)
                    : WorkflowInvoker.Invoke((Activity)activity, inputs);

                return ((T)outputs["Result"], outputs);
            }
        }
    }
}
