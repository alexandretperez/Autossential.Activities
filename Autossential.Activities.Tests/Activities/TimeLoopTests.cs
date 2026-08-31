using Autossential.Activities.Tests.Extensions;
using Autossential.Activities.Tests.Helpers;
using Microsoft.VisualBasic.Activities;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Statements;

namespace Autossential.Activities.Tests.Activities
{
    public class TimeLoopTests
    {
        [Test]
        public async Task ShouldInitializeBody_WithDoSequence()
        {
            var loop = new TimeLoop();

            await Assert.That(loop.Body).IsNotNull();
            await Assert.That(loop.Body.Handler).IsNotNull();
            var sequence = await Assert.That(loop.Body.Handler).IsTypeOf<Sequence>();
            await Assert.That(sequence.DisplayName).IsEqualTo("Do");
        }

        [Test]
        public async Task Body_ShouldBeMutable()
        {
            var loop = new TimeLoop();
            var custom = new ActivityAction { Handler = new Sequence { DisplayName = "Custom" } };

            loop.Body = custom;

            await Assert.That(loop.Body).IsSameReferenceAs(custom);
        }

        [Test]
        [Arguments(1, 1, 1)]
        [Arguments(1, 0.5, 2)]
        [Arguments(1, 0.7, 2)]
        [Arguments(1, 0.25, 4)]
        public async Task ShouldStopIterating_AfterTimeoutExpires(int seconds, double interval, int expectedCount)
        {
            var count = 0;
            var loop = Build(TimeSpan.FromSeconds(seconds), interval, () => count++);
            WorkflowInvoker.Invoke(loop);
            await Assert.That(count).IsEqualTo(expectedCount);
        }

        [Test]
        public async Task WithZeroTimeout_ShouldNotRunBody()
        {
            var count = 0;
            var loop = Build(TimeSpan.Zero, 0, () => count++);
            await Assert.That(count).IsEqualTo(0);
        }

        [Test]
        public async Task ShouldIncrementIterationIndex_EachIteration()
        {
            var count = -1; // iteration index is zero-based index, for correct comparison, counter starts in -1
            var outputs = WorkflowInvoker.Invoke(new OutputWrapper<int>
            {
                Factory = outputVar => new TimeLoop
                {
                    IterationIndex = new OutArgument<int>(outputVar),
                    Timeout = TimeSpan.FromSeconds(1),
                    IntervalSeconds = new InArgument<double>(.1),
                    Body = new ActivityAction
                    {
                        Handler = new ActionInvoker(() => count++)
                    }
                }
            });

            await Assert.That(outputs["Output"]).IsEqualTo(count);
        }

        [Test]
        public async Task Result_ShouldBeTrue_WhenLoopEndsAfterTimeout()
        {
            var loop = Build(TimeSpan.FromMilliseconds(100), 0);
            await Assert.That(WorkflowInvoker.Invoke(loop)).IsTrue();
        }

        [Test]
        public async Task Result_ShouldBeFalse_WhenLoopEndsViaExit()
        {
            var loop = new TimeLoop
            {
                Timeout = new InArgument<TimeSpan>(TimeSpan.FromMilliseconds(300)),
                Body = new ActivityAction
                {
                    Handler = new Sequence { Activities = { new Exit() } }
                }
            };

            await Assert.That(WorkflowInvoker.Invoke(loop)).IsFalse();
        }

        [Test]
        public async Task WhenExitConditionIsFalse_ShouldContinueLoop()
        {
            var loop = new TimeLoop
            {
                Timeout = new InArgument<TimeSpan>(TimeSpan.FromMilliseconds(300)),
                Body = new ActivityAction
                {
                    Handler = new Sequence { Activities = { new Exit { Condition = false } } }
                }
            };

            await Assert.That(WorkflowInvoker.Invoke(loop)).IsTrue();
        }

        [Test]
        public async Task WhenExitCalledInsideNestedSequence_ShouldStopLoop()
        {
            var outerRun = false;
            var loop = new TimeLoop
            {
                Timeout = new InArgument<TimeSpan>(TimeSpan.FromSeconds(1)),
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new Sequence(),
                            new Sequence { Activities = { new Exit() }},
                            new ActionInvoker(() => outerRun = true)
                        }
                    }
                }
            };

            WorkflowInvoker.Invoke(loop);
            await Assert.That(outerRun).IsFalse();
        }

        [Test]
        public async Task WhenCanceled_ShouldMarkCanceled_AndStop()
        {
            var completed = new ManualResetEventSlim(false);
            ActivityInstanceState? completionState = null;

            var timeout = TimeSpan.FromSeconds(5);
            var loop = Build(timeout, .1);
            var app = AppRunner.Run(loop, args =>
            {
                completionState = args.CompletionState;
                completed.Set();
            });
            Thread.Sleep(300);
            app.Cancel();

            await Assert.That(completed.Wait(1000)).IsTrue();
            await Assert.That(completionState).IsEqualTo(ActivityInstanceState.Canceled);
        }

        [Test]
        public async Task WhenBodyThrows_ShouldFaultWorkflow()
        {
            var loop = Build(TimeSpan.FromSeconds(1), 0, () => throw new ApplicationException("Boom!"));

            await Assert.That(() => WorkflowInvoker.Invoke(loop))
                .Throws<ApplicationException>();
        }

        [Test]
        public async Task SameInstance_InvokedTwice_SecondRunExitsOnItsOwnCondition_NotFirstRunsResidualStop()
        {
            var bodyRunCount = 0;
            var allowExit = false;

            var loop = new TimeLoop
            {
                Timeout = new InArgument<TimeSpan>(ctx => TimeSpan.FromSeconds(1)),
                IntervalSeconds = new InArgument<double>(0),
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new ActionInvoker(() => bodyRunCount++),
                            new Exit { Condition = new InArgument<bool>(ctx => allowExit) }
                        }
                    }
                }
            };

            bodyRunCount = 0;
            allowExit = true;
            var (_, outputs1) = WorkflowInvoker.InvokeOutputs(loop);
            var result1 = (bool)outputs1["Result"];
            var bodyRunCount1 = bodyRunCount;

            bodyRunCount = 0;
            allowExit = false;
            var (_, outputs2) = WorkflowInvoker.InvokeOutputs(loop);
            var result2 = (bool)outputs2["Result"];
            var bodyRunCount2 = bodyRunCount;

            await Assert.That(result1).IsFalse(); // stop by exit
            await Assert.That(bodyRunCount1).IsGreaterThanOrEqualTo(1);

            await Assert.That(result2).IsTrue(); // stop by timeout
            await Assert.That(bodyRunCount2).IsGreaterThanOrEqualTo(1);
        }

        private static TimeLoop Build(TimeSpan timeout, double intervalSeconds, Action bodyAction = null!)
        {
            return new TimeLoop
            {
                Timeout = new InArgument<TimeSpan>(timeout),
                IntervalSeconds = new InArgument<double>(intervalSeconds),
                Body = new ActivityAction
                {
                    Handler = bodyAction == null ? new Sequence() : new ActionInvoker(bodyAction)
                }
            };
        }
    }
}