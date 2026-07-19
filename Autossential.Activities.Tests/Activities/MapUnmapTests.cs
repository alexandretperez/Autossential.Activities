using Autossential.Activities.Tests.Extensions;
using System.Activities;

namespace Autossential.Activities.Tests.Activities
{
    [NotInParallel]
    public class MapUnmapTests
    {
        private const string NetworkPath = @"\\127.0.0.1\Shared";

        private static string[] GetLogicalDrives() => Environment.GetLogicalDrives();

        [Test]
        public async Task ShouldMapAndUnmap_CustomDriveLetter()
        {
            var map = new MapDrive
            {
                SharedDrivePath = new InArgument<string>(NetworkPath)
            };

            var drivers = GetLogicalDrives();
            var driversCount = drivers.Length;

            var (result, outputs) = WorkflowInvoker.InvokeOutputs(map);
            await Assert.That(result).IsTrue();
            await Assert.That(string.IsNullOrEmpty(outputs[nameof(MapDrive.MappedDrive)].ToString())).IsFalse();

            var newDrivers = GetLogicalDrives().Except(drivers).ToArray();
            await Assert.That(newDrivers).Count().IsEqualTo(1);

            var unmap = new UnmapDrive
            {
                DriveLetter = newDrivers[0].TrimEnd('\\')
            };

            await Assert.That(WorkflowInvoker.Invoke(unmap)).IsTrue();
            await Assert.That(GetLogicalDrives().Length).IsEqualTo(driversCount);
        }

        [Test]
        public async Task MapDrive_MissingSharedDrivePath_Throws()
        {
            // SharedDrivePath is required; validation occurs before execution and raises ArgumentException
            await Assert.That(() => WorkflowInvoker.Invoke(new MapDrive()))
                .Throws<ArgumentException>();
        }

        [Test]
        public async Task UnmapDrive_MissingDriveLetter_Throws()
        {
            await Assert.That(() => WorkflowInvoker.Invoke(new UnmapDrive()))
                .Throws<InvalidOperationException>();
        }

        [Test]
        public async Task UnmapDrive_InvalidDriveLetterFormat_Throws()
        {
            // Invalid drive letter formats (not like 'X:') should be rejected
            await Assert.That(() => WorkflowInvoker.Invoke(new UnmapDrive(), new Dictionary<string, object>
            {
                { nameof(UnmapDrive.DriveLetter), "XYZ" }
            })).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task MapDrive_InvalidDriveLetterFormat_Throws()
        {
            // MapDrive should validate provided drive letter format and throw when invalid
            await Assert.That(() => WorkflowInvoker.Invoke(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), "A" },
                { nameof(MapDrive.SharedDrivePath), NetworkPath }
            })).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task MapDrive_SetsResponseCodeAndMessage()
        {
            // Ensure ResponseCode and ResponseMessage out arguments are populated when invoking
            var map = new MapDrive
            {
                SharedDrivePath = new InArgument<string>(NetworkPath)
            };

            var (result, outputs) = WorkflowInvoker.InvokeOutputs(map);

            // result indicates success of activity; outputs should contain response information
            await Assert.That(outputs).IsNotNull();
            await Assert.That(outputs.ContainsKey(nameof(MapDrive.ResponseCode))).IsTrue();
            await Assert.That(outputs.ContainsKey(nameof(MapDrive.ResponseMessage))).IsTrue();

            // ResponseCode should be convertible to int and ResponseMessage a non-empty string
            var codeObj = outputs[nameof(MapDrive.ResponseCode)];
            var msgObj = outputs[nameof(MapDrive.ResponseMessage)];

            await Assert.That(codeObj).IsNotNull();
            await Assert.That(codeObj).IsTypeOf<int>();
            await Assert.That(msgObj).IsNotNull();
            await Assert.That(msgObj).IsTypeOf<string>();

            WorkflowInvoker.Invoke(new UnmapDrive
            {
                DriveLetter = outputs["MappedDrive"].ToString()
            });
        }

        [Test]
        public async Task ShoudMapAndUnmap_SpecificDriveLetter()
        {
            const string driveLetter = "X:";
            var (result, outputs) = WorkflowInvoker.InvokeOutputs(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), driveLetter },
                { nameof(MapDrive.SharedDrivePath), NetworkPath }
            });

            await Assert.That(result).IsTrue();
            await Assert.That(outputs[nameof(MapDrive.MappedDrive)]).IsEqualTo(driveLetter);

            await Assert.That(WorkflowInvoker.Invoke(new UnmapDrive(), new Dictionary<string, object>
            {
                { nameof(UnmapDrive.DriveLetter), driveLetter },
            })).IsTrue();
        }

        [Test]
        public async Task SameLetterMapWithoutForce_ShouldReturnFalse()
        {
            const string driveLetter = "X:";
            var (result, outputs) = WorkflowInvoker.InvokeOutputs(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), driveLetter },
                { nameof(MapDrive.SharedDrivePath), NetworkPath }
            });

            await Assert.That(result).IsTrue();
            await Assert.That(outputs[nameof(MapDrive.MappedDrive)]).IsEqualTo(driveLetter);

            (result, _) = WorkflowInvoker.InvokeOutputs(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), driveLetter },
                { nameof(MapDrive.SharedDrivePath), NetworkPath }
            });

            await Assert.That(result).IsFalse();
            await Assert.That(WorkflowInvoker.Invoke(new UnmapDrive(), new Dictionary<string, object>
            {
                { nameof(UnmapDrive.DriveLetter), driveLetter },
            })).IsTrue();
        }

        [Test]
        public async Task SameLetterMapWithForce_ShouldReturnTrue()
        {
            const string driveLetter = "X:";
            var (result, outputs) = WorkflowInvoker.InvokeOutputs(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), driveLetter },
                { nameof(MapDrive.SharedDrivePath), NetworkPath }
            });

            await Assert.That(result).IsTrue();
            await Assert.That(outputs[nameof(MapDrive.MappedDrive)]).IsEqualTo(driveLetter);

            (result, _) = WorkflowInvoker.InvokeOutputs(new MapDrive(), new Dictionary<string, object>
            {
                { nameof(MapDrive.DriveLetter), driveLetter },
                { nameof(MapDrive.SharedDrivePath), NetworkPath },
                { nameof(MapDrive.Force), true }
            });

            await Assert.That(result).IsTrue();
            await Assert.That(WorkflowInvoker.Invoke(new UnmapDrive(), new Dictionary<string, object>
            {
                { nameof(UnmapDrive.DriveLetter), driveLetter },
            })).IsTrue();
        }

        [Test]
        public async Task UnmapDrive_AllDrives_ShouldUnmapAll()
        {
            var drives = GetLogicalDrives();
            for (int i = 0; i < 3; i++)
            {
                WorkflowInvoker.InvokeOutputs(new MapDrive
                {
                    SharedDrivePath = NetworkPath
                });
            }

            var drivesAfterMapping = GetLogicalDrives();
            await Assert.That(drivesAfterMapping.Length > drives.Length).IsTrue();

            WorkflowInvoker.Invoke(new UnmapDrive
            {
                AllDrives = true
            });

            var drivesAfterUnmapping = GetLogicalDrives();
            await Assert.That(drivesAfterUnmapping).IsEquivalentTo(drives);
        }
    }
}