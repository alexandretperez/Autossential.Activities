using Autossential.Activities.Base;
using Autossential.Activities.Extensions;
using System.Activities.DesignViewModels;
using System.Activities.ViewModels;

namespace Autossential.Activities.ViewModels
{
    internal class UnmapDriveViewModel(IDesignServices services) : BaseViewModel(services)
    {
        public DesignInArgument<string> DriveLetter { get; set; }
        public DesignOutArgument<int> ResponseCode { get; set; }
        public DesignOutArgument<string> ResponseMessage { get; set; }
        public DesignOutArgument<bool> Result { get; set; }
        public DataSource<string> AvailableDrivers { get; }
        public DesignProperty<bool> AllDrives { get; set; }
        protected override void InitializeModel()
        {
            base.InitializeModel();
            PersistValuesChangedDuringInit();

            int orderIndex = 0;

            AllDrives.IsPrincipal = true;
            AllDrives.OrderIndex = orderIndex++;
           
            DriveLetter.IsPrincipal = true;
            DriveLetter.OrderIndex = orderIndex++;
            DriveLetter.DataSource = AvailableDrivers;

            ResponseCode.OrderIndex = orderIndex++;
            ResponseMessage.OrderIndex = orderIndex++;
            Result.OrderIndex = orderIndex;
        }

        protected override void InitializeRules()
        {
            Rule(nameof(AllDrives), AllChanged, true);
        }

        private void AllChanged()
        {
            DriveLetter.IsVisible = !AllDrives.Value;
        }
    }
}
