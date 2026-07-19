using Autossential.Activities.Tests.Helpers;
using System.Activities;
using System.Activities.Statements;
using System.Globalization;

namespace Autossential.Activities.Tests.Activities
{
    public class CultureScopeTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        [Test]
        public async Task ShouldInitializeBody_WithDoSequence()
        {
            var scope = new CultureScope();

            await Assert.That(scope.Body).IsNotNull();
            await Assert.That(scope.Body.Handler).IsNotNull();
            var sequence = await Assert.That(scope.Body.Handler).IsTypeOf<Sequence>();
            await Assert.That(sequence.DisplayName).IsEqualTo("Do");
        }

        [Test]
        public async Task Body_ShouldBeMutable()
        {
            var scope = new CultureScope();
            var custom = new ActivityAction { Handler = new Sequence { DisplayName = "Custom" } };
            scope.Body = custom;
            await Assert.That(scope.Body).IsSameReferenceAs(custom);
        }

        [Test]
        [Arguments("en-US")]
        [Arguments("pt-BR")]
        [Arguments("fr-FR")]
        [Arguments("de-DE")]
        [Arguments("ja-JP")]
        public async Task ShouldApplyCulture_DuringBodyExecution(string cultureName)
        {
            CultureInfo cultureInsideBody = null;
            var completed = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>(cultureName),
                Body = new ActivityAction
                {
                    Handler = new ActionInvoker(() => cultureInsideBody = CultureInfo.CurrentCulture)
                }
            };

            AppRunner.Run(scope, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(cultureInsideBody).IsNotNull();
            await Assert.That(cultureInsideBody!.Name).IsEqualTo(cultureName);
        }

        [Test]
        public async Task WithEmptyCultureString_ShouldUseInvariantCulture()
        {
            CultureInfo inside = null;
            var completed = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>(""),
                Body = new ActivityAction
                {
                    Handler = new ActionInvoker(() => inside = CultureInfo.CurrentCulture)
                }
            };

            AppRunner.Run(scope, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(inside!.Name).IsEqualTo(CultureInfo.InvariantCulture.Name);
        }

        [Test]
        public async Task WithNullCulture_ShouldFallbackToEmptyString_AndUseInvariantCulture()
        {
            CultureInfo inside = null;
            var completed = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>((string)null),
                Body = new ActivityAction
                {
                    Handler = new ActionInvoker(() => inside = CultureInfo.CurrentCulture)
                }
            };

            AppRunner.Run(scope, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(inside!.Name).IsEqualTo(CultureInfo.InvariantCulture.Name);
        }

        [Test]
        public async Task ShouldRestoreOriginalCulture_AfterBodyCompletes()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            CultureInfo after = null;
            var completed = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>("ja-JP"),
                Body = new ActivityAction { Handler = new Sequence() }
            };

            AppRunner.Run(scope, onCompleted: _ =>
            {
                after = CultureInfo.CurrentCulture;
                completed.Set();
            });

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(after!.Name).IsEqualTo(originalCulture.Name);
        }

        [Test]
        public async Task NestedCultureScopes_ShouldApplyInnerCulture_ThenRestoreOuter()
        {
            CultureInfo innerCulture = null;
            CultureInfo outerCulture = null;
            var completed = new ManualResetEventSlim(false);

            var outer = new CultureScope
            {
                Culture = new InArgument<string>("en-US"),
                Body = new ActivityAction
                {
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new CultureScope
                            {
                                Culture = new InArgument<string>("fr-FR"),
                                Body    = new ActivityAction
                                {
                                    Handler = new ActionInvoker(() => innerCulture = CultureInfo.CurrentCulture)
                                }
                            },
                            new ActionInvoker(() => outerCulture = CultureInfo.CurrentCulture)
                        }
                    }
                }
            };

            AppRunner.Run(outer, onCompleted: _ => completed.Set());

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(innerCulture!.Name).IsEqualTo("fr-FR");
            await Assert.That(outerCulture!.Name).IsEqualTo("en-US");
        }

        [Test]
        public async Task ShouldRestoreOriginalCulture_AfterBodyFaults()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            CultureInfo after = null;
            var signaled = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>("de-DE"),
                Body = new ActivityAction
                {
                    Handler = new ActionInvoker(() => throw new ApplicationException("Boom!"))
                }
            };

            AppRunner.Run(scope, onCompleted: _ =>
            {
                after = CultureInfo.CurrentCulture;
                signaled.Set();
            });

            await Assert.That(signaled.Wait(TestTimeout)).IsTrue();
            await Assert.That(after!.Name).IsEqualTo(originalCulture.Name);
        }

        [Test]
        public async Task WithInvalidCultureName_ShouldCompleteWithFaultedState()
        {
            WorkflowApplicationCompletedEventArgs completedArgs = null;
            var signaled = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>("invalid-XX"),
                Body = new ActivityAction { Handler = new Sequence() }
            };

            AppRunner.Run(scope, onCompleted: args =>
            {
                completedArgs = args;
                signaled.Set();
            });

            await Assert.That(signaled.Wait(TestTimeout)).IsTrue();
            await Assert.That(completedArgs!.CompletionState).IsEqualTo(ActivityInstanceState.Faulted);
            await Assert.That(completedArgs.TerminationException).IsTypeOf<CultureNotFoundException>();
        }

        [Test]
        public async Task WhenBodyIsNull_ShouldCompleteWithoutFault()
        {
            WorkflowApplicationCompletedEventArgs completedArgs = null;
            var completed = new ManualResetEventSlim(false);

            var scope = new CultureScope
            {
                Culture = new InArgument<string>("en-US"),
                Body = null
            };

            AppRunner.Run(scope, onCompleted: args =>
            {
                completedArgs = args;
                completed.Set();
            });

            await Assert.That(completed.Wait(TestTimeout)).IsTrue();
            await Assert.That(completedArgs!.CompletionState).IsEqualTo(ActivityInstanceState.Closed);
        }
    }
}