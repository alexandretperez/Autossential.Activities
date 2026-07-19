using Autossential.Activities.Properties;
using System.Activities;

namespace Autossential.Activities
{
    public sealed class UnmapDrive : NetworkDrive
    {
        public InArgument<string> DriveLetter { get; set; }
        public bool AllDrives { get; set; } = false;
        protected override bool Execute(CodeActivityContext context)
        {
            if (AllDrives)
            {
                var messages = new List<string>();
                var networkDrives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Network).ToArray();
                foreach (var drive in networkDrives)
                {
                    var name = drive.Name[..(drive.Name.IndexOf(':') + 1)];
                    var code = Disconnect(name);
                    if (code != 0)
                    {
                        var message = GetMessageFromCode(code);
                        messages.Add($"{name} {code} - {message}");
                    }
                }

                if (messages.Count == 0)
                {
                    ResponseCode.Set(context, 0);
                    SetResponseMessageFromCode(context, 0);
                }
                else
                {
                    ResponseCode.Set(context, messages.Count == networkDrives.Length ? 2 : 1);
                    ResponseMessage.Set(context, string.Join("; ", messages));
                }

                return messages.Count == 0;
            }

            var driveLetter = DriveLetter.Get(context)
                ?? throw new InvalidOperationException(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.UnmapDrive_DriveLetter_DisplayName));

            var result = Disconnect(driveLetter);
            ResponseCode.Set(context, result);

            SetResponseMessageFromCode(context, result);
            return result == 0;
        }
    }
}