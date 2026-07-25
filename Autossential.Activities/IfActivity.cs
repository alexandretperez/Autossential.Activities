using Autossential.Activities.Helpers;
using Autossential.Activities.Properties;
using System.Activities;
using System.Activities.Statements;
using System.Windows.Markup;

namespace Autossential.Activities
{
    public sealed class IfActivity : NativeActivity
    {
        [RequiredArgument]
        public ActivityFunc<bool> Condition { get; set; }

        [DependsOn("Condition")]
        public Activity Then { get; set; }

        public Activity Else { get; set; }

        public IfActivity()
        {
            Condition = new ActivityFunc<bool>();
            Constraints.Add(ActivityConstraints.ConditionalValidationConstraint<IfActivity>(p => p.Condition != null && p.Condition.Handler is Activity<bool>, Resources.IfActivity_ErrorMsg_ConditionReturnsBoolean));
            Then = new Sequence
            {
                DisplayName = string.Empty
            };
            Else = new Sequence
            {
                DisplayName = string.Empty
            };
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.ScheduleFunc(Condition, OnCompleted);
        }

        private void OnCompleted(NativeActivityContext context, ActivityInstance completedInstance, bool result)
        {
            if (result)
            {
                if (Then is not null)
                    context.ScheduleActivity(Then);
            }
            else if (Else is not null)
            {
                context.ScheduleActivity(Else);
            }
        }
    }
}