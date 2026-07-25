
using Autossential.Activities.Base;
using Autossential.Activities.Properties;
using System.Activities.DesignViewModels;

namespace Autossential.Activities.ViewModels
{
    internal class ExitViewModel(IDesignServices services) : BaseViewModel(services)
    {
        public DesignInArgument<bool> Condition { get; set; }

        protected override void InitializeModel()
        {
            base.InitializeModel();
            PersistValuesChangedDuringInit();

            Condition.IsPrincipal = true;
            Condition.Placeholder = Resources.Exit_Placeholder_TrueByDefault;
        }
    }
}
