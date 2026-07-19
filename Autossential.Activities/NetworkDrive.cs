using Autossential.Activities.Properties;
using System.Activities;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace Autossential.Activities
{
    public abstract class NetworkDrive : CodeActivity<bool>
    {
        public OutArgument<int> ResponseCode { get; set; }
        public OutArgument<string> ResponseMessage { get; set; }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2W(
             ref NetResource lpNetResource,
             string lpPassword,
             string lpUserName,
             uint dwFlags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2W(
            string lpName,
            int dwFlags,
            bool fForce);

        protected static int Connect(string driveLetter, string sharedDrivePath, NetworkCredential credential)
        {
            ValidateDriveLetterFormat(driveLetter);

            var resource = new NetResource
            {
                dwType = 1,         // disk
                dwScope = 2,        // global network
                dwDisplayType = 3,  // share
                lpLocalName = NormalizeDriveLetter(driveLetter),
                lpRemoteName = sharedDrivePath
            };

            string username = null;
            string password = null;

            if (credential is not null)
            {
                username = string.IsNullOrEmpty(credential.Domain) ? credential.UserName : Path.Combine(credential.Domain, credential.UserName);
                password = credential.Password;
            }

            return WNetAddConnection2W(ref resource, password, username, 0);
        }

        protected static int Disconnect(string driveLetter)
        {
            ValidateDriveLetterFormat(driveLetter);
            return WNetCancelConnection2W(NormalizeDriveLetter(driveLetter), 0, true);
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NetResource
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        protected static bool IsDriveMapped(string driveLetter)
        {
            ValidateDriveLetterFormat(driveLetter);
            foreach (var drive in Environment.GetLogicalDrives())
            {
                if (drive[0].ToString().Equals(driveLetter[0].ToString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        protected static void UnmapDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Network)
                {
                    Disconnect(drive.Name.TrimEnd('\\'));
                }
            }
        }

        private const string ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        protected static string GetAvailableDriveLetter()
        {
            var letters = ALPHABET.ToCharArray().Select(p => p.ToString()).ToList();
            foreach (var di in DriveInfo.GetDrives())
            {
                char drive = di.Name[0];
                if (char.IsLower(drive))
                    drive = char.ToUpper(drive);

                letters.Remove(drive.ToString());
            }

            var random = letters.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
            return NormalizeDriveLetter(random) ?? throw new Exception(Resources.NetworkDrive_ErrorMsg_NoDriversAvailable);
        }

        private static void ValidateDriveLetterFormat(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter)
                || driveLetter.Length != 2
                || !ALPHABET.Contains(driveLetter[0])
                || driveLetter[1] != ':')
                throw new InvalidOperationException(Resources.NetworkDrive_ErrorMsg_InvalidDriveLetter);
        }

        private static string NormalizeDriveLetter(string driveLetter) => char.ToUpper(driveLetter[0]) + ":";

        protected static string GetMessageFromCode(int responseCode)
            => responseCode == 0
                ? Resources.NetworkDrive_ResponseMsg_Success
                : new Win32Exception(responseCode).Message;

        protected void SetResponseMessageFromCode(CodeActivityContext context, int responseCode)
        {
            ResponseMessage.Set(context, GetMessageFromCode(responseCode));
        }
    }
}
