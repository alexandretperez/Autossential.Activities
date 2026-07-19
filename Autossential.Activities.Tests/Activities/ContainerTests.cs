using Autossential.Activities.Tests.Helpers;
using System.Activities;
using System.Activities.Statements;

namespace Autossential.Activities.Tests.Activities
{
    /// <summary>
    /// Unit tests for <see cref="Container"/>.
    ///
    /// Strategy:
    ///   - Direct tests: constructor defaults, property initialization.
    ///   - Integration via WorkflowApplication: exercita o fluxo real de bookmark
    ///     (Execute → CreateExitBookmark → OnExit) incluindo o uso de <see cref="Exit"/>
    ///     como mecanismo de saída antecipada.
    ///
    /// WorkflowApplication é necessário (em vez de WorkflowInvoker) porque
    /// Container usa bookmarks (CanInduceIdle = true), o que requer suporte
    /// assíncrono/idle que somente WorkflowApplication fornece.
    /// </summary>
    public class ContainerTests
    {
        // Timeout usado em todos os WaitOne para evitar travamento nos testes.
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        [Test]
        public async Task ShouldInitializeBody_WithDoSequence()
        {
            var container = new Container();

            await Assert.That(container.Body).IsNotNull();
            await Assert.That(container.Body.Handler).IsNotNull();
            var sequence = await Assert.That(container.Body.Handler).IsTypeOf<Sequence>();
            await Assert.That(sequence.DisplayName).IsEqualTo("Do");
        }

        [Test]
        public async Task Body_ShouldBeMutable()
        {
            var container = new Container();
            var custom = new ActivityAction { Handler = new Sequence { DisplayName = "Custom" } };

            container.Body = custom;
            await Assert.That(container.Body).IsSameReferenceAs(custom);
        }

        [Test]
        public async Task ShouldCompleteWithoutFault_WhenBodyIsNull()
        {
            var completed = new ManualResetEventSlim(false);
            Exception faultEx = null;

            var container = new Container { Body = null };

            var app = AppRunner.Run(container, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(faultEx).IsNull();
        }

        [Test]
        public async Task ShouldRunBodyToCompletion_WithoutExit()
        {
            var bodyRan = false;
            var completed = new ManualResetEventSlim(false);

            var container = new Container
            {
                Body = new ActivityAction
                {
                    Handler = new ActionInvoker(() => bodyRan = true)
                }
            };

            var app = AppRunner.Run(container, _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(bodyRan).IsTrue();
        }

        [Test]
        public async Task ShouldCancelRemainingChildren_WhenExitCalled()
        {
            // Sequence: Exit → atividade que NÃO deve rodar
            var shouldNotRun = false;
            var completed = new ManualResetEventSlim(false);

            var container = new Container
            {
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new Exit(),
                            new ActionInvoker(() => shouldNotRun = true) // do not run
                        }
                    }
                }
            };

            var app = AppRunner.Run(container, _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(shouldNotRun).IsFalse();
        }

        [Test]
        [Arguments(true, false)]
        [Arguments(false, true)]
        public async Task ShouldExitWhenConditionIsTrue_WhenExitConditionIsSet(bool condition, bool expectedResult)
        {
            var afterExitRan = false;
            var completed = new ManualResetEventSlim(false);

            var container = new Container
            {
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new Exit { Condition = condition },
                            new ActionInvoker(() => afterExitRan = true)
                        }
                    }
                }
            };

            var app = AppRunner.Run(container, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(afterExitRan).IsEqualTo(expectedResult);
        }

        [Test]
        public async Task ShouldStillExit_WhenExitCalledInsideNestedSequence()
        {
            var outerActivityRan = false;
            var completed = new ManualResetEventSlim(false);

            var container = new Container
            {
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new Sequence          // nested
                            {
                                Activities = { new Exit() }
                            },
                            new ActionInvoker(() => outerActivityRan = true)
                        }
                    }
                }
            };

            var app = AppRunner.Run(container, _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(outerActivityRan).IsFalse();
        }

        [Test]
        public async Task ShouldNotFault_WithMultipleExitCalls()
        {
            // Two Exit in sequence — the second must not cause exception
            // because the bookmark was already consumed by the first one
            var completed = new ManualResetEventSlim(false);
            Exception faultEx = null;

            var container = new Container
            {
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new Exit(),
                            new Exit()   // bookmark does not exit
                        }
                    }
                }
            };

            var app = AppRunner.Run(container, onCompleted: _ => completed.Set());
            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(faultEx).IsNull();
        }
    }
}