
using Autossential.Activities.Base;
using System.Activities.DesignViewModels;

namespace Autossential.Activities.ViewModels
{
    internal class TimeLoopViewModel(IDesignServices services) : BaseViewModel(services)
    {
        public DesignInArgument<TimeSpan> Timeout { get; set; }
        public DesignInArgument<double> IntervalSeconds { get; set; }
        public DesignOutArgument<int> IterationIndex { get; set; }
        public DesignOutArgument<bool> Result { get; set; }

        protected override void InitializeModel()
        {
            base.InitializeModel();
            PersistValuesChangedDuringInit();

            var orderIndex = 0;
            Timeout.IsPrincipal = true;
            Timeout.OrderIndex = orderIndex++;
            IntervalSeconds.OrderIndex = orderIndex++;
            IterationIndex.OrderIndex = orderIndex++;
            Result.OrderIndex = orderIndex++;
        }
    }
}
